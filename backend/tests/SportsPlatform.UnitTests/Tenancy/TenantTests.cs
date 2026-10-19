using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>The tenant state machine (docs/domain/state-machines.md, "Tenant") and the tenant's profile.</summary>
public sealed class TenantTests
{
    private const string _reason = "Annual fee unpaid after three reminders";
    private static readonly DateTimeOffset _now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly TenantProfile _profile = new(
        "Kampala Metro Sports Association Limited", "Kampala Metro Sports Association", "UG");

    public enum Transition
    {
        Activate,
        Suspend,
        Restore,
        Archive,
        Unarchive,
    }

    /// <summary>Every allowed (status, transition) pair and where it leads; every other pair is refused.</summary>
    private static readonly Dictionary<(TenantStatus From, Transition Transition), TenantStatus> _allowed = new()
    {
        [(TenantStatus.Provisioning, Transition.Activate)] = TenantStatus.Active,
        [(TenantStatus.Active, Transition.Suspend)] = TenantStatus.Suspended,
        [(TenantStatus.Suspended, Transition.Restore)] = TenantStatus.Active,
        [(TenantStatus.Active, Transition.Archive)] = TenantStatus.Archived,
        [(TenantStatus.Suspended, Transition.Archive)] = TenantStatus.Archived,
        [(TenantStatus.Archived, Transition.Unarchive)] = TenantStatus.Suspended,
    };

    public static TheoryData<TenantStatus, Transition> EveryPair()
    {
        var data = new TheoryData<TenantStatus, Transition>();
        foreach (var status in Enum.GetValues<TenantStatus>())
        {
            foreach (var transition in Enum.GetValues<Transition>())
            {
                data.Add(status, transition);
            }
        }

        return data;
    }

    [Fact]
    public void A_new_tenant_is_provisioning_with_its_first_history_row_and_event()
    {
        var actor = new UserActor(new Guid("0192a1b2-0000-7000-8000-00000000a001"));

        var tenant = Tenant.Create("kmsa", _profile, actor, _now);

        tenant.ShouldSatisfyAllConditions(
            t => t.Id.ShouldNotBe(Guid.Empty),
            t => t.Slug.ShouldBe("kmsa"),
            t => t.Status.ShouldBe(TenantStatus.Provisioning),
            t => t.Profile.ShouldBe(_profile));
        var row = tenant.StatusHistory.ShouldHaveSingleItem();
        row.ShouldSatisfyAllConditions(
            r => r.TenantId.ShouldBe(tenant.Id),
            r => r.FromStatus.ShouldBeNull(),
            r => r.ToStatus.ShouldBe(TenantStatus.Provisioning),
            r => r.Reason.ShouldBeNull(),
            r => r.ActorKind.ShouldBe(ActorKind.User),
            r => r.ActorId.ShouldBe(actor.ActorId),
            r => r.OccurredAt.ShouldBe(_now));
        tenant.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new TenantCreatedDomainEvent(tenant.Id, "kmsa", TenantStatus.Provisioning));
    }

    [Theory]
    [InlineData("www")]
    [InlineData("Kmsa")]
    [InlineData("-kmsa")]
    [InlineData("km")]
    public void Create_refuses_an_invalid_or_reserved_slug(string slug) =>
        Should.Throw<ArgumentException>(() => Tenant.Create(slug, _profile, SystemActor.Instance, _now));

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Each_transition_is_allowed_only_from_its_states(TenantStatus from, Transition transition)
    {
        var tenant = TenantIn(from);
        tenant.ClearDomainEvents();
        var historyBefore = tenant.StatusHistory.Count;

        var result = Apply(tenant, transition);

        if (_allowed.TryGetValue((from, transition), out var to))
        {
            result.IsSuccess.ShouldBeTrue();
            tenant.Status.ShouldBe(to);
            var row = tenant.StatusHistory[^1];
            tenant.StatusHistory.Count.ShouldBe(historyBefore + 1);
            row.FromStatus.ShouldBe(from);
            row.ToStatus.ShouldBe(to);
            row.Reason.ShouldBe(NeedsReason(transition) ? _reason : null);
            tenant.DomainEvents.ShouldHaveSingleItem().ShouldBe(new TenantStatusChangedDomainEvent(tenant.Id, from, to));
        }
        else
        {
            result.Error.ShouldBe(TenancyErrors.InvalidTransition);
            tenant.Status.ShouldBe(from);
            tenant.StatusHistory.Count.ShouldBe(historyBefore);
            tenant.DomainEvents.ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Transitions_that_need_a_reason_refuse_a_blank_one(string reason)
    {
        var active = TenantIn(TenantStatus.Active);

        Should.Throw<ArgumentException>(() => active.Suspend(reason, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => active.Archive(reason, SystemActor.Instance, _now));
        Should.Throw<ArgumentException>(() => TenantIn(TenantStatus.Archived).Unarchive(reason, SystemActor.Instance, _now));
    }

    [Fact]
    public void A_reason_is_trimmed_and_at_most_500_characters()
    {
        var tenant = TenantIn(TenantStatus.Active);
        Should.Throw<ArgumentException>(() => tenant.Suspend(new string('x', 501), SystemActor.Instance, _now));

        tenant.Suspend($"  {_reason}  ", SystemActor.Instance, _now).IsSuccess.ShouldBeTrue();

        tenant.StatusHistory[^1].Reason.ShouldBe(_reason);
    }

    [Fact]
    public void Profile_update_reports_whether_anything_changed()
    {
        var tenant = Tenant.Create("kmsa", _profile, SystemActor.Instance, _now);

        tenant.UpdateProfile(_profile).ShouldBeFalse();
        tenant.UpdateProfile(_profile with { DisplayName = "KMSA", CountryCode = "KE" }).ShouldBeTrue();

        tenant.ShouldSatisfyAllConditions(
            t => t.DisplayName.ShouldBe("KMSA"),
            t => t.CountryCode.ShouldBe("KE"),
            t => t.LegalName.ShouldBe(_profile.LegalName),
            t => t.Status.ShouldBe(TenantStatus.Provisioning));
    }

    private static Tenant TenantIn(TenantStatus status)
    {
        var tenant = Tenant.Create("elssc", _profile, SystemActor.Instance, _now);
        var path = status switch
        {
            TenantStatus.Provisioning => [],
            TenantStatus.Active => [Transition.Activate],
            TenantStatus.Suspended => [Transition.Activate, Transition.Suspend],
            TenantStatus.Archived => new[] { Transition.Activate, Transition.Archive },
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        foreach (var step in path)
        {
            Apply(tenant, step).IsSuccess.ShouldBeTrue();
        }

        return tenant;
    }

    private static bool NeedsReason(Transition transition) =>
        transition is Transition.Suspend or Transition.Archive or Transition.Unarchive;

    private static Result Apply(Tenant tenant, Transition transition) => transition switch
    {
        Transition.Activate => tenant.Activate(SystemActor.Instance, _now),
        Transition.Suspend => tenant.Suspend(_reason, SystemActor.Instance, _now),
        Transition.Restore => tenant.Restore(SystemActor.Instance, _now),
        Transition.Archive => tenant.Archive(_reason, SystemActor.Instance, _now),
        _ => tenant.Unarchive(_reason, SystemActor.Instance, _now),
    };

    private sealed record UserActor(Guid ActorId) : ICurrentActor
    {
        public ActorKind Kind => ActorKind.User;
    }
}
