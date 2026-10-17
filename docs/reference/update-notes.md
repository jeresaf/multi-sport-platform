# Update to sports platform (owner's notes, verbatim)

```text
Update to sports platform:
We will have 2 main UI's
- Admin Dashboard
- Public UI: This will be both on web and mobile. It will take into consideration different form factors; tablet, mobile and desktop.

Below are the separations I have found based on the previous work done:

1. Player Manager
A player is an individual that participates in a sport. Generally all players have non sport related biodata such as first name, middle name, last name, other names, gender, date of birth, height, weight, nationality, photo, etc.
Duplicate players within the same tenant should be avoided at all costs.
Most events in a game/contest are attached to a specific player(s). Players are the basis of the system. 
The player manager handles creation, update, deleting and listing players.

2. Team Manager
A team is an organization of staff and players that participate in a sport directly or indirectly. The staff are indirectly involved and should be treated as a separate entity. They help the players get ready to participate before, during and after games/contests. 
- A full staff management system should be added into the team manager since staff are an integral part of a team and players are handled using the player manager.
- A roster manager should also be added so the players can be on more than one roster for more than one competition for the same team.
- A transfer manager can also be added which handles player transfers. This is only applicable for some sports and some competitions that happen on a continuous basis.
Teams also have non sports related fields such as name, logo, badge, motto and sponsors.
The Ui should allow team creation then addition of staff and players to the team. After that rosters can be created.

3. Competition Manager
A competition is an event with a specific sport and ruleset that teams and players can participate in. It therefore relies on:
- Sports Manager
- Competition Format Manager
A competition has a name and occurs between a specific set of dates. 
Creation of a competition can trigger automatic scheduling of games or just fall back to manual schedule creation.

4. Schedule Manager
A schedule is a set of games in a competition where teams or players participate against each other. The schedule manager maintains the dates and times the games are supposed to happen. The fixtures can be created automatically or manually. Direct editing of approved fixtures should not be allowed. The manager keeps track of game statuses; not started, about to start, started, break, ended. 
The manager should also handle sending reminders and notifications of game statuses.
The UI should be dynamic enough to display the competition within its ruleset such as brackets or a table.

5. Live Score Manager
This handles scoring games. It keeps track of events in games as they are going on. This is where games are started and ended.
The events for the live games are obtained from the sports manager and competition format manager.
The UI should be dynamic enough to allow easy scoring of games. 

6. Backlog Manager
The backlog manager allows the entry of past data. It will work hand in hand with the player manager, team manager, competition manager, schedule manager, live score manager, sports manager, competition format manager to provide a UI that can facilitate easy entry of data. It should also be able to provide a template in json or xls that can be filled out of the system and later imported into the system.

7. History/Statistics Manager
This handles all data within the system for UI display. It is the primary source of information for the UI. These includes players, teams, competitions, schedules, scores and game live score events.
It will also be used to generate statistics that may be required from the UI.

8. Subscription/Feature Manager
All tenants have to subscribe to use features. These are determined through the available actions in the system. Each action should be documented and added to the subscription model. Initially we should have 2 tiers. The specific system actions will be generated as the system is being built therefore we need to keep track of all system actions as we expand the system. This should limit tenant actions.

9. White label/Branding Manager
This allows a tenant to have their own brand identity within the system (Iconography, typography, themes, logos). Another manager can be used to extend the functionality of this manager;
- Sponsor Manager: It handles all information about a tenant's sponsors including the required images for the placement positions for the ads on the UI. It can also allow total removal of sponsors from the page or using google AdSense.

10. Tenant Manager
This handles information separation between distinct customers of the system. Its functionality still needs some more precise defining. The UI should generally be focused on dashboards and reports.

11. Sports Manager
This handles creation, update and deleting of sports and their specific rulesets. It will replace hardcoding of sports within the system.

12. Competition Format Manager
This handles the creation, update, deleting of competition formats and their rules such as knockout, round robin etc. It will replace hard coding of competition formats in the system.

13. Constant Manager
This is a system wide manager that handles all constants used in the system.

14. Seed Manager
This is a system wide manager that handles all seed data required for startup of the system and efficient first use on an empty database as well as for providing test data.

15. Audit Manager
This handles all actions both user and system that occur throughout the platform when it is in use or idle.

16. Data Integrity Manager
This is used to create rules for data integrity in the platform such as soft delete, general database table structure, naming etc.

17. Analytics Manager
This is used to handle capture and reporting of usage statistics throughout the platform




Given all the above discoveries, I broke down the system further with a few differences:
I want to separate the platform into fully functional separate systems. 
First I want to declare the managers that will be present throughout all systems in the platform.
1. Constant Manager
2. Seed Manager
3. Audit Manager
4. Data Integrity Manager
5. Analytics Manager

With those managers as a backbone of many of the system actions, we can start building the core systems that will make up the platform. We will start with the helper systems that will set rules for sports and competition formats.
1. Sports Management System
2. Competition Format Management System

For the next part of the platform, it should have the additional managers below that would make the system more enterprise level.
1. Tenant Manager
2. Subscription/Feature Manager
3. White label/Branding Manager
4. Sponsor Manager

We can now start building the systems that will generally be used by customers.
1. Player Management System
2. Team Management System: with Staff Manager, Roster Manager and Transfer Manager
3. Competition Management System

For the next 2 systems, I am not sure whether they should be standalone or integrated as helpers to expand the above 3 systems.
1. Schedule Management System
2. Score Management System

The last 2 managers interact with the above 5 systems and I am not sure whether to be standalone or not
1. History/Statistics Manager
2. Backlog Manager

The goal is for each separate system to expand into national register wherever possible. 
```
