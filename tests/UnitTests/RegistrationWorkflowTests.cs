using FluentAssertions;
using SportsPlatform.Modules.Registrations.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class RegistrationWorkflowTests
{
    [Fact]
    public void OfficialApproval_RecordsSubmitterReviewerReasonAndEligibilityStatus()
    {
        var submittedBy = Guid.NewGuid();
        var reviewedBy = Guid.NewGuid();
        var registration = CompetitionRegistration();

        registration.Submit(submittedBy);
        registration.StartReview();
        registration.Approve(reviewedBy, "Identity and competition eligibility verified");

        registration.Status.Should().Be(RegistrationStatus.Approved);
        registration.SubmittedBy.Should().Be(submittedBy);
        registration.ReviewedBy.Should().Be(reviewedBy);
        registration.ReviewNotes.Should().Be("Identity and competition eligibility verified");
        registration.RegisteredAt.Should().NotBeNull();
        registration.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public void OfficialDecisions_RequireARecordedReason()
    {
        var reviewer = Guid.NewGuid();
        var approve = SubmittedRegistration();
        var reject = SubmittedRegistration();
        var waitlist = SubmittedRegistration();
        var withdraw = SubmittedRegistration();

        approve.Invoking(item => item.Approve(reviewer, " "))
            .Should().Throw<ArgumentException>().WithMessage("*approval reason*");
        reject.Invoking(item => item.Reject(reviewer))
            .Should().Throw<ArgumentException>().WithMessage("*rejection reason*");
        waitlist.Invoking(item => item.AddToWaitlist(reviewer, null))
            .Should().Throw<ArgumentException>().WithMessage("*waitlist reason*");
        withdraw.Invoking(item => item.Withdraw(string.Empty))
            .Should().Throw<ArgumentException>().WithMessage("*withdrawal reason*");
    }

    [Fact]
    public void RejectedWaitlistedAndWithdrawnEntries_RetainTheirReasons()
    {
        var reviewer = Guid.NewGuid();
        var rejected = SubmittedRegistration();
        var waitlisted = SubmittedRegistration();
        var withdrawn = SubmittedRegistration();

        rejected.Reject(reviewer, "Required document is missing");
        waitlisted.AddToWaitlist(reviewer, "Competition capacity reached");
        withdrawn.Withdraw("Participant requested withdrawal");

        rejected.Status.Should().Be(RegistrationStatus.Rejected);
        rejected.ReviewNotes.Should().Be("Required document is missing");
        waitlisted.Status.Should().Be(RegistrationStatus.Waitlisted);
        waitlisted.ReviewNotes.Should().Be("Competition capacity reached");
        withdrawn.Status.Should().Be(RegistrationStatus.Withdrawn);
        withdrawn.CancellationReason.Should().Be("Participant requested withdrawal");
    }

    [Fact]
    public void EligibilityOverride_RequiresReasonAndRetainsDecisionEvidence()
    {
        var registration = SubmittedRegistration();
        var reviewer = Guid.NewGuid();

        registration.Invoking(item => item.RecordEligibility(["Squad is too small"], reviewer, " "))
            .Should().Throw<ArgumentException>().WithMessage("*override reason*");

        registration.RecordEligibility(["Squad is too small"], reviewer, "Approved by the eligibility panel");

        registration.EligibilityIssues().Should().ContainSingle("Squad is too small");
        registration.EligibilityOverriddenBy.Should().Be(reviewer);
        registration.EligibilityOverrideReason.Should().Be("Approved by the eligibility panel");
        registration.EligibilityCheckedAt.Should().NotBeNull();
        registration.EligibilityOverriddenAt.Should().NotBeNull();
    }

    [Fact]
    public void EligibilityPolicy_RejectsInvalidRangesAndNormalizesDocuments()
    {
        var policy = new CompetitionEligibilityPolicy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        policy.Invoking(item => item.Configure(20, 10, null, null, null, null, null, [], false, true))
            .Should().Throw<ArgumentException>().WithMessage("*squad size*");

        policy.Configure(10, 20, 16, 35, 50, 120, DateTime.UtcNow.AddDays(2),
            [" Passport ", "passport", "Medical certificate"], true, true);
        policy.RequiredDocumentTypes.Should().BeEquivalentTo("Passport", "Medical certificate");
        policy.RequireSquadNumbers.Should().BeTrue();
    }

    private static Registration SubmittedRegistration()
    {
        var registration = CompetitionRegistration();
        registration.Submit(Guid.NewGuid());
        return registration;
    }

    private static Registration CompetitionRegistration() =>
        Registration.CreateForCompetition(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), RegistrationType.Team);
}
