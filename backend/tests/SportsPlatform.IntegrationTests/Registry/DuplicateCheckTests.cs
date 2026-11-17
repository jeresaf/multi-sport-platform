using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The duplicate check (S06-T2a; D5, D7; the sibling rule and the action <c>person.duplicate_check</c> by the owner):
/// the rules against real names and <c>pg_trgm</c>'s scores, decided pairs, the order and the cap, no <c>PRIVATE</c>
/// value in the answer, the request's refusals, the roles and the tenants.
/// </summary>
public sealed class DuplicateCheckTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _regNo = "KMSA_REG_NO";

    private static readonly PersonBiodata _peterOchiengOkot =
        new("Peter", "Ochieng", OtherNames: "Okot", DateOfBirth: new DateOnly(1998, 2, 10), Nationality: "UG");

    private static readonly PersonBiodata _patrickOkelloOwino =
        new("Patrick", "Okello Owino", DateOfBirth: new DateOnly(1995, 5, 5));

    private static readonly PersonBiodata _ivanMukasaBorn = new("Ivan", "Mukasa", DateOfBirth: new DateOnly(1999, 8, 30));

    // T2 acceptance: the table. pg_trgm's scores of the name keys are in the comments (shared trigrams / all trigrams).
    public static TheoryData<string, object, string[]> Cases => new()
    {
        {
            "the same names in the other order with the same birth date are LIKELY",
            new { firstName = "Ssekandi", lastName = "John", dateOfBirth = "2001-04-12" },
            ["John Ssekandi|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,SAME_NAME_KEY"]
        },
        {
            "accents, case, apostrophes and spaces don't make another name",
            new { firstName = " JÓHN ", lastName = "S'sekandi", dateOfBirth = "2001-04-12", nationality = "UG" },
            ["John Ssekandi|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,SAME_NAME_KEY,SAME_NATIONALITY"]
        },
        {
            "similarity 0.80 (12/15) with the same birth date is LIKELY",
            new { firstName = "John", lastName = "Sekandi", dateOfBirth = "2001-04-12" },
            ["John Ssekandi|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,NAME_SIMILARITY:80"]
        },
        {
            "similarity 0.79 (11/14) with the same birth date is only POSSIBLE",
            new { firstName = "Ivan", lastName = "Mukassa", dateOfBirth = "1999-08-30" },
            ["Ivan Mukasa|POSSIBLE|ACTIVE|SAME_DATE_OF_BIRTH,NAME_SIMILARITY:78"]
        },
        {
            "similarity 0.60 (9/15) with the same birth date is POSSIBLE",
            new { firstName = "Ivan", lastName = "Mukisa", dateOfBirth = "1999-08-30" },
            ["Ivan Mukasa|POSSIBLE|ACTIVE|SAME_DATE_OF_BIRTH,NAME_SIMILARITY:60"]
        },
        {
            "similarity 0.56 (9/16) is no match, even with the same birth date",
            new { firstName = "Ivan", lastName = "Mukiisa", dateOfBirth = "1999-08-30" },
            []
        },
        {
            "a close name without a date of birth is POSSIBLE",
            new { firstName = "John", lastName = "Sekandi" },
            ["John Ssekandi|POSSIBLE|ACTIVE|NAME_SIMILARITY:80"]
        },
        {
            "the same names without a date of birth are POSSIBLE",
            new { firstName = "John", lastName = "Ssekandi" },
            ["John Ssekandi|POSSIBLE|ACTIVE|SAME_NAME_KEY"]
        },
        {
            "a close name against a person without a date of birth is POSSIBLE",
            new { firstName = "Brenda", lastName = "Akelo", dateOfBirth = "2011-03-02" },
            ["Brenda Akello|POSSIBLE|ACTIVE|NAME_SIMILARITY:80"]
        },
        {
            "a close name born the year after is POSSIBLE",
            new { firstName = "John", lastName = "Sekandi", dateOfBirth = "2002-12-31" },
            ["John Ssekandi|POSSIBLE|ACTIVE|BIRTH_YEAR_WITHIN_ONE,NAME_SIMILARITY:80"]
        },
        {
            "a close name born two years apart is no match",
            new { firstName = "John", lastName = "Sekandi", dateOfBirth = "2003-01-01" },
            []
        },
        {
            "a close name born two years apart with the same nationality is POSSIBLE",
            new { firstName = "John", lastName = "Sekandi", dateOfBirth = "2003-01-01", nationality = "UG" },
            ["John Ssekandi|POSSIBLE|ACTIVE|NAME_SIMILARITY:80,SAME_NATIONALITY"]
        },
        {
            // Q44: a nationality in common counts, primary or secondary on either side.
            "a close name born two years apart whose secondary nationality is the person's is POSSIBLE",
            new { firstName = "John", lastName = "Sekandi", dateOfBirth = "2003-01-01", nationality = "KE", secondaryNationality = "UG" },
            ["John Ssekandi|POSSIBLE|ACTIVE|NAME_SIMILARITY:80,SAME_NATIONALITY"]
        },
        {
            "a close name born years apart with the person's secondary nationality is POSSIBLE",
            new { firstName = "Denis", lastName = "Opokaa", middleName = "Okot", dateOfBirth = "1985-01-01", nationality = "KE" },
            ["Denis Opoka|POSSIBLE|ACTIVE|NAME_SIMILARITY:83,SAME_NATIONALITY"]
        },
        {
            "the same names with another birth date are POSSIBLE, however far apart",
            new { firstName = "John", lastName = "Ssekandi", dateOfBirth = "1988-01-01" },
            ["John Ssekandi|POSSIBLE|ACTIVE|SAME_NAME_KEY"]
        },
        {
            "a twin with the same birth date and another first name is POSSIBLE, never LIKELY (0.33)",
            new { firstName = "Shamim", lastName = "Nankya", dateOfBirth = "2004-09-21" },
            ["Mariam Nankya|POSSIBLE|ACTIVE|SAME_DATE_OF_BIRTH,SAME_LAST_NAME"]
        },
        {
            "the same last name a day apart is no match",
            new { firstName = "Shamim", lastName = "Nankya", dateOfBirth = "2004-09-22" },
            []
        },
        {
            "a second name more is POSSIBLE with the same birth date (0.78, 14/18)",
            new { firstName = "Peter", lastName = "Ochieng", dateOfBirth = "1998-02-10" },
            ["Peter Ochieng|POSSIBLE|ACTIVE|SAME_DATE_OF_BIRTH,NAME_SIMILARITY:77,SAME_LAST_NAME"]
        },
        {
            // #95 Q2: hyphens are removed, not turned into spaces: "okelloowino patrick" against "okello owino patrick".
            "Okello-Owino against Okello Owino is LIKELY with the same birth date (0.82, 18/22)",
            new { firstName = "Patrick", lastName = "Okello-Owino", dateOfBirth = "1995-05-05" },
            ["Patrick Okello Owino|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,NAME_SIMILARITY:81"]
        },
        {
            "an alias matches like the name",
            new { firstName = "Deno", lastName = "Opoka", dateOfBirth = "1990-07-05" },
            ["Denis Opoka|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,SAME_NAME_KEY,ALIAS_MATCH"]
        },
        {
            "a removed alias doesn't match",
            new { firstName = "Okot", lastName = "Junior", dateOfBirth = "1990-07-05" },
            []
        },
        {
            "an external identifier with the same authority and value is EXACT_IDENTIFIER, whoever the names say",
            new { firstName = "Moses", lastName = "Kato", externalIdentifiers = new[] { new { authority = _regNo, value = " KMSA-00417 " } } },
            ["John Ssekandi|EXACT_IDENTIFIER|ACTIVE|SAME_EXTERNAL_ID:KMSA_REG_NO"]
        },
        {
            // D10: a value is never reused, so an ended identifier still matches.
            "an ended identifier still matches",
            new { firstName = "Moses", lastName = "Kato", externalIdentifiers = new[] { new { authority = "SCHOOL_ADMISSION_NO", value = "EL/2019/044" } } },
            ["Mariam Nankya|EXACT_IDENTIFIER|ACTIVE|SAME_EXTERNAL_ID:SCHOOL_ADMISSION_NO"]
        },
        {
            "the same value from another authority is no match",
            new { firstName = "Moses", lastName = "Kato", externalIdentifiers = new[] { new { authority = "SCHOOL_ADMISSION_NO", value = "KMSA-00417" } } },
            []
        },
        {
            "an archived person is found and marked",
            new { firstName = "Lillian", lastName = "Namaganda", dateOfBirth = "2010-06-14" },
            ["Lillian Namaganda|LIKELY|ARCHIVED|SAME_DATE_OF_BIRTH,SAME_NAME_KEY"]
        },
        {
            "somebody new matches nobody",
            new { firstName = "Moses", lastName = "Kato", dateOfBirth = "2001-04-12", nationality = "UG" },
            []
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task The_check_answers_who_may_be_the_same_person_and_why(string because, object candidate, string[] expected)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SeedRegistryAsync(kmsa);

        var answer = await CheckAsync(registrar, candidate);

        Summaries(answer).ShouldBe(expected, ignoreOrder: false, because);
    }

    // T2 acceptance: a decided pair isn't shown. Decisions are between two existing people, so the check names the one
    // being changed (D8); T2b's create writes them.
    [Fact]
    public async Task A_pair_decided_not_the_same_person_is_not_shown_again_and_the_person_itself_never_is()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var mariam = await SavePersonAsync(App.Services, kmsa, MariamNankya, Ct);
        var shamim = await SavePersonAsync(App.Services, kmsa, ShamimNankya, Ct);
        var shamimsData = new { firstName = "Shamim", lastName = "Nankya", dateOfBirth = "2004-09-21" };
        var mariamsData = new { firstName = "Mariam", lastName = "Nankya", dateOfBirth = "2004-09-21" };

        var before = await CheckAsync(registrar, Merge(shamimsData, shamim.Id));
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            context.Add(DuplicateDecision.NotSame(
                shamim.Id, mariam.Id, "Twin sister, different first name", SystemActor.Instance, Now));
            await context.SaveChangesAsync(Ct);
        }

        Summaries(before).ShouldBe(["Mariam Nankya|POSSIBLE|ACTIVE|SAME_DATE_OF_BIRTH,SAME_LAST_NAME"]);
        Summaries(await CheckAsync(registrar, Merge(shamimsData, shamim.Id))).ShouldBeEmpty();
        Summaries(await CheckAsync(registrar, Merge(mariamsData, mariam.Id))).ShouldBeEmpty();

        // The decision is about names and dates: Mariam's registration number typed on Shamim still blocks.
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            context.Add(ExternalIdentifier.ForPerson(mariam.Id, _regNo, "KMSA-00388"));
            await context.SaveChangesAsync(Ct);
        }

        var withMariamsNumber = Merge(shamimsData, shamim.Id);
        withMariamsNumber["externalIdentifiers"] = new[] { new { authority = _regNo, value = "KMSA-00388" } };
        Summaries(await CheckAsync(registrar, withMariamsNumber)).ShouldBe(
            ["Mariam Nankya|EXACT_IDENTIFIER|ACTIVE|SAME_EXTERNAL_ID:KMSA_REG_NO,SAME_DATE_OF_BIRTH,SAME_LAST_NAME"]);

        // Somebody new with Shamim's data still meets both sisters.
        Summaries(await CheckAsync(registrar, shamimsData)).ShouldBe(
        [
            "Shamim Nankya|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,SAME_NAME_KEY",
            "Mariam Nankya|POSSIBLE|ACTIVE|SAME_DATE_OF_BIRTH,SAME_LAST_NAME",
        ]);
    }

    [Fact]
    public async Task A_person_being_changed_is_not_matched_by_their_own_identifier()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, ("Johnny", AliasKind.Nickname), (_regNo, "KMSA-00417"), Ct);
        var body = new
        {
            firstName = "John",
            lastName = "Ssekandi",
            dateOfBirth = "2001-04-12",
            externalIdentifiers = new[] { new { authority = _regNo, value = "KMSA-00417" } },
        };

        Summaries(await CheckAsync(registrar, Merge(body, john.Id))).ShouldBeEmpty();
        Summaries(await CheckAsync(registrar, body)).ShouldBe(
            ["John Ssekandi|EXACT_IDENTIFIER|ACTIVE|SAME_EXTERNAL_ID:KMSA_REG_NO,SAME_DATE_OF_BIRTH,SAME_NAME_KEY"]);
    }

    [Fact]
    public async Task At_most_twenty_people_are_answered_strongest_first()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var born = new DateOnly(2003, 3, 3);
        for (var i = 0; i < 20; i++)
        {
            await SavePersonAsync(App.Services, kmsa, new PersonBiodata("Grace", "Atim"), Ct);
        }

        await SavePersonAsync(App.Services, kmsa, new PersonBiodata("Grace", "Atimo", DateOfBirth: born), Ct);
        await SavePersonAsync(App.Services, kmsa, new PersonBiodata("Atim", "Grace", DateOfBirth: born, DisplayName: "Grace A."), Ct);
        await SavePersonAsync(
            App.Services, kmsa, new PersonBiodata("Sarah", "Nabirye"), ("Sarah N", AliasKind.Nickname), (_regNo, "KMSA-00902"), Ct);

        var answer = await CheckAsync(registrar, new
        {
            firstName = "Grace",
            lastName = "Atim",
            dateOfBirth = "2003-03-03",
            externalIdentifiers = new[] { new { authority = _regNo, value = "KMSA-00902" } },
        });

        var summaries = Summaries(answer);
        summaries.Count.ShouldBe(20);
        summaries[0].ShouldBe("Sarah Nabirye|EXACT_IDENTIFIER|ACTIVE|SAME_EXTERNAL_ID:KMSA_REG_NO");
        summaries[1].ShouldBe("Grace A.|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,SAME_NAME_KEY");
        summaries.Skip(2).ShouldAllBe(summary => summary == "Grace Atim|POSSIBLE|ACTIVE|SAME_NAME_KEY");
    }

    // Review #97 R1: more rows match than the finder reads. The ones that matter are kept: the identifier's owner and a
    // person whose alias has the same key and who has the same birth date, both saved last and both unlike by own name.
    [Fact]
    public async Task With_more_matches_than_are_read_the_strongest_are_still_answered_first()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var born = new DateOnly(2003, 3, 3);
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            for (var i = 0; i < DuplicateFinder.MaxRows + 5; i++)
            {
                context.Add(NewPerson(new PersonBiodata("Grace", "Atim")));
            }

            var sarah = NewPerson(new PersonBiodata("Sarah", "Nabirye", DateOfBirth: born));
            sarah.AddAlias("Grace Atim", AliasKind.FormerName);
            var esther = NewPerson(new PersonBiodata("Esther", "Auma"));
            context.AddRange(sarah, esther, ExternalIdentifier.ForPerson(esther.Id, _regNo, "KMSA-00902"));
            await context.SaveChangesAsync(Ct);
        }

        var answer = await CheckAsync(registrar, new
        {
            firstName = "Grace",
            lastName = "Atim",
            dateOfBirth = "2003-03-03",
            externalIdentifiers = new[] { new { authority = _regNo, value = "KMSA-00902" } },
        });

        var summaries = Summaries(answer);
        summaries.Count.ShouldBe(20);
        summaries[0].ShouldBe("Esther Auma|EXACT_IDENTIFIER|ACTIVE|SAME_EXTERNAL_ID:KMSA_REG_NO");
        summaries[1].ShouldBe("Sarah Nabirye|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,SAME_NAME_KEY,ALIAS_MATCH");
        summaries.Skip(2).ShouldAllBe(summary => summary == "Grace Atim|POSSIBLE|ACTIVE|SAME_NAME_KEY");
    }

    // T2 acceptance: the check's answer carries no PRIVATE value. It also saves and audits nothing.
    [Fact]
    public async Task The_answer_carries_a_birth_year_and_never_a_date_of_birth_and_nothing_is_saved_or_audited()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);
        await SavePersonAsync(App.Services, kmsa, new PersonBiodata("John", "Sekandi", DateOfBirth: new DateOnly(2002, 11, 30)), Ct);
        App.Telemetry.Clear();

        using var response = await registrar.PostAsJsonAsync(
            DuplicateCheckPath, new { firstName = "John", lastName = "Ssekandi", dateOfBirth = "2001-04-12" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        var text = await response.Content.ReadAsStringAsync(Ct);
        var candidates = JsonDocument.Parse(text).RootElement.GetProperty("candidates");
        Summaries(JsonDocument.Parse(text).RootElement).ShouldBe(
        [
            "John Ssekandi|LIKELY|ACTIVE|SAME_DATE_OF_BIRTH,SAME_NAME_KEY",
            "John Sekandi|POSSIBLE|ACTIVE|BIRTH_YEAR_WITHIN_ONE,NAME_SIMILARITY:80",
        ]);
        (candidates[0].GetProperty("personId").GetGuid(), candidates[0].GetProperty("birthYear").GetInt32()).ShouldBe((john.Id, 2001));
        candidates[1].GetProperty("birthYear").GetInt32().ShouldBe(2002);
        candidates[0].EnumerateObject().Select(property => property.Name)
            .ShouldBe(["personId", "displayName", "birthYear", "status", "confidence", "evidence"]);
        text.ShouldNotContain("2001-04-12");
        text.ShouldNotContain("2002-11-30");
        text.ShouldNotContain("dateOfBirth", Case.Insensitive);
        JsonDocument.Parse(text).RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["candidates", "checkToken"]);

        await App.Telemetry.WaitForRequestLogAsync(DuplicateCheckPath);
        foreach (var date in new[] { "2001-04-12", "2002-11-30", "Ssekandi" })
        {
            App.Telemetry.Events.ShouldAllBe(e =>
                !e.RenderMessage(null).Contains(date, StringComparison.Ordinal)
                && e.Properties.Values.All(value => !value.ToString().Contains(date, StringComparison.Ordinal)));
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldBeEmpty();
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Person>().CountAsync(Ct)).ShouldBe(2);
        (await context.Set<DuplicateDecision>().CountAsync(Ct)).ShouldBe(0);
    }

    public static TheoryData<string, string, string> BadRequests => new()
    {
        { """{"lastName":"Ssekandi"}""", "VALIDATION_INVALID_REQUEST", "firstName" },
        { """{"firstName":"John","lastName":"  "}""", "VALIDATION_INVALID_REQUEST", "lastName" },
        { $$"""{"firstName":"{{new string('a', 101)}}","lastName":"Ssekandi"}""", "VALIDATION_INVALID_REQUEST", "firstName" },
        { $$"""{"firstName":"John","lastName":"Ssekandi","otherNames":"{{new string('a', 201)}}"}""", "VALIDATION_INVALID_REQUEST", "otherNames" },
        { """{"firstName":"John","lastName":"Ssekandi","nationality":"ug"}""", "VALIDATION_INVALID_REQUEST", "nationality" },
        { """{"firstName":"John","lastName":"Ssekandi","personId":"00000000-0000-0000-0000-000000000000"}""", "VALIDATION_INVALID_REQUEST", "personId" },
        {
            """{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[{"authority":"KMSA_REG_NO","value":"K-1"},{"authority":"KMSA_REG_NO","value":" "}]}""",
            "VALIDATION_INVALID_REQUEST", "externalIdentifiers[1].value"
        },
        {
            """{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[{"authority":"kmsa reg no","value":"K-1"}]}""",
            "VALIDATION_INVALID_REQUEST", "externalIdentifiers[0].authority"
        },
        {
            """{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[{"authority":"KMSA_REG_NO","value":"K\u0000-1"}]}""",
            "VALIDATION_INVALID_REQUEST", "externalIdentifiers[0].value"
        },
        {
            """{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[{"authority":"NIN\n","value":"K-1"}]}""",
            "VALIDATION_INVALID_REQUEST", "externalIdentifiers[0].authority"
        },
        { """{"firstName":"John","lastName":"Ssekandi","nationality":"UG\n"}""", "VALIDATION_INVALID_REQUEST", "nationality" },
        { """{"firstName":"John","lastName":"Ssekandi","nationality":"XX"}""", "VALIDATION_INVALID_REQUEST", "nationality" },
        { """{"firstName":"John","lastName":"Ssekandi","secondaryNationality":"ke"}""", "VALIDATION_INVALID_REQUEST", "secondaryNationality" },

        // Q43: the test clock is 2026-10-17 in Kampala.
        { """{"firstName":"John","lastName":"Ssekandi","dateOfBirth":"2026-10-18"}""", "PERSON_DATE_OF_BIRTH_IN_FUTURE", "dateOfBirth" },
        { """{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[null]}""", "VALIDATION_INVALID_REQUEST", "externalIdentifiers" },
        {
            $$"""{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[{{string.Join(',', Enumerable.Range(0, 11).Select(i => $$"""{"authority":"KMSA_REG_NO","value":"K-{{i}}"}"""))}}]}""",
            "VALIDATION_INVALID_REQUEST", "externalIdentifiers"
        },

        // #95 Q2: a name whose key is empty has nothing to compare.
        { """{"firstName":"--","lastName":"Ssekandi"}""", "PERSON_NAME_INVALID", "firstName" },
        { """{"firstName":"John","lastName":"'.'"}""", "PERSON_NAME_INVALID", "lastName" },

        // D10: national identity documents are not collected.
        {
            """{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[{"authority":"KMSA_REG_NO","value":"K-1"},{"authority":"NIN","value":"CM0000000000XX"}]}""",
            "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED", "externalIdentifiers[1].authority"
        },
        {
            """{"firstName":"John","lastName":"Ssekandi","externalIdentifiers":[{"authority":"PASSPORT","value":"A0000000"}]}""",
            "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED", "externalIdentifiers[0].authority"
        },
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task A_request_that_cant_be_checked_is_refused_on_its_field(string body, string code, string field)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await PostRawAsync(registrar, body);

        var status = code.StartsWith("PERSON_", StringComparison.Ordinal) ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.BadRequest;
        var problem = await response.ShouldBeProblemAsync(status, code);
        problem.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()))
            .ShouldBe([(field, code)]);
        problem.GetRawText().ShouldNotContain("CM0000000000XX");
    }

    [Fact]
    public async Task Unknown_members_and_unknown_people_are_refused()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var unknownMember = await PostRawAsync(registrar, """{"firstName":"John","lastName":"Ssekandi","status":"ARCHIVED"}""");
        using var unknownPerson = await registrar.PostAsJsonAsync(
            DuplicateCheckPath, new { firstName = "John", lastName = "Ssekandi", personId = Guid.CreateVersion7() }, Ct);

        await unknownMember.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        await unknownPerson.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
    }

    // D21: every change to people, the check included, is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_check(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);

        using var response = await client.PostAsJsonAsync(DuplicateCheckPath, new { firstName = "John", lastName = "Ssekandi" }, Ct);

        if (allowed)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    [Fact]
    public async Task Another_tenants_people_never_match_and_cant_be_named()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, teacher) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await SavePersonAsync(App.Services, elssc, JohnSsekandi, ("Johnny", AliasKind.Nickname), (_regNo, "KMSA-00417"), Ct);
        var body = new
        {
            firstName = "John",
            lastName = "Ssekandi",
            dateOfBirth = "2001-04-12",
            externalIdentifiers = new[] { new { authority = _regNo, value = "KMSA-00417" } },
        };

        Summaries(await CheckAsync(registrar, body)).ShouldBeEmpty();
        Summaries(await CheckAsync(teacher, body)).Count.ShouldBe(1);
        using var named = await registrar.PostAsJsonAsync(DuplicateCheckPath, Merge(body, theirs.Id), Ct);
        await named.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        kmsa.ShouldNotBe(elssc);
    }

    // The registry every case of the table is checked against.
    private async Task SeedRegistryAsync(Guid tenantId)
    {
        await SavePersonAsync(App.Services, tenantId, JohnSsekandi, ("Johnny", AliasKind.Nickname), (_regNo, "KMSA-00417"), Ct);
        await SavePersonAsync(
            App.Services, tenantId, MariamNankya, ("Mariam N", AliasKind.Nickname), ("SCHOOL_ADMISSION_NO", "EL/2019/044"), Ct);
        await SavePersonAsync(App.Services, tenantId, _ivanMukasaBorn, Ct);
        await SavePersonAsync(App.Services, tenantId, new PersonBiodata("Brenda", "Akello"), Ct);
        await SavePersonAsync(App.Services, tenantId, _peterOchiengOkot with { DisplayName = "Peter Ochieng" }, Ct);
        await SavePersonAsync(App.Services, tenantId, _patrickOkelloOwino, Ct);
        var lillian = await SavePersonAsync(
            App.Services, tenantId, new PersonBiodata("Lillian", "Namaganda", DateOfBirth: new DateOnly(2010, 6, 14)), Ct);
        await SetStatusAsync(App.Services, tenantId, lillian.Id, PersonStatus.Archived, Ct);

        // Denis has a former name that still matches and a nickname that was removed.
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var denis = NewPerson(DenisOpoka);
        denis.AddAlias("Deno Opoka", AliasKind.FormerName);
        var removed = denis.AddAlias("Okot Junior", AliasKind.Nickname);
        context.Add(denis);
        await context.SaveChangesAsync(Ct);
        (await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE registry.person_aliases
            SET removed_at = now(), removed_by = {SystemActor.Instance.ActorId}, remove_reason = 'Entered twice'
            WHERE id = {removed.Id}
            """, Ct)).ShouldBe(1);
        (await context.Database.ExecuteSqlAsync(
            $"UPDATE registry.external_identifiers SET valid_to = DATE '2024-12-06' WHERE value = 'EL/2019/044'", Ct)).ShouldBe(1);
    }

    private static async Task<JsonElement> CheckAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(DuplicateCheckPath, body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PostRawAsync(HttpClient client, string json) =>
        client.PostAsync(DuplicateCheckPath, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), Ct);

    // "display name|confidence|status|evidence codes, with their values".
    private static List<string> Summaries(JsonElement answer) =>
    [
        .. answer.GetProperty("candidates").EnumerateArray().Select(candidate =>
        {
            var evidence = candidate.GetProperty("evidence").EnumerateArray().Select(item =>
                item.GetProperty("value").GetString() is { } value
                    ? $"{item.GetProperty("code").GetString()}:{value}"
                    : item.GetProperty("code").GetString());
            return $"{candidate.GetProperty("displayName").GetString()}|{candidate.GetProperty("confidence").GetString()}|"
                + $"{candidate.GetProperty("status").GetString()}|{string.Join(',', evidence)}";
        }),
    ];

    // The body with personId: the check for a person being changed.
    private static Dictionary<string, object?> Merge(object body, Guid personId)
    {
        var merged = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(body))!;
        merged["personId"] = personId;
        return merged;
    }

    /// <summary>An active tenant (time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}
