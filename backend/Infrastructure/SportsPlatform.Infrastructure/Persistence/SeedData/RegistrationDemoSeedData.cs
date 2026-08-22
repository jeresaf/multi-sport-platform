using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Registrations.Services;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

/// <summary>
/// Deterministic registration data that exercises every current registration and
/// form-submission state. It intentionally belongs to the National Sports tenant
/// so an administrator can test the complete workflow in one workspace.
/// </summary>
public static class RegistrationDemoSeedData
{
    public static readonly Guid TeamEntryFormId = DemoId(100);
    public static readonly Guid AthleteProfileFormId = DemoId(101);
    public static readonly Guid ArchivedFormId = DemoId(102);
    public static readonly Guid FootballPolicyId = DemoId(200);

    private static readonly Guid SystemAdministratorId =
        Guid.Parse("00000000-0000-0000-0000-000000000020");

    public static async Task EnsureAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Competitions.AnyAsync(
                item => item.Id == DemoSeedData.FootballCompetitionId,
                cancellationToken))
            return;

        await EnsureFormsAsync(context, cancellationToken);
        await EnsurePolicyAsync(context, cancellationToken);
        await EnsureRegistrationsAsync(context, cancellationToken);
        await EnsureSubmissionsAsync(context, cancellationToken);
    }

    private static async Task EnsureFormsAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        var formIds = new[] { TeamEntryFormId, AthleteProfileFormId, ArchivedFormId };
        var existingIds = await context.RegistrationForms
            .Where(item => formIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        if (!existingIds.Contains(TeamEntryFormId))
        {
            var form = new RegistrationForm(
                TeamEntryFormId,
                DemoSeedData.NationalSportsTenantId,
                "National Championship Team Entry",
                "admin@sportsplatform.com",
                DemoSeedData.FootballCompetitionId,
                "Competition-specific entry form for team, squad, document and consent testing.");
            form.UpdateSchema(SerializeSchema(TeamEntryFields()));
            context.RegistrationForms.Add(form);
        }

        if (!existingIds.Contains(AthleteProfileFormId))
        {
            var form = new RegistrationForm(
                AthleteProfileFormId,
                DemoSeedData.NationalSportsTenantId,
                "Reusable Athlete Profile and Consent",
                "admin@sportsplatform.com",
                description: "Reusable profile form covering every supported field type and conditional fields.");
            form.UpdateSchema(SerializeSchema(AthleteProfileFields()));
            context.RegistrationForms.Add(form);
        }

        if (!existingIds.Contains(ArchivedFormId))
        {
            var form = new RegistrationForm(
                ArchivedFormId,
                DemoSeedData.NationalSportsTenantId,
                "Legacy Paper Registration Archive",
                "admin@sportsplatform.com",
                description: "Inactive historical form retained to test archived form presentation.");
            form.UpdateSchema(SerializeSchema(
            [
                new RegistrationFormField
                {
                    Id = "archiveReference", FieldType = "Text", Label = "Archive reference",
                    IsRequired = true, Order = 1, MaxLength = 50
                }
            ]));
            form.Deactivate();
            context.RegistrationForms.Add(form);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsurePolicyAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.CompetitionEligibilityPolicies.AnyAsync(
                item => item.Id == FootballPolicyId,
                cancellationToken))
            return;

        var policy = new CompetitionEligibilityPolicy(
            FootballPolicyId,
            DemoSeedData.NationalSportsTenantId,
            DemoSeedData.FootballCompetitionId);
        policy.Configure(
            minimumSquadSize: 15,
            maximumSquadSize: 30,
            minimumAge: 16,
            maximumAge: 45,
            minimumWeightKg: null,
            maximumWeightKg: null,
            registrationDeadline: new DateTime(2030, 1, 2, 23, 59, 59, DateTimeKind.Utc),
            requiredDocumentTypes: ["Team roster", "Player identification", "Medical clearance"],
            requireSquadNumbers: true,
            requireUniqueSquadNumbers: true);
        context.CompetitionEligibilityPolicies.Add(policy);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRegistrationsAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new RegistrationDefinition(1, DemoIdForFootballTeam(0), DemoSeedData.FootballCompetitionId, RegistrationStatus.Draft),
            new RegistrationDefinition(2, DemoIdForFootballTeam(1), DemoSeedData.FootballCompetitionId, RegistrationStatus.Submitted),
            new RegistrationDefinition(3, DemoIdForFootballTeam(2), DemoSeedData.FootballCompetitionId, RegistrationStatus.UnderReview),
            new RegistrationDefinition(4, DemoIdForFootballTeam(3), DemoSeedData.FootballCompetitionId, RegistrationStatus.Approved),
            new RegistrationDefinition(5, DemoIdForFootballTeam(4), DemoSeedData.FootballCompetitionId, RegistrationStatus.Rejected),
            new RegistrationDefinition(6, DemoIdForFootballTeam(5), DemoSeedData.FootballCompetitionId, RegistrationStatus.Waitlisted),
            new RegistrationDefinition(7, DemoIdForFootballTeam(6), DemoSeedData.FootballCompetitionId, RegistrationStatus.Withdrawn),
            new RegistrationDefinition(8, DemoIdForFootballTeam(7), DemoSeedData.FootballCompetitionId, RegistrationStatus.Confirmed),
            new RegistrationDefinition(9, DemoIdForRugbyTeam(0), RugbyDemoSeedData.SevensCompetitionId, RegistrationStatus.Cancelled),
            new RegistrationDefinition(10, DemoIdForRugbyTeam(1), RugbyDemoSeedData.SevensCompetitionId, RegistrationStatus.NoShow)
        };
        var availableParticipantIds = await context.Participants
            .Where(item => definitions.Select(definition => definition.ParticipantId).Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var availableCompetitionIds = await context.Competitions
            .Where(item => definitions.Select(definition => definition.CompetitionId).Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var registrationIds = definitions.Select(item => DemoId(300 + item.Number)).ToArray();
        var existingIds = await context.Registrations
            .Where(item => registrationIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var definition in definitions.Where(item =>
                     availableParticipantIds.Contains(item.ParticipantId)
                     && availableCompetitionIds.Contains(item.CompetitionId)
                     && !existingIds.Contains(DemoId(300 + item.Number))))
        {
            var registration = Registration.CreateForCompetition(
                DemoId(300 + definition.Number),
                definition.CompetitionId,
                definition.ParticipantId,
                RegistrationType.Team);
            registration.SetNotes($"Deterministic test entry for the {definition.Status} workflow.");
            registration.SetEntryData(JsonSerializer.Serialize(new
            {
                manager = $"Team Manager {definition.Number:D2}",
                squadSize = 14 + definition.Number,
                primaryColour = definition.Number % 2 == 0 ? "Blue" : "Red"
            }));
            registration.SetEntryFee(2500m, definition.Number % 3 != 0);
            Transition(registration, definition.Status);
            context.Registrations.Add(registration);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureSubmissionsAsync(
        SportsPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        var form = await context.RegistrationForms.SingleAsync(
            item => item.Id == TeamEntryFormId,
            cancellationToken);
        var definitions = new[]
        {
            new SubmissionDefinition(1, DemoIdForFootballTeam(0), RegistrationFormSubmissionStatus.Draft),
            new SubmissionDefinition(2, DemoIdForFootballTeam(1), RegistrationFormSubmissionStatus.Submitted),
            new SubmissionDefinition(3, DemoIdForFootballTeam(2), RegistrationFormSubmissionStatus.UnderReview),
            new SubmissionDefinition(4, DemoIdForFootballTeam(3), RegistrationFormSubmissionStatus.Approved),
            new SubmissionDefinition(5, DemoIdForFootballTeam(4), RegistrationFormSubmissionStatus.Rejected),
            new SubmissionDefinition(6, DemoIdForFootballTeam(5), RegistrationFormSubmissionStatus.ChangesRequested)
        };
        var submissionIds = definitions.Select(item => DemoId(400 + item.Number)).ToArray();
        var existingIds = await context.RegistrationFormSubmissions
            .Where(item => submissionIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var definition in definitions.Where(item => !existingIds.Contains(DemoId(400 + item.Number))))
        {
            var submission = new RegistrationFormSubmission(
                DemoId(400 + definition.Number),
                form.Id,
                definition.ParticipantId,
                form.Version,
                form.FormSchema,
                DemoId(1001));
            submission.UpdateFormData(JsonSerializer.Serialize(new
            {
                managerName = $"Team Manager {definition.Number:D2}",
                managerEmail = $"manager{definition.Number:D2}@demo.sportsplatform.local",
                squadSize = 14 + definition.Number,
                homeColours = definition.Number % 2 == 0 ? "Blue and gold" : "Red and white",
                rosterDocument = new
                {
                    url = $"https://files.demo.sportsplatform.local/rosters/team-{definition.Number:D2}.pdf",
                    size = 180000 + definition.Number,
                    contentType = "application/pdf"
                },
                declaration = true
            }));
            Transition(submission, definition.Status);
            context.RegistrationFormSubmissions.Add(submission);
        }

        await context.SaveChangesAsync(cancellationToken);

        var linkedRegistrationIds = definitions.Select(item => DemoId(300 + item.Number)).ToArray();
        var linkedSubmissionIds = definitions.Select(item => DemoId(400 + item.Number)).ToArray();
        var registrations = await context.Registrations
            .Where(item => linkedRegistrationIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var submissions = await context.RegistrationFormSubmissions
            .Where(item => linkedSubmissionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var definition in definitions)
        {
            var registration = registrations[DemoId(300 + definition.Number)];
            var submission = submissions[DemoId(400 + definition.Number)];
            if (!registration.FormSubmissionId.HasValue)
                registration.LinkFormSubmission(submission.Id);
            if (!submission.RegistrationId.HasValue)
                submission.LinkRegistration(registration.Id);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void Transition(Registration registration, RegistrationStatus status)
    {
        if (status == RegistrationStatus.Draft) return;
        registration.Submit(SystemAdministratorId);
        if (status == RegistrationStatus.Submitted) return;
        if (status == RegistrationStatus.Cancelled)
        {
            registration.Cancel("Team withdrew before the competition draw.");
            return;
        }
        if (status == RegistrationStatus.Withdrawn)
        {
            registration.Withdraw("Travel arrangements could not be completed.");
            return;
        }
        registration.StartReview();
        if (status == RegistrationStatus.UnderReview) return;
        if (status == RegistrationStatus.Rejected)
        {
            registration.RecordEligibility(["Squad contains fewer than 15 eligible players."]);
            registration.Reject(SystemAdministratorId, "Squad size is below the competition minimum.");
            return;
        }
        if (status == RegistrationStatus.Waitlisted)
        {
            registration.RecordEligibility([]);
            registration.AddToWaitlist(SystemAdministratorId, "Competition capacity is currently full.");
            return;
        }

        if (status == RegistrationStatus.Approved)
            registration.RecordEligibility(
                ["One medical clearance expires before the final."],
                SystemAdministratorId,
                "A replacement medical clearance is already scheduled.");
        else
            registration.RecordEligibility([]);
        registration.Approve(SystemAdministratorId, "Eligibility and submitted documents verified.");
        if (status is RegistrationStatus.Confirmed or RegistrationStatus.NoShow)
            registration.Confirm();
        if (status == RegistrationStatus.NoShow)
            registration.MarkAsNoShow();
    }

    private static void Transition(
        RegistrationFormSubmission submission,
        RegistrationFormSubmissionStatus status)
    {
        if (status == RegistrationFormSubmissionStatus.Draft) return;
        submission.Submit();
        if (status == RegistrationFormSubmissionStatus.Submitted) return;
        submission.UnderReview();
        submission.SetReviewer(SystemAdministratorId);
        if (status == RegistrationFormSubmissionStatus.UnderReview) return;
        if (status == RegistrationFormSubmissionStatus.Approved)
            submission.Approve();
        else if (status == RegistrationFormSubmissionStatus.Rejected)
            submission.Reject("The uploaded roster is missing player identification pages.");
        else
            submission.RequestChanges("Replace the roster file and confirm the final squad numbers.");
    }

    private static IReadOnlyList<RegistrationFormField> TeamEntryFields() =>
    [
        new() { Id = "managerName", FieldType = "Text", Label = "Team manager name", IsRequired = true, Order = 1, MinLength = 3, MaxLength = 100 },
        new() { Id = "managerEmail", FieldType = "Text", Label = "Team manager email", IsRequired = true, Order = 2, ValidationRegex = "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", ValidationMessage = "Enter a valid email address." },
        new() { Id = "squadSize", FieldType = "Number", Label = "Squad size", IsRequired = true, Order = 3, MinValue = 15, MaxValue = 30 },
        new() { Id = "homeColours", FieldType = "Text", Label = "Home colours", IsRequired = true, Order = 4, MaxLength = 100 },
        new() { Id = "rosterDocument", FieldType = "File", Label = "Signed team roster", IsRequired = true, Order = 5, AllowedContentTypes = ["application/pdf"], MaxFileSizeBytes = 5 * 1024 * 1024 },
        new() { Id = "declaration", FieldType = "Consent", Label = "Eligibility declaration", IsRequired = true, Order = 6, ConsentText = "I confirm that the submitted squad meets the competition eligibility rules." }
    ];

    private static IReadOnlyList<RegistrationFormField> AthleteProfileFields() =>
    [
        new() { Id = "preferredName", FieldType = "Text", Label = "Preferred name", IsRequired = true, Order = 1, MinLength = 2, MaxLength = 80 },
        new() { Id = "dateOfBirth", FieldType = "Date", Label = "Date of birth", IsRequired = true, Order = 2 },
        new() { Id = "shirtSize", FieldType = "Select", Label = "Shirt size", IsRequired = true, Order = 3, Options = [new() { Value = "S", Label = "Small" }, new() { Value = "M", Label = "Medium" }, new() { Value = "L", Label = "Large" }, new() { Value = "XL", Label = "Extra large" }] },
        new() { Id = "disciplines", FieldType = "Checkbox", Label = "Preferred disciplines", Order = 4, Options = [new() { Value = "sprint", Label = "Sprint" }, new() { Value = "distance", Label = "Distance" }, new() { Value = "field", Label = "Field" }] },
        new() { Id = "requiresAssistance", FieldType = "Select", Label = "Accessibility assistance required", IsRequired = true, Order = 5, Options = [new() { Value = "no", Label = "No" }, new() { Value = "yes", Label = "Yes" }] },
        new() { Id = "assistanceDetails", FieldType = "Text", Label = "Accessibility requirements", Order = 6, ConditionalFieldId = "requiresAssistance", ConditionalValue = "yes", MaxLength = 500 },
        new() { Id = "medicalDocument", FieldType = "File", Label = "Medical clearance", Order = 7 },
        new() { Id = "profileConsent", FieldType = "Consent", Label = "Profile consent", IsRequired = true, Order = 8, ConsentText = "I consent to this information being used for competition administration." }
    ];

    private static string SerializeSchema(IEnumerable<RegistrationFormField> fields) =>
        JsonSerializer.Serialize(RegistrationFormSchema.Normalize(fields));

    private static Guid DemoIdForFootballTeam(int index) =>
        Guid.Parse($"10000000-0000-0000-0000-{200 + index:D12}");

    private static Guid DemoIdForRugbyTeam(int index) =>
        Guid.Parse($"70000000-0000-0000-0000-{200 + index:D12}");

    private static Guid DemoId(int value) =>
        Guid.Parse($"80000000-0000-0000-0000-{value:D12}");

    private sealed record RegistrationDefinition(
        int Number,
        Guid ParticipantId,
        Guid CompetitionId,
        RegistrationStatus Status);

    private sealed record SubmissionDefinition(
        int Number,
        Guid ParticipantId,
        RegistrationFormSubmissionStatus Status);
}
