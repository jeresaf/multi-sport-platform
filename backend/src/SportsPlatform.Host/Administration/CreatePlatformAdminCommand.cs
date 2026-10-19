using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Features.PlatformAdmins.CreatePlatformAdmin;

namespace SportsPlatform.Host.Administration;

/// <summary>
/// CLI <c>create-platform-admin --email &lt;address&gt;</c> (S03 D16): the first platform administrator. The password
/// comes from <see cref="PasswordVariable"/>, never from an argument (shell history) or a default. Runs as the system
/// actor and is audited as <c>platform.admin.create</c>; the email is never printed (ADR-0015), the new user's ID is.
/// </summary>
public static class CreatePlatformAdminCommand
{
    public const string PasswordVariable = "MSP_ADMIN_PASSWORD";

    public const string Usage = $"--email <address>, password in {PasswordVariable}";

    public static async Task<int> RunAsync(
        IServiceProvider services,
        IReadOnlyList<string> args,
        Func<string, string?> environment,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args is not ["--email", { Length: > 0 } email])
        {
            await error.WriteLineAsync($"Usage: create-platform-admin {Usage}.");
            return 1;
        }

        var password = environment(PasswordVariable);
        if (string.IsNullOrEmpty(password))
        {
            await error.WriteLineAsync($"Set {PasswordVariable} to the new administrator's password; there is no default.");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<AuditScope>().Begin(IdentityActions.PlatformAdminCreate, AuditSource.System);
        var result = await scope.ServiceProvider.GetRequiredService<CreatePlatformAdminHandler>()
            .HandleAsync(email, password, cancellationToken);
        if (result.IsFailure)
        {
            await error.WriteLineAsync($"{result.Error!.Code}: {result.Error.Message}");
            return 1;
        }

        await output.WriteLineAsync($"Created platform administrator {result.Value}.");
        return 0;
    }
}
