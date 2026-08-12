using FluentAssertions;
using SportsPlatform.Modules.Participants.Entities;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class AthleteProfileTests
{
    [Fact]
    public void Profile_SupportsIdentityPersonalContactAndEmergencyDetails()
    {
        var athlete = Athlete();

        athlete.UpdateIdentity("Amina Runner", "A. Runner", "https://assets.example/amina.webp");
        athlete.UpdatePersonalInfo(new DateTime(2002, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            "Female", "Senior Women", "Kenyan");
        athlete.UpdateContact("amina@example.com", "+254700000001", null);
        athlete.UpdateEmergencyContact("Salma Runner", "Parent", "+254700000002");

        athlete.Name.Should().Be("Amina Runner");
        athlete.CompetitionCategory.Should().Be("Senior Women");
        athlete.ContactEmail.Should().Be("amina@example.com");
        athlete.EmergencyContact.Should().Contain("Parent");
    }

    [Fact]
    public void Profile_RejectsInvalidBirthDateEmailAndPhoto()
    {
        var athlete = Athlete();

        ((Action)(() => athlete.UpdatePersonalInfo(DateTime.UtcNow.AddDays(1), null, null)))
            .Should().Throw<ArgumentException>().WithMessage("*future*");
        ((Action)(() => athlete.UpdateContact("not-an-email", null, null)))
            .Should().Throw<ArgumentException>().WithMessage("*email*");
        ((Action)(() => athlete.UpdateIdentity("Runner", "Run", "file:///private/photo.png")))
            .Should().Throw<ArgumentException>().WithMessage("*HTTP(S)*");
    }

    [Fact]
    public void MedicalClearance_UsesStatusAndExpiry()
    {
        var athlete = Athlete();

        athlete.SetMedicalClearance("Cleared", DateTime.UtcNow.AddMonths(6));
        athlete.MedicalClearance.Should().BeTrue();
        athlete.SetMedicalClearance("Cleared", DateTime.UtcNow.AddMinutes(-1));
        athlete.MedicalClearance.Should().BeFalse();
        ((Action)(() => athlete.SetMedicalClearance("Unknown", null)))
            .Should().Throw<ArgumentException>().WithMessage("*not supported*");
    }

    [Fact]
    public void IdentificationDocuments_AreValidatedAndRoundTrip()
    {
        var athlete = Athlete();
        athlete.SetIdentificationDocuments(new[]
        {
            new AthleteIdentificationDocument(
                "Passport", "P123456", "Kenya", DateTime.UtcNow.AddYears(3),
                "https://assets.example/documents/passport.pdf")
        });

        var document = athlete.GetIdentificationDocuments().Single();
        document.Type.Should().Be("Passport");
        document.Number.Should().Be("P123456");
        ((Action)(() => athlete.SetIdentificationDocuments(new[]
        {
            new AthleteIdentificationDocument("Passport", "P123", FileUrl: "file:///passport.pdf")
        }))).Should().Throw<ArgumentException>().WithMessage("*HTTP(S)*");
    }

    private static Athlete Athlete() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Runner", "RUN");
}
