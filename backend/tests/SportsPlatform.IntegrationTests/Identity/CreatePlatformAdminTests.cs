using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Host.Administration;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// CLI <c>create-platform-admin</c> (S03 D16): a confirmed user with <c>PLATFORM_ADMIN</c>, audited as the system actor;
/// refuses a missing or short password and an email that any user already has.
/// </summary>
public sealed class CreatePlatformAdminTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _email = "ruth.akello@msp.example";

    [Fact]
    public async Task Creates_a_confirmed_user_with_PLATFORM_ADMIN_and_prints_its_ID_but_not_the_email()
    {
        var password = TestPasswords.New();

        var run = await RunAsync(["--email", _email], password);

        run.ExitCode.ShouldBe(0, run.Error);
        var user = (await UsersAsync()).ShouldHaveSingleItem();
        run.Output.ShouldBe($"Created platform administrator {user.Id}.{Environment.NewLine}");
        run.Output.ShouldNotContain("ruth", Case.Insensitive);
        user.Email.ShouldBe(_email);
        user.EmailConfirmed.ShouldBeTrue();
        user.Id.Version.ShouldBe(7);

        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        (await users.CheckPasswordAsync((await users.FindByEmailAsync(_email))!, password)).ShouldBeTrue();

        var assignment = (await AssignmentsAsync()).ShouldHaveSingleItem();
        assignment.ShouldSatisfyAllConditions(
            a => a.UserId.ShouldBe(user.Id),
            a => a.RoleCode.ShouldBe(PlatformRoles.Admin),
            a => a.GrantedBy.ShouldBe(SystemActor.Id),
            a => a.GrantedAt.ShouldBe(App.Clock.GetUtcNow()),
            a => a.IsActive.ShouldBeTrue());
    }

    [Fact]
    public async Task Is_audited_as_platform_admin_create_by_the_system_without_the_email()
    {
        await RunAsync(["--email", _email], TestPasswords.New());
        var userId = (await UsersAsync()).ShouldHaveSingleItem().Id;

        var processor = App.Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(Ct);
        await processor.ProcessOnceAsync(Ct);

        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        entry.ShouldSatisfyAllConditions(
            e => e.ActionCode.ShouldBe("platform.admin.create"),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.Source.ShouldBe(AuditSource.System),
            e => e.ActorKind.ShouldBe(ActorKind.System),
            e => e.ActorId.ShouldBe(SystemActor.Id),
            e => e.TenantId.ShouldBeNull(),
            e => e.EntityType.ShouldBe("User"),
            e => e.EntityId.ShouldBe(userId),
            e => e.Metadata.ShouldBeNull());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Refuses_a_missing_password(string? password)
    {
        var run = await RunAsync(["--email", _email], password);

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain(CreatePlatformAdminCommand.PasswordVariable);
        (await UsersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Refuses_a_password_shorter_than_12_characters()
    {
        var run = await RunAsync(["--email", _email], TestPasswords.TooShort());

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldStartWith("AUTH_PASSWORD_REJECTED:");
        (await UsersAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Refuses_an_existing_platform_admin_whatever_the_case_of_the_email()
    {
        (await RunAsync(["--email", _email], TestPasswords.New())).ExitCode.ShouldBe(0);

        var run = await RunAsync(["--email", _email.ToUpperInvariant()], TestPasswords.New());

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldStartWith("AUTH_USER_EXISTS:");
        (await UsersAsync()).ShouldHaveSingleItem();
        (await AssignmentsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Refuses_an_existing_user_without_granting_the_role_or_changing_the_password()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            (await users.CreateAsync(User.Create(_email, emailConfirmed: true), TestPasswords.New())).Succeeded.ShouldBeTrue();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().SaveChangesAsync(Ct);
        }

        var stamp = (await UsersAsync()).ShouldHaveSingleItem().SecurityStamp;

        var run = await RunAsync(["--email", _email], TestPasswords.New());

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldStartWith("AUTH_USER_EXISTS:");
        (await UsersAsync()).ShouldHaveSingleItem().SecurityStamp.ShouldBe(stamp);
        (await AssignmentsAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ruth.akello")]
    [InlineData("   ")]
    public async Task Refuses_an_invalid_email(string email)
    {
        var run = await RunAsync(["--email", email], TestPasswords.New());

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldStartWith("VALIDATION_INVALID_REQUEST:");
        (await UsersAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData]
    [InlineData("--email")]
    [InlineData("--name", _email)]
    [InlineData("--email", _email, "--force")]
    public async Task Refuses_other_arguments(params string[] args)
    {
        var run = await RunAsync(args, TestPasswords.New());

        run.ExitCode.ShouldBe(1);
        run.Error.ShouldStartWith("Usage: create-platform-admin");
        (await UsersAsync()).ShouldBeEmpty();
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(string[] args, string? password)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CreatePlatformAdminCommand.RunAsync(
            App.Services,
            args,
            name => name == CreatePlatformAdminCommand.PasswordVariable ? password : null,
            output,
            error,
            Ct);
        return (exitCode, output.ToString(), error.ToString());
    }

    private async Task<List<User>> UsersAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<User>().AsNoTracking().ToListAsync(Ct);
    }

    private async Task<List<PlatformRoleAssignment>> AssignmentsAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Set<PlatformRoleAssignment>()
            .AsNoTracking().ToListAsync(Ct);
    }
}
