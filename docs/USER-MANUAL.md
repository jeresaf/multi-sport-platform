# User Manual
# Multi-Sport Platform

**Version:** 1.0  
**Date:** July 2026  
**Status:** Final

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Getting Started](#2-getting-started)
3. [Admin Web Application](#3-admin-web-application)
4. [Public Web Application](#4-public-web-application)
5. [Mobile Application](#5-mobile-application)
6. [Competition Management](#6-competition-management)
7. [Live Scoring](#7-live-scoring)
8. [Reports and Analytics](#8-reports-and-analytics)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Introduction

### 1.1 About This Manual
This user manual provides comprehensive instructions for using the Multi-Sport Platform. It covers all user roles: Platform Admins, Tenant Admins, Competition Managers, Officials/Scorers, and Spectators.

### 1.2 System Overview
The Multi-Sport Platform is a comprehensive sports management system that supports:
- **7 Sports**: Football, Basketball, Golf, MMA/Boxing, Athletics, Cricket T20
- **Multiple Competition Formats**: Round-robin, Knockout, Group+Knockout, and more
- **Real-time Scoring**: Live event submission with instant updates
- **Offline Support**: Mobile scoring without internet connectivity
- **Multi-Tenant**: Multiple organizations can use the platform independently

### 1.3 User Roles

| Role | Description | Access Level |
|------|-------------|--------------|
| Platform Admin | Manages all tenants and global settings | Full system access |
| Tenant Admin | Manages their organization's competitions and users | Organization-wide |
| Competition Manager | Creates and manages competitions | Assigned competitions |
| Official/Scorer | Records live events during contests | Assigned contests |
| Spectator | Views live scores and results | Public read-only |

---

## 2. Getting Started

### 2.1 Accessing the Platform

#### Admin Web Application
1. Navigate to `https://admin.platform.com`
2. Enter your credentials
3. Click "Sign In"

#### Public Web Application
1. Navigate to `https://{tenant}.platform.com`
2. Browse competitions and live scores

#### Mobile Application
1. Download from App Store (iOS) or Play Store (Android)
2. Open the app
3. Enter your tenant code or select from list
4. Sign in with your credentials

### 2.2 First-Time Setup

#### For Platform Admins
1. Access the admin dashboard
2. Navigate to **Tenants** → **Add Tenant**
3. Enter tenant details (name, slug, contact)
4. Configure subscription plan
5. Set up branding (colors, logo)
6. Create initial admin user for the tenant

#### For Tenant Admins
1. Access your tenant's admin dashboard
2. Navigate to **Settings** → **Branding**
3. Upload your organization's logo
4. Set primary and secondary colors
5. Configure feature entitlements

### 2.3 Navigation Overview

#### Admin Dashboard Layout
```
┌─────────────────────────────────────────────────────────────┐
│  [Logo]  Dashboard  Competitions  Participants  Settings   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │
│   │ Live        │  │ Upcoming    │  │ Completed   │       │
│   │ Contests    │  │ Contests    │  │ Today       │       │
│   │    12       │  │    28       │  │    45       │       │
│   └─────────────┘  └─────────────┘  └─────────────┘       │
│                                                             │
│   Recent Activity                                           │
│   ─────────────────────────────────────────────────────     │
│   • Premier League: Manchester vs Chelsea - LIVE             │
│   • NBA Season: Lakers vs Celtics - Starting in 30 min      │
│   • Masters 2026: Round 3 in progress                       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Admin Web Application

### 3.1 Dashboard

The dashboard provides an overview of:
- **Live Contests**: Currently active contests
- **Upcoming Contests**: Scheduled contests
- **Today's Results**: Completed contests
- **Quick Actions**: Common tasks
- **Recent Activity**: System events

### 3.2 Sports Catalogue

#### Managing the Sports Catalogue

1. Open **Sports** in the admin sidebar.
2. Platform administrators can create a sport with a unique code, display name,
   description, icon, and comma-separated participant shapes such as `team`,
   `individual`, or `pair`.
3. Use **Edit** to change catalogue details or active status. A sport must be
   disabled for every tenant before it can be made inactive platform-wide.
4. Select a tenant and use **Enabled for tenant** to control which active sports
   it may use. Tenant owners see their own tenant and can make the same
   selection within their subscription limit.
5. Check the package badge and definition counts before creating competitions.

#### Managing Definition Versions

1. On a sport card, select **Versions**.
2. Create a blank draft for a new ruleset, or select a baseline and choose
   **Clone selected version** to copy every contest, segment, event, metric,
   result, ranking, display, and validation definition.
3. Enter the contest shape and participant type. Use the structured **Metric
   definitions** editor to add a code, name, one of the ten supported data
   types, unit, precision, ranking direction, and type-specific validation.
   Enumeration metrics require allowed values, while composite scores require
   ordered component names. **Advanced metric JSON** remains available for
   additional sport-package metadata. Other collection editors require JSON
   arrays; display and validation editors require JSON objects.
4. Select **Save draft**. Metric values and definitions are checked for valid
   types, ranges, precision, units, patterns, enumeration choices, composite
   components, and ranking direction. Errors do not change the saved definition.
5. Review the draft and select **Publish permanently**. Confirm carefully:
   published definitions cannot be edited. Clone a published version when
   future rules need to change.

New competitions automatically bind to the latest published definition for
their sport. Existing competitions retain their exact pinned version even when
a newer definition is published.

Competition creation accepts only sports enabled for the selected tenant. If a
toggle is rejected, review the tenant's **Subscription** page for its sport
entitlement and `max_sports` usage limit.

### 3.3 Competition Management

#### Creating a New Competition
1. Navigate to **Competitions** → **Create New**
2. **Step 1 - Basic Info**
   - Select sport
   - Enter competition name
   - Select format (round-robin, knockout, etc.)
   - Set date range
3. **Step 2 - Participants**
   - Add teams/athletes
   - Set seeds (optional)
   - Import from CSV (optional)
4. **Step 3 - Structure**
   - Configure groups/stages
   - Set advancement rules
   - Configure scoring rules
5. **Step 4 - Schedule**
   - Add venues
   - Set contest times
   - Auto-generate schedule
6. **Step 5 - Review & Publish**
   - Review all settings
   - Publish competition

#### Managing Competitions
- **Edit**: Modify competition details
- **Clone**: Copy competition structure
- **Archive**: Hide from active list
- **Delete**: Remove competition (with confirmation)
- **Export**: Download competition data

#### Managing the hierarchy

1. On **Competitions**, choose **Manage hierarchy** for a competition.
2. Add one or more editions or seasons with their start dates.
3. Add ordered stages to an edition. Supported stage types include groups,
   knockout, combined groups and knockout, league, leaderboard, heat, and
   fight card.
4. Select a stage and add groups, rounds, or both.
5. Create an individual contest manually and optionally place it in both a
   group and round. Generated fixtures from the playoff and sport workspaces
   appear in this same hierarchy.

When creating a manual contest, select one of the seven universal shapes:
head-to-head team, head-to-head individual, multi-participant ranked, race,
heat, best-of series, or fight-card bout. Sport workspaces add their own
segments, events, metrics, officials, and results to this same contest model;
they do not create incompatible sport-specific contest records.

The server rejects invalid date ranges, blank names, non-positive ordering,
and group or round IDs from another stage or competition.

### 3.4 Participant Management

The competition setup wizard uses one workflow for team and individual
entrants. On the **Participants** step, select Team, Athlete, Pair, Relay team,
Crew, Club, School, Country, or Composite team, then enter unique names. The
wizard suggests Team for team sports and Athlete for Golf, Athletics, and
combat sports; you may change that selection.

#### Adding Athletes
1. Navigate to **Participants** → **Athletes**
2. Click **Add Athlete**
3. Fill in details:
   - Name (required)
   - Date of birth
   - Nationality
   - Sport(s)
   - Photo
   - Contact info
4. Click **Save**

#### Adding Teams
1. Navigate to **Participants** → **Teams**
2. Click **Add Team**
3. Fill in details:
   - Team name (required)
   - Abbreviation
   - Home venue
   - Logo
   - Sport(s)
4. Click **Save**

#### Managing Rosters
1. Select a team
2. Navigate to **Roster** tab
3. Click **Add Player**
4. Select athlete from list
5. Set jersey number and position
6. Set status (active/inactive)

### 3.5 Registration Management

#### Creating Registration Forms
1. Navigate to **Registrations** → **Forms**
2. Click **Create Form**
3. Add fields:
   - Text fields
   - Number fields
   - Date fields
   - Dropdowns
   - File uploads
4. Set field requirements
5. Save form

#### Reviewing Registrations
1. Navigate to **Registrations** → **Submissions**
2. Filter by status (Pending, Approved, Rejected)
3. Click on submission to review
4. Approve, reject, or request changes

### 3.6 Tenant Administration

#### Managing the Tenant
1. Navigate to **Tenants**.
2. Platform operators can create tenants and activate, suspend, or archive the
   selected tenant. Tenant administrators see only their own tenant.
3. Update the tenant profile and country, timezone, currency, and default
   language where permitted.
4. Add, edit, or remove operational contacts. Selecting a primary contact
   automatically clears the previous primary selection.

Suspended and archived tenants remain readable, but protected changes are
blocked until a platform operator reactivates a suspended tenant. Archive is a
terminal state.

#### Managing Users
1. Navigate to **Settings** → **Users**
2. View user list
3. Actions:
   - **Add User**: Create new user account
   - **Edit**: Modify user details
   - **Reset Password**: Send password reset
   - **Deactivate**: Disable account

#### Configuring Branding
1. Navigate to **Settings** → **Branding**
2. Upload a PNG, JPEG, or WebP logo (maximum 2 MB), or provide a trusted HTTPS
   logo URL
3. Set primary color (hex code)
4. Set secondary and accent colors
5. Add the public contact email, phone, and official website
6. Preview changes
7. Save

The public slug is shown for reference but cannot be changed from branding.
The public web shell and Flutter application reload the selected tenant's
identity, logo, and theme from the public API. Use **Sponsors** to configure
date-controlled sponsor placeholders for public and operational surfaces.

---

## 4. Public Web Application

### 4.1 Homepage

The public homepage displays:
- **Hero Section**: Featured competition or event
- **Live Now**: Currently active contests
- **Upcoming**: Next scheduled contests
- **Sport Categories**: Browse by sport
- **Recent Results**: Latest completed contests

### 4.2 Competition Listing

#### Browsing Competitions
1. Navigate to **Competitions**
2. Filter by:
   - Sport
   - Status (Live, Upcoming, Completed)
   - Date range
3. Sort by:
   - Name
   - Start date
   - Popularity
4. Click competition to view details

#### Competition Details
- Overview and description
- Participants/teams
- Schedule
- Standings/leaderboard
- Results

### 4.3 Contest Page

#### Viewing Live Contest
- Score display (real-time)
- Event timeline
- Statistics
- Commentary (if available)

#### Viewing Results
- Final score
- Match statistics
- Event summary
- Player/team stats

### 4.4 Participant Profiles

#### Team Profile
- Team information
- Roster
- Upcoming matches
- Recent results
- Statistics

#### Athlete Profile
- Personal information
- Statistics
- Career highlights
- Recent performances

#### Sport-specific attributes

A participant may have a separate JSON-object profile for each sport—for
example a rugby position and an athletics personal best. Updating one sport's
attributes does not overwrite another sport profile. Contest entry uses the
participant's stored type and rejects mismatched or disallowed types.

### 4.5 Search

1. Click search icon
2. Enter search term
3. Results show:
   - Competitions
   - Participants
   - Recent results
4. Click result to navigate

---

## 5. Mobile Application

### 5.1 Installation and Setup

#### iOS
1. Open App Store
2. Search "Multi-Sport Platform"
3. Tap **Get**
4. Open app after installation

#### Android
1. Open Play Store
2. Search "Multi-Sport Platform"
3. Tap **Install**
4. Open app after installation

### 5.2 Initial Setup

1. **Select Tenant**
   - Enter tenant code, OR
   - Select from list
2. **Sign In**
   - Enter email and password
   - Tap "Sign In"
3. **Grant Permissions**
   - Notifications (recommended)
   - Location (optional)

### 5.3 Supporter Mode

#### Home Screen
- Live contests
- Upcoming contests
- Recent results
- Quick access to followed teams

#### Competition List
1. Tap **Competitions**
2. Browse or search
3. Tap to view details
4. Follow competitions for updates

#### Contest Details
1. Select a contest
2. View:
   - Live score
   - Event timeline
   - Statistics
   - Participant info
3. Tap **Follow** for notifications

#### Live Scoring
1. Select live contest
2. Watch real-time updates
3. View event details
4. Access statistics

### 5.4 Scorer Mode

#### Starting a Scoring Session
1. Tap **Score**
2. Select competition
3. Select contest
4. Choose your role (Main Scorer, Assistant)
5. Tap **Start Scoring**

#### Football Scoring
```
┌─────────────────────────────────────────────┐
│  TEAM A          2 - 1          TEAM B     │
│                                             │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐       │
│  │  GOAL  │  │  CARD   │  │   SUB   │       │
│  └─────────┘  └─────────┘  └─────────┘       │
│                                             │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐       │
│  │  FOUL  │  │ PENALTY │  │  TIMER  │       │
│  └─────────┘  └─────────┘  └─────────┘       │
│                                             │
│  Events Timeline                             │
│  ─────────────────────────────────────      │
│  45' ⚽ Goal - Team A (Player 9)            │
│  32' 🟨 Card - Team B (Player 4)            │
│  15' ⚽ Goal - Team B (Player 7)            │
│                                             │
└─────────────────────────────────────────────┘
```

#### Basketball Scoring
```
┌─────────────────────────────────────────────┐
│  HOME          78 - 72          AWAY        │
│                                             │
│  Q4  |  2:34  |  Shot: 24                    │
│                                             │
│  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐            │
│  │ 1PT │ │ 2PT │ │ 3PT │ │  FT │            │
│  └─────┘ └─────┘ └─────┘ └─────┘            │
│                                             │
│  ┌─────┐ ┌─────┐ ┌─────┐                    │
│  │FOUL │ │TOUT │ │TURNO│                    │
│  └─────┘ └─────┘ └─────┘                    │
│                                             │
└─────────────────────────────────────────────┘
```

#### Golf Scoring
```
┌─────────────────────────────────────────────┐
│  MASTERS 2026 - ROUND 3                     │
│                                             │
│  Leaderboard                                │
│  ─────────────────────────────────────      │
│  1. J. Smith     -12  (68)                  │
│  2. T. Woods     -10  (70)                  │
│  3. R. McIlroy   -8   (71)                  │
│                                             │
│  Current Hole: 14  Par 4                    │
│                                             │
│  Your Player: J. Smith                      │
│  Current: -2 (Birdie!)                     │
│                                             │
│  Hole  |  1  2  3  4  5  6  7  8  9 10 ...  │
│  Par   |  4  3  5  4  4  3  4  5  4  4 ...  │
│  Score |  4  2  5  3  4  3  5  4  4  4 ...  │
│                                             │
└─────────────────────────────────────────────┘
```

#### MMA/Boxing Scoring
```
┌─────────────────────────────────────────────┐
│  UFC 315 - MAIN EVENT                       │
│                                             │
│  ROUND 3 OF 5                               │
│                                             │
│  ┌─────────────┐    ┌─────────────┐         │
│  │  FIGHTER A  │    │  FIGHTER B  │         │
│  │    28-27    │    │    29-28    │         │
│  └─────────────┘    └─────────────┘         │
│                                             │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐       │
│  │ KNOCKOUT│  │  TKO    │  │DECISION │       │
│  └─────────┘  └─────────┘  └─────────┘       │
│                                             │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐       │
│  │ KNOCKDWN│  │ SUBMIT  │  │  DRAW   │       │
│  └─────────┘  └─────────┘  └─────────┘       │
│                                             │
└─────────────────────────────────────────────┘
```

### 5.5 Offline Mode

#### Enabling Offline Scoring
1. Go to **Settings**
2. Enable **Offline Mode**
3. Download contest data when online

#### Scoring Offline
1. Score normally - events are saved locally
2. Offline indicator shows sync status
3. Events queue for sync

#### Syncing When Online
1. Connectivity restored
2. Automatic sync begins
3. Status shows "Syncing..."
4. Confirmation when complete

### 5.6 Notifications

#### Setting Up Notifications
1. Go to **Settings** → **Notifications**
2. Enable notification types:
   - Contest starts
   - Score updates
   - Final results
   - Team updates
3. Set quiet hours (optional)

#### Managing Notifications
1. View all notifications in app
2. Mark as read
3. Delete old notifications

---

## 6. Competition Management

### 6.1 Competition Lifecycle

```
Draft → Published → In Progress → Completed → Archived
  │         │            │            │           │
  │         │            │            │           │
  └─ Edit   └─ Register  └─ Live       └─ Results  └─ Hide
           participants  scoring
```

### 6.2 Format Engine Selection

#### Round-Robin
- Every participant plays every other participant
- Best for leagues and groups
- Generates n(n-1)/2 contests

#### Knockout
- Single elimination bracket
- Best for tournaments
- Generates n-1 contests (with bye handling)

#### Group + Knockout
- Group stage followed by knockout
- Best for world cups
- Combines round-robin and knockout

#### Best-of-Series
- Multiple games between teams
- First to win n games advances
- Best for playoffs

#### Multi-Round Leaderboard
- Multiple rounds/stages
- Cumulative scoring
- Best for golf, motorsports

#### Heat Qualification
- Heats determine qualification
- Fastest times advance
- Best for athletics, swimming

### 6.3 Schedule Generation

1. Add venues
2. Set venue availability
3. Configure rest days
4. Click **Generate Schedule**
5. Review and adjust
6. Confirm schedule

---

## 7. Live Scoring

### 7.1 Starting a Contest

1. Navigate to contest
2. Click **Start Contest**
3. Confirm participants present
4. Verify scores at 0-0
5. Begin scoring

### 7.2 Event Submission

#### Standard Events
1. Select participant/team
2. Select event type
3. Add value (if applicable)
4. Confirm event
5. Event broadcasts to all

#### Special Events
- **Penalties**: Select type, confirm
- **Substitutions**: Select out player, select in player
- **Timeouts**: Select team, start timer

### 7.3 Corrections

#### Requesting Correction
1. Tap on event
2. Select **Request Correction**
3. Enter reason
4. Submit for approval

#### Approving Correction
1. Review correction request
2. Verify with video/records
3. Approve or reject
4. Notification sent to scorer

### 7.4 Ending Contest

1. Verify all events recorded
2. Click **End Contest**
3. Confirm final score
4. Submit for approval (if required)
5. Contest marked as completed

---

## 8. Reports and Analytics

### 8.1 Competition Reports

#### Generating Report
1. Navigate to competition
2. Click **Reports**
3. Select report type
4. Set date range
5. Click **Generate**

#### Report Types
- **Summary**: Overview statistics
- **Detailed**: Event-by-event
- **Participants**: Individual stats
- **Timeline**: Chronological events

### 8.2 Participant Statistics

#### Viewing Stats
1. Navigate to participant
2. Click **Statistics** tab
3. View career stats
4. Filter by competition/date

#### Available Statistics
- Games played
- Wins/Losses/Draws
- Points scored
- Performance trends

### 8.3 Data Export

#### Exporting Data
1. Navigate to **Reports** → **Export**
2. Select data type
3. Choose format (CSV, Excel, JSON)
4. Set filters
5. Click **Export**
6. Download file

---

## 9. Troubleshooting

### 9.1 Common Issues

#### Cannot Log In
- Verify email and password
- Check Caps Lock is off
- Reset password if forgotten
- Contact admin if issue persists

#### Offline Mode Not Working
- Enable offline mode in settings
- Download contest data before going offline
- Check device storage space
- Restart app

#### Events Not Syncing
- Check internet connection
- Verify sync status in app
- Force sync from settings
- Check for sync errors

#### Real-Time Updates Delayed
- Check internet connection
- Refresh page/app
- Check SignalR connection status
- Contact support if persistent

### 9.2 Error Messages

| Error | Solution |
|-------|----------|
| "Session expired" | Log in again |
| "Network error" | Check connection |
| "Sync failed" | Retry or contact support |
| "Invalid data" | Refresh and try again |
| "Access denied" | Contact admin |

### 9.3 Contact Support

For additional help:
- Email: support@platform.com
- Phone: +1-800-PLATFORM
- Hours: 24/7 for live contests

---

## Appendix A: Keyboard Shortcuts

### Admin Web
| Shortcut | Action |
|----------|--------|
| Ctrl+N | New competition |
| Ctrl+S | Save current form |
| Ctrl+F | Search |
| Esc | Close modal |

### Mobile App
| Gesture | Action |
|---------|--------|
| Swipe left | Next event |
| Swipe right | Previous event |
| Long press | Event options |
| Pull down | Refresh |

---

## Appendix B: Glossary

| Term | Definition |
|------|------------|
| Contest | A single match/game between participants |
| Competition | A tournament or league containing contests |
| Participant | An athlete or team in a competition |
| Segment | A period within a contest (e.g., quarter, round) |
| Event | A scoring action within a contest |
| Tenant | An organization using the platform |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | July 2026 | Development Team | Initial release |
