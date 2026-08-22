using Microsoft.EntityFrameworkCore;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Tenancy.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Sports.Services;

namespace SportsPlatform.Infrastructure.Persistence.SeedData;

public static class SeedData
{
    public static async Task InitializeAsync(SportsPlatformDbContext context)
    {
        // Only seed if database is empty
        if (await context.Tenants.AnyAsync())
        {
            await EnsurePublishedRulesetsAsync(context);
            await DemoSeedData.EnsureAsync(context);
            return;
        }

        // ============================================
        // SEED TENANTS
        // ============================================
        var defaultTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "Default",
            "Default",
            "default");
        defaultTenant.UpdateDetails("Default", "Default", "Default tenant for single-tenant deployments");
        
        var premierLeagueTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "Premier League",
            "PL",
            "premier-league");
        premierLeagueTenant.UpdateDetails("Premier League", "PL", "English Premier League football competitions");
        
        var nbaTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            "NBA",
            "NBA",
            "nba");
        nbaTenant.UpdateDetails("NBA", "NBA", "National Basketball Association");
        
        var athleticsTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            "World Athletics",
            "WA",
            "world-athletics");
        athleticsTenant.UpdateDetails("World Athletics", "WA", "International athletics competitions");
        
        var cricketTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000005"),
            "Cricket World",
            "CW",
            "cricket-world");
        cricketTenant.UpdateDetails("Cricket World", "CW", "International cricket competitions");
        
        var golfTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000006"),
            "PGA Tour",
            "PGA",
            "pga-tour");
        golfTenant.UpdateDetails("PGA Tour", "PGA", "Professional golf tournaments");
        
        var mmaTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000007"),
            "UFC",
            "UFC",
            "ufc");
        mmaTenant.UpdateDetails("UFC", "UFC", "Mixed martial arts events");
        
        var rugbyTenant = new Tenant(
            Guid.Parse("00000000-0000-0000-0000-000000000008"),
            "Six Nations Rugby",
            "6N",
            "six-nations");
        rugbyTenant.UpdateDetails("Six Nations Rugby", "6N", "Six Nations Championship");

        context.Tenants.AddRange(
            defaultTenant, premierLeagueTenant, nbaTenant, athleticsTenant,
            cricketTenant, golfTenant, mmaTenant, rugbyTenant);

        // ============================================
        // SEED ROLES AND PERMISSIONS
        // ============================================
        var adminRole = new Role(Guid.Parse("00000000-0000-0000-0000-000000000010"), "Admin", "System administrator with full access");
        var userRole = new Role(Guid.Parse("00000000-0000-0000-0000-000000000011"), "User", "Standard user with basic access");
        var managerRole = new Role(Guid.Parse("00000000-0000-0000-0000-000000000012"), "Manager", "Manager with elevated access");
        var scorerRole = new Role(Guid.Parse("00000000-0000-0000-0000-000000000013"), "Scorer", "Live scoring operator");
        context.Roles.AddRange(adminRole, userRole, managerRole, scorerRole);

        // Seed admin user
        var adminUser = new User(
            Guid.Parse("00000000-0000-0000-0000-000000000020"),
            "admin@sportsplatform.com",
            "Admin",
            "User",
            "Admin123!",
            defaultTenant.Id);
        context.Users.Add(adminUser);

        // Assign admin role to admin user
        var adminUserRole = new UserRole(
            Guid.Parse("00000000-0000-0000-0000-000000000030"),
            adminUser.Id,
            adminRole.Id);
        context.UserRoles.Add(adminUserRole);

        // Seed demo users for each tenant
        var demoUsers = new List<User>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000021"), "scorer@premier.com", "John", "Scorer", "Scorer123!", premierLeagueTenant.Id),
            new(Guid.Parse("00000000-0000-0000-0000-000000000022"), "scorer@nba.com", "Mike", "Scorer", "Scorer123!", nbaTenant.Id),
            new(Guid.Parse("00000000-0000-0000-0000-000000000023"), "scorer@athletics.com", "Sarah", "Scorer", "Scorer123!", athleticsTenant.Id),
            new(Guid.Parse("00000000-0000-0000-0000-000000000024"), "scorer@cricket.com", "David", "Scorer", "Scorer123!", cricketTenant.Id),
            new(Guid.Parse("00000000-0000-0000-0000-000000000025"), "scorer@golf.com", "Lisa", "Scorer", "Scorer123!", golfTenant.Id),
            new(Guid.Parse("00000000-0000-0000-0000-000000000026"), "scorer@ufc.com", "Tony", "Scorer", "Scorer123!", mmaTenant.Id),
        };
        context.Users.AddRange(demoUsers);

        // Assign scorer roles to demo users
        var scorerUserRoles = demoUsers.Select((u, i) => new UserRole(
            Guid.Parse($"00000000-0000-0000-0000-0000000000{31 + i}"),
            u.Id,
            scorerRole.Id)).ToList();
        context.UserRoles.AddRange(scorerUserRoles);

        // Seed basic permissions for roles
        var adminPermissions = new List<RolePermission>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000040"), adminRole.Id, "sports.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000041"), adminRole.Id, "sports.create"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000042"), adminRole.Id, "sports.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000043"), adminRole.Id, "competitions.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000044"), adminRole.Id, "competitions.create"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000045"), adminRole.Id, "competitions.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000046"), adminRole.Id, "contests.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000047"), adminRole.Id, "contests.create"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000048"), adminRole.Id, "contests.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000049"), adminRole.Id, "participants.read"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000004a"), adminRole.Id, "participants.create"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000004b"), adminRole.Id, "participants.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000004c"), adminRole.Id, "tenants.read"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000004d"), adminRole.Id, "tenants.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000004e"), adminRole.Id, "subscriptions.read"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000004f"), adminRole.Id, "subscriptions.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000050"), adminRole.Id, "users.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000051"), adminRole.Id, "users.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000052"), adminRole.Id, "roles.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000054"), adminRole.Id, "branding.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000055"), adminRole.Id, "branding.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000056"), adminRole.Id, "scoring.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000057"), adminRole.Id, "results.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000058"), adminRole.Id, "scoring.correct"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000059"), adminRole.Id, "results.submit"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000005a"), adminRole.Id, "results.approve"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000005b"), adminRole.Id, "reports.read"),
            new(Guid.Parse("00000000-0000-0000-0000-00000000005c"), adminRole.Id, "reports.export"),
        };
        context.RolePermissions.AddRange(adminPermissions);

        var scorerPermissions = new List<RolePermission>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000060"), scorerRole.Id, "sports.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000061"), scorerRole.Id, "competitions.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000062"), scorerRole.Id, "contests.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000063"), scorerRole.Id, "contests.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000064"), scorerRole.Id, "participants.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000065"), scorerRole.Id, "scoring.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000066"), scorerRole.Id, "results.manage"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000067"), scorerRole.Id, "results.submit"),
        };
        context.RolePermissions.AddRange(scorerPermissions);

        var userPermissions = new List<RolePermission>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000070"), userRole.Id, "sports.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000071"), userRole.Id, "competitions.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000072"), userRole.Id, "contests.read"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000073"), userRole.Id, "participants.read"),
        };
        context.RolePermissions.AddRange(userPermissions);

        // ============================================
        // SEED SPORTS
        // ============================================
        var footballSport = new Sport(Guid.Parse("00000000-0000-0000-0000-000000000100"), "FOOTBALL", "Football");
        footballSport.UpdateDetails("Football", "Association football/soccer", "⚽", new[] { "team" });
        
        var basketballSport = new Sport(Guid.Parse("00000000-0000-0000-0000-000000000101"), "BASKETBALL", "Basketball");
        basketballSport.UpdateDetails("Basketball", "Basketball (5v5)", "🏀", new[] { "team" });
        
        var golfSport = new Sport(Guid.Parse("00000000-0000-0000-0000-000000000102"), "GOLF", "Golf");
        golfSport.UpdateDetails("Golf", "Stroke play golf tournament", "⛳", new[] { "individual" });
        
        var athleticsSport = new Sport(Guid.Parse("00000000-0000-0000-0000-000000000103"), "ATHLETICS", "Athletics");
        athleticsSport.UpdateDetails("Athletics", "Track and field athletics", "🏃", new[] { "individual", "team" });
        
        var cricketSport = new Sport(Guid.Parse("00000000-0000-0000-0000-000000000104"), "CRICKET", "Cricket");
        cricketSport.UpdateDetails("Cricket", "T20 Cricket", "🏏", new[] { "team" });
        
        var mmaSport = new Sport(Guid.Parse("00000000-0000-0000-0000-000000000105"), "MMA", "Mixed Martial Arts");
        mmaSport.UpdateDetails("Mixed Martial Arts", "UFC-style MMA", "🥊", new[] { "individual" });
        
        var rugbySport = new Sport(Guid.Parse("00000000-0000-0000-0000-000000000106"), "RUGBY", "Rugby");
        rugbySport.UpdateDetails("Rugby", "Rugby Union", "🏉", new[] { "team" });

        context.Sports.AddRange(footballSport, basketballSport, golfSport, athleticsSport, cricketSport, mmaSport, rugbySport);

        // ============================================
        // SEED FOOTBALL TEAMS (Premier League)
        // ============================================
        var footballTeams = new List<Team>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000200"), premierLeagueTenant.Id, "Manchester City", "MCI"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000201"), premierLeagueTenant.Id, "Arsenal", "ARS"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000202"), premierLeagueTenant.Id, "Liverpool", "LIV"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000203"), premierLeagueTenant.Id, "Chelsea", "CHE"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000204"), premierLeagueTenant.Id, "Manchester United", "MUN"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000205"), premierLeagueTenant.Id, "Tottenham Hotspur", "TOT"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000206"), premierLeagueTenant.Id, "Newcastle United", "NEW"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000207"), premierLeagueTenant.Id, "Brighton & Hove Albion", "BHA"),
        };
        context.Set<Team>().AddRange(footballTeams);

        // ============================================
        // SEED BASKETBALL TEAMS (NBA)
        // ============================================
        var basketballTeams = new List<Team>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000210"), nbaTenant.Id, "Los Angeles Lakers", "LAL"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000211"), nbaTenant.Id, "Boston Celtics", "BOS"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000212"), nbaTenant.Id, "Golden State Warriors", "GSW"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000213"), nbaTenant.Id, "Miami Heat", "MIA"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000214"), nbaTenant.Id, "Brooklyn Nets", "BKN"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000215"), nbaTenant.Id, "Milwaukee Bucks", "MIL"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000216"), nbaTenant.Id, "Phoenix Suns", "PHX"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000217"), nbaTenant.Id, "Denver Nuggets", "DEN"),
        };
        context.Set<Team>().AddRange(basketballTeams);

        // ============================================
        // SEED GOLFERS
        // ============================================
        var golfers = new List<Athlete>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000220"), golfTenant.Id, "Scottie Scheffler", "Scheffler"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000221"), golfTenant.Id, "Rory McIlroy", "McIlroy"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000222"), golfTenant.Id, "Jon Rahm", "Rahm"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000223"), golfTenant.Id, "Brooks Koepka", "Koepka"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000224"), golfTenant.Id, "Collin Morikawa", "Morikawa"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000225"), golfTenant.Id, "Dustin Johnson", "Johnson"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000226"), golfTenant.Id, "Bryson DeChambeau", "DeChambeau"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000227"), golfTenant.Id, "Cameron Smith", "Smith"),
        };
        context.Set<Athlete>().AddRange(golfers);

        // ============================================
        // SEED ATHLETICS ATHLETES
        // ============================================
        var trackAthletes = new List<Athlete>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000230"), athleticsTenant.Id, "Noah Lyles", "Lyles"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000231"), athleticsTenant.Id, "Christian Coleman", "Coleman"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000232"), athleticsTenant.Id, "Fred Kerley", "Kerley"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000233"), athleticsTenant.Id, "Sha'Carri Richardson", "Richardson"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000234"), athleticsTenant.Id, "Elaine Thompson-Herah", "Thompson-Herah"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000235"), athleticsTenant.Id, "Sydney McLaughlin-Levrone", "McLaughlin-Levrone"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000236"), athleticsTenant.Id, "Armand Duplantis", "Duplantis"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000237"), athleticsTenant.Id, "Jakob Ingebrigtsen", "Ingebrigtsen"),
        };
        context.Set<Athlete>().AddRange(trackAthletes);

        // ============================================
        // SEED CRICKET TEAMS
        // ============================================
        var cricketTeams = new List<Team>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000240"), cricketTenant.Id, "India", "IND"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000241"), cricketTenant.Id, "Australia", "AUS"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000242"), cricketTenant.Id, "England", "ENG"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000243"), cricketTenant.Id, "Pakistan", "PAK"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000244"), cricketTenant.Id, "South Africa", "SA"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000245"), cricketTenant.Id, "New Zealand", "NZ"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000246"), cricketTenant.Id, "West Indies", "WI"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000247"), cricketTenant.Id, "Sri Lanka", "SL"),
        };
        context.Set<Team>().AddRange(cricketTeams);

        // ============================================
        // SEED MMA FIGHTERS
        // ============================================
        var mmaFighters = new List<Athlete>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000250"), mmaTenant.Id, "Islam Makhachev", "Makhachev"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000251"), mmaTenant.Id, "Alex Pereira", "Pereira"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000252"), mmaTenant.Id, "Leon Edwards", "Edwards"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000253"), mmaTenant.Id, "Sean O'Malley", "O'Malley"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000254"), mmaTenant.Id, "Ilia Topuria", "Topuria"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000255"), mmaTenant.Id, "Max Holloway", "Holloway"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000256"), mmaTenant.Id, "Charles Oliveira", "Oliveira"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000257"), mmaTenant.Id, "Belal Muhammad", "Muhammad"),
        };
        context.Set<Athlete>().AddRange(mmaFighters);

        // ============================================
        // SEED RUGBY TEAMS (Six Nations)
        // ============================================
        var rugbyTeams = new List<Team>
        {
            new(Guid.Parse("00000000-0000-0000-0000-000000000260"), rugbyTenant.Id, "Ireland", "IRL"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000261"), rugbyTenant.Id, "France", "FRA"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000262"), rugbyTenant.Id, "England", "ENG"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000263"), rugbyTenant.Id, "Wales", "WAL"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000264"), rugbyTenant.Id, "Scotland", "SCO"),
            new(Guid.Parse("00000000-0000-0000-0000-000000000265"), rugbyTenant.Id, "Italy", "ITA"),
        };
        context.Set<Team>().AddRange(rugbyTeams);

        // ============================================
        // SEED COMPETITIONS
        // ============================================
        var now = DateTime.UtcNow;
        
        // Premier League Competition
        var premierLeague = new Competition(
            Guid.Parse("00000000-0000-0000-0000-000000000300"),
            premierLeagueTenant.Id,
            "Premier League 2025-26",
            footballSport.Id);
        premierLeague.Publish();
        context.Competitions.Add(premierLeague);

        var plEdition = premierLeague.AddEdition("2025-26 Season", now.AddMonths(-6), now.AddMonths(6));
        plEdition.Open();
        
        var plGroupStage = plEdition.AddStage("Regular Season", StageType.League, 1);
        plGroupStage.Open();
        
        var plGroup = plGroupStage.AddGroup("Main League");

        // NBA Competition
        var nbaSeason = new Competition(
            Guid.Parse("00000000-0000-0000-0000-000000000301"),
            nbaTenant.Id,
            "NBA Season 2025-26",
            basketballSport.Id);
        nbaSeason.Publish();
        context.Competitions.Add(nbaSeason);

        var nbaEdition = nbaSeason.AddEdition("2025-26 Season", now.AddMonths(-4), now.AddMonths(4));
        nbaEdition.Open();
        
        var nbaGroupStage = nbaEdition.AddStage("Regular Season", StageType.League, 1);
        nbaGroupStage.Open();

        // Golf Tournament
        var mastersTournament = new Competition(
            Guid.Parse("00000000-0000-0000-0000-000000000302"),
            golfTenant.Id,
            "The Masters 2026",
            golfSport.Id);
        mastersTournament.Publish();
        context.Competitions.Add(mastersTournament);

        var mastersEdition = mastersTournament.AddEdition("2026", now.AddDays(-2), now.AddDays(2));
        mastersEdition.Open();
        
        var mastersStage = mastersEdition.AddStage("Tournament", StageType.Leaderboard, 1);
        mastersStage.Open();

        // Athletics Meet
        var worldChampionships = new Competition(
            Guid.Parse("00000000-0000-0000-0000-000000000303"),
            athleticsTenant.Id,
            "World Athletics Championships 2026",
            athleticsSport.Id);
        worldChampionships.Publish();
        context.Competitions.Add(worldChampionships);

        var athleticsEdition = worldChampionships.AddEdition("2026", now.AddDays(-1), now.AddDays(5));
        athleticsEdition.Open();
        
        var athleticsStage = athleticsEdition.AddStage("Track Events", StageType.Heat, 1);
        athleticsStage.Open();

        // Cricket Tournament
        var t20WorldCup = new Competition(
            Guid.Parse("00000000-0000-0000-0000-000000000304"),
            cricketTenant.Id,
            "T20 World Cup 2026",
            cricketSport.Id);
        t20WorldCup.Publish();
        context.Competitions.Add(t20WorldCup);

        var t20Edition = t20WorldCup.AddEdition("2026", now.AddDays(-10), now.AddDays(30));
        t20Edition.Open();
        
        var t20GroupStage = t20Edition.AddStage("Group Stage", StageType.Group, 1);
        t20GroupStage.Open();
        
        var t20Group = t20GroupStage.AddGroup("Group A");

        // UFC Event
        var ufcEvent = new Competition(
            Guid.Parse("00000000-0000-0000-0000-000000000305"),
            mmaTenant.Id,
            "UFC 315: Pereira vs Makhachev",
            mmaSport.Id);
        ufcEvent.Publish();
        context.Competitions.Add(ufcEvent);

        var ufcEdition = ufcEvent.AddEdition("315", now, now.AddDays(1));
        ufcEdition.Open();
        
        var ufcStage = ufcEdition.AddStage("Main Card", StageType.FightCard, 1);
        ufcStage.Open();

        // Six Nations Rugby Competition
        var sixNations = new Competition(
            Guid.Parse("00000000-0000-0000-0000-000000000306"),
            rugbyTenant.Id,
            "Six Nations 2026",
            rugbySport.Id);
        sixNations.Publish();
        context.Competitions.Add(sixNations);

        var sixNationsEdition = sixNations.AddEdition("2026", now.AddMonths(-2), now.AddMonths(2));
        sixNationsEdition.Open();
        
        var sixNationsStage = sixNationsEdition.AddStage("Championship", StageType.League, 1);
        sixNationsStage.Open();

        await context.SaveChangesAsync();
        await EnsurePublishedRulesetsAsync(context);

        // ============================================
        // SEED CONTESTS FOR ALL COMPETITIONS
        // ============================================
        
        // Generate Round-Robin contests for Premier League (8 teams = 28 matches)
        var plContests = GenerateRoundRobinContests(
            plGroupStage.Id,
            plGroup.Id,
            premierLeagueTenant.Id,
            footballSport.Id,
            footballTeams,
            "Matchday {0}",
            1);
        ScheduleContests(
            plContests,
            "Premier League venue TBC",
            now.AddDays(1),
            7);
        context.Contests.AddRange(plContests);

        // Generate Round-Robin contests for NBA (8 teams = 28 matches)
        var nbaContests = GenerateRoundRobinContests(
            nbaGroupStage.Id,
            null,
            nbaTenant.Id,
            basketballSport.Id,
            basketballTeams,
            "Game {0}",
            1001);
        ScheduleContests(
            nbaContests,
            "NBA venue TBC",
            now.AddDays(1),
            2);
        context.Contests.AddRange(nbaContests);

        // Generate leaderboard entries for Golf Masters
        var golfContests = GenerateLeaderboardEntries(
            mastersStage.Id,
            golfTenant.Id,
            golfSport.Id,
            golfers,
            2001);
        context.Contests.AddRange(golfContests);

        // Generate heat entries for Athletics
        var athleticsContests = GenerateHeatEntries(
            athleticsStage.Id,
            athleticsTenant.Id,
            athleticsSport.Id,
            trackAthletes,
            3001);
        context.Contests.AddRange(athleticsContests);

        // Generate Round-Robin contests for Cricket T20 (8 teams = 28 matches)
        var cricketContests = GenerateRoundRobinContests(
            t20GroupStage.Id,
            t20Group.Id,
            cricketTenant.Id,
            cricketSport.Id,
            cricketTeams,
            "Match {0}",
            4001);
        ScheduleContests(
            cricketContests,
            "T20 venue TBC",
            now.AddDays(1),
            2);
        context.Contests.AddRange(cricketContests);

        // Generate fight card for UFC
        var ufcContests = GenerateFightCard(
            ufcStage.Id,
            mmaTenant.Id,
            mmaSport.Id,
            mmaFighters,
            5001);
        context.Contests.AddRange(ufcContests);

        // Generate Round-Robin contests for Six Nations Rugby (6 teams = 15 matches)
        var rugbyContests = GenerateRoundRobinContests(
            sixNationsStage.Id,
            null,
            rugbyTenant.Id,
            rugbySport.Id,
            rugbyTeams,
            "Match {0}",
            6001);
        var rugbyStart = new DateTime(2026, 2, 5, 20, 0, 0, DateTimeKind.Utc);
        ScheduleContests(rugbyContests, "Six Nations venue TBC", rugbyStart, 3);
        context.Contests.AddRange(rugbyContests);

        await context.SaveChangesAsync();
        await DemoSeedData.EnsureAsync(context);
    }

    internal static async Task EnsurePublishedRulesetsAsync(SportsPlatformDbContext context)
    {
        var sports = await context.Sports.Include(sport => sport.Versions).ToArrayAsync();
        foreach (var sport in sports)
        {
            var version = sport.Versions.FirstOrDefault(candidate =>
                candidate.IsPublished && candidate.CreatedBy == "system-seed");
            var isNew = version is null && !sport.Versions.Any(candidate => candidate.IsPublished);
            if (!isNew) continue;
            version = sport.CreateNewVersion("Platform baseline ruleset", "system-seed");
            var configuration = sport.Code switch
            {
                "FOOTBALL" => ("TeamVsTeam", "Team", FootballRulesetService.GetSegmentDefinitions(), FootballRulesetService.GetEventDefinitions(), FootballRulesetService.GetMetricDefinitions(), FootballRulesetService.GetResultMethods(), FootballRulesetService.GetRankingRules(), FootballRulesetService.GetDisplayConfiguration(), FootballRulesetService.GetValidationConfiguration()),
                "BASKETBALL" => ("TeamVsTeam", "Team", BasketballRulesetService.GetSegmentDefinitions(), BasketballRulesetService.GetEventDefinitions(), BasketballRulesetService.GetMetricDefinitions(), BasketballRulesetService.GetResultMethods(), BasketballRulesetService.GetRankingRules(), BasketballRulesetService.GetDisplayConfiguration(), BasketballRulesetService.GetValidationConfiguration()),
                "GOLF" => ("Individual", "Athlete", GolfRulesetService.GetSegmentDefinitions(), GolfRulesetService.GetEventDefinitions(), GolfRulesetService.GetMetricDefinitions(), GolfRulesetService.GetResultMethods(), GolfRulesetService.GetRankingRules(), GolfRulesetService.GetDisplayConfiguration(), GolfRulesetService.GetValidationConfiguration()),
                "ATHLETICS" => ("Individual", "Athlete", AthleticsRulesetService.GetSegmentDefinitions(), AthleticsRulesetService.GetEventDefinitions(), AthleticsRulesetService.GetMetricDefinitions(), AthleticsRulesetService.GetResultMethods(), AthleticsRulesetService.GetRankingRules(), AthleticsRulesetService.GetDisplayConfiguration(), AthleticsRulesetService.GetValidationConfiguration()),
                "CRICKET" => ("Team", "Team", CricketT20RulesetService.GetSegmentDefinitions(), CricketT20RulesetService.GetEventDefinitions(), CricketT20RulesetService.GetMetricDefinitions(), CricketT20RulesetService.GetResultMethods(), CricketT20RulesetService.GetRankingRules(), CricketT20RulesetService.GetDisplayConfiguration(), CricketT20RulesetService.GetValidationConfiguration()),
                "MMA" => ("FighterVsFighter", "Athlete", CombatSportsRulesetService.GetSegmentDefinitions(), CombatSportsRulesetService.GetEventDefinitions(), CombatSportsRulesetService.GetMetricDefinitions(), CombatSportsRulesetService.GetResultMethods(), CombatSportsRulesetService.GetRankingRules(), CombatSportsRulesetService.GetDisplayConfiguration(), CombatSportsRulesetService.GetValidationConfiguration()),
                "RUGBY" => ("TeamVsTeam", "Team", RugbyRulesetService.GetSegmentDefinitions(), RugbyRulesetService.GetEventDefinitions(), RugbyRulesetService.GetMetricDefinitions(), RugbyRulesetService.GetResultMethods(), RugbyRulesetService.GetRankingRules(), RugbyRulesetService.GetDisplayConfiguration(), RugbyRulesetService.GetValidationConfiguration()),
                _ => ("TeamVsTeam", "Team", "[]", "[]", "[]", "[]", "[]", "{}", "{}")
            };
            version.ConfigureContestShape(
                configuration.Item1, configuration.Item2, configuration.Item3,
                configuration.Item4, configuration.Item5, configuration.Item6,
                configuration.Item7, configuration.Item8, configuration.Item9);
            version.Publish();
            if (isNew) context.Entry(version).State = EntityState.Added;
        }

        var rugby = sports.SingleOrDefault(sport => sport.Code == "RUGBY");
        if (rugby is not null)
        {
            EnsureRugbyVariant(rugby, RugbyVariants.Fifteens, "Rugby Union Fifteens Rules v1.0", context);
            EnsureRugbyVariant(rugby, RugbyVariants.Sevens, "Rugby Sevens Rules v1.0", context);
        }
        await context.SaveChangesAsync();
    }

    private static void EnsureRugbyVariant(
        Sport rugby,
        string variant,
        string name,
        SportsPlatformDbContext context)
    {
        if (rugby.Versions.Any(version =>
                version.IsPublished &&
                RugbyVariants.FromValidationConfiguration(version.ValidationConfiguration) == variant))
        {
            return;
        }

        var version = rugby.CreateNewVersion(name, "system-seed");
        version.ConfigureContestShape(
            "TeamVsTeam",
            "Team",
            RugbyRulesetService.GetSegmentDefinitions(variant),
            RugbyRulesetService.GetEventDefinitions(variant),
            RugbyRulesetService.GetMetricDefinitions(),
            RugbyRulesetService.GetResultMethods(variant),
            RugbyRulesetService.GetRankingRules(),
            RugbyRulesetService.GetDisplayConfiguration(variant),
            RugbyRulesetService.GetValidationConfiguration(variant));
        version.Publish();
        context.Entry(version).State = EntityState.Added;
    }

    /// <summary>
    /// Generates round-robin contests for team sports.
    /// </summary>
    private static List<Contest> GenerateRoundRobinContests(
        Guid stageId,
        Guid? groupId,
        Guid tenantId,
        Guid sportId,
        List<Team> teams,
        string matchNameFormat,
        int startIndex)
    {
        var contests = new List<Contest>();
        var n = teams.Count;
        var matchIndex = startIndex;

        // Generate single round-robin (each team plays every other team once)
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var contest = new Contest(
                    Guid.NewGuid(),
                    tenantId,
                    sportId,
                    "RoundRobin");
                
                // Set StageId using reflection since it's not a constructor parameter
                var stageIdProperty = typeof(Contest).GetProperty("StageId");
                stageIdProperty?.SetValue(contest, stageId);
                
                if (groupId.HasValue)
                {
                    var groupIdProperty = typeof(Contest).GetProperty("GroupId");
                    groupIdProperty?.SetValue(contest, groupId.Value);
                }

                // Set sort order
                var sortOrderProperty = typeof(Contest).GetProperty("SortOrder");
                sortOrderProperty?.SetValue(contest, matchIndex);

                // Add participants
                contest.AddParticipant(teams[i].Id, "Team", 1);
                contest.AddParticipant(teams[j].Id, "Team", 2);

                AddTeamSportSegments(contest, sportId);

                contests.Add(contest);
                matchIndex++;
            }
        }

        return contests;
    }

    private static void ScheduleContests(
        IReadOnlyList<Contest> contests,
        string venue,
        DateTime firstStart,
        int daysBetweenContests)
    {
        for (var index = 0; index < contests.Count; index++)
            contests[index].Schedule(venue, firstStart.AddDays(index * daysBetweenContests));
    }

    /// <summary>
    /// Generates leaderboard entries for golf tournaments.
    /// </summary>
    private static List<Contest> GenerateLeaderboardEntries(
        Guid stageId,
        Guid tenantId,
        Guid sportId,
        List<Athlete> golfers,
        int startIndex)
    {
        var contests = new List<Contest>();

        // Create a single contest for the tournament leaderboard
        var contest = new Contest(
            Guid.NewGuid(),
            tenantId,
            sportId,
            "Leaderboard");
        
        var stageIdProperty = typeof(Contest).GetProperty("StageId");
        stageIdProperty?.SetValue(contest, stageId);

        var sortOrderProperty = typeof(Contest).GetProperty("SortOrder");
        sortOrderProperty?.SetValue(contest, startIndex);

        // Add each golfer as a participant
        for (var i = 0; i < golfers.Count; i++)
        {
            contest.AddParticipant(golfers[i].Id, "Athlete", i + 1);
        }

        // Add segments for each round
        for (var roundNumber = 1; roundNumber <= 4; roundNumber++)
        {
            var round = contest.AddSegment($"Round {roundNumber}", "Round", roundNumber);
            for (var hole = 1; hole <= 18; hole++)
            {
                round.AddNestedSegment($"Hole {hole}", "Hole", hole);
            }
        }

        contests.Add(contest);

        return contests;
    }

    private static void AddTeamSportSegments(Contest contest, Guid sportId)
    {
        var basketball = Guid.Parse("00000000-0000-0000-0000-000000000101");
        var cricket = Guid.Parse("00000000-0000-0000-0000-000000000104");
        if (sportId == basketball)
        {
            for (var quarter = 1; quarter <= 4; quarter++)
            {
                contest.AddSegment($"Quarter {quarter}", "Quarter", quarter);
            }
            contest.AddSegment(
                "Overtime",
                "Overtime",
                5,
                isOptional: true,
                activationCondition: "tie_after_regulation");
            return;
        }

        if (sportId == cricket)
        {
            for (var inningsNumber = 1; inningsNumber <= 2; inningsNumber++)
            {
                var innings = contest.AddSegment(
                    $"Innings {inningsNumber}",
                    "Innings",
                    inningsNumber);
                for (var over = 1; over <= 20; over++)
                {
                    innings.AddNestedSegment($"Over {over}", "Over", over);
                }
            }
            return;
        }

        contest.AddSegment("First Half", "Half", 1);
        contest.AddSegment("Second Half", "Half", 2);
        contest.AddSegment(
            "Extra Time",
            "Overtime",
            3,
            isOptional: true,
            activationCondition: "tie_after_regulation");
    }

    /// <summary>
    /// Generates heat entries for athletics events.
    /// </summary>
    private static List<Contest> GenerateHeatEntries(
        Guid stageId,
        Guid tenantId,
        Guid sportId,
        List<Athlete> athletes,
        int startIndex)
    {
        var contests = new List<Contest>();

        // Create two heats of 4 athletes each
        var heat1 = new Contest(
            Guid.NewGuid(),
            tenantId,
            sportId,
            "Heat");
        
        var stageIdProperty = typeof(Contest).GetProperty("StageId");
        stageIdProperty?.SetValue(heat1, stageId);

        var sortOrderProperty = typeof(Contest).GetProperty("SortOrder");
        sortOrderProperty?.SetValue(heat1, startIndex);

        // Add first 4 athletes to heat 1
        for (var i = 0; i < 4; i++)
        {
            heat1.AddParticipant(athletes[i].Id, "Athlete", i + 1);
        }
        heat1.AddSegment("Final", "Segment", 1);
        contests.Add(heat1);

        var heat2 = new Contest(
            Guid.NewGuid(),
            tenantId,
            sportId,
            "Heat");
        
        stageIdProperty?.SetValue(heat2, stageId);
        sortOrderProperty?.SetValue(heat2, startIndex + 1);

        // Add next 4 athletes to heat 2
        for (var i = 4; i < 8; i++)
        {
            heat2.AddParticipant(athletes[i].Id, "Athlete", i - 3);
        }
        heat2.AddSegment("Final", "Segment", 1);
        contests.Add(heat2);

        return contests;
    }

    /// <summary>
    /// Generates fight card for MMA events.
    /// </summary>
    private static List<Contest> GenerateFightCard(
        Guid stageId,
        Guid tenantId,
        Guid sportId,
        List<Athlete> fighters,
        int startIndex)
    {
        var contests = new List<Contest>();

        // Create 4 fights (preliminary and main card)
        var fightPairings = new List<(int, int)>
        {
            (0, 1), // Fight 1: Fighter 1 vs Fighter 2
            (2, 3), // Fight 2: Fighter 3 vs Fighter 4
            (4, 5), // Fight 3: Fighter 5 vs Fighter 6
            (6, 7), // Fight 4: Fighter 7 vs Fighter 8 (Main Event)
        };

        for (var i = 0; i < fightPairings.Count; i++)
        {
            var (fighter1Idx, fighter2Idx) = fightPairings[i];
            var isMainEvent = i == fightPairings.Count - 1;

            var fight = new Contest(
                Guid.NewGuid(),
                tenantId,
                sportId,
                "Fight");
            
            var stageIdProperty = typeof(Contest).GetProperty("StageId");
            stageIdProperty?.SetValue(fight, stageId);

            var sortOrderProperty = typeof(Contest).GetProperty("SortOrder");
            sortOrderProperty?.SetValue(fight, startIndex + i);

            // Add fighters
            fight.AddParticipant(fighters[fighter1Idx].Id, "Athlete", 1);
            fight.AddParticipant(fighters[fighter2Idx].Id, "Athlete", 2);

            // Add rounds as segments (5 rounds for main event, 3 for others)
            var numRounds = isMainEvent ? 5 : 3;
            for (var round = 1; round <= numRounds; round++)
            {
                fight.AddSegment($"Round {round}", "Round", round);
            }

            contests.Add(fight);
        }

        return contests;
    }
}
