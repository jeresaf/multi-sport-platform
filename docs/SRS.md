# Software Requirements Specification (SRS)
# Multi-Sport Platform

**Version:** 1.1  
**Date:** 2026-08-10  
**Status:** Maintained requirements baseline

---

## 1. Introduction

### 1.1 Purpose
This document defines the requirements for the Multi-Sport Platform: a sports
management system supporting football, basketball, rugby, golf, MMA/boxing,
athletics, and T20 cricket with real-time scoring, offline support, and
multi-tenant architecture. Implementation status and proof belong in the
repository-level `task-progress.md`, not in this requirements baseline.

### 1.2 Scope
The platform consists of:
- **Backend API**: .NET 9 ASP.NET Core with Entity Framework Core
- **Admin Web Application**: Next.js-based administration dashboard
- **Public Web Application**: Next.js-based public-facing website
- **Mobile Application**: Flutter-based supporter and scorer apps

### 1.3 Definitions, Acronyms, and Abbreviations

| Term | Definition |
|------|------------|
| Tenant | An organization using the platform with isolated data |
| Contest | A single match/game/event within a competition |
| Competition | A tournament, league, or event containing multiple contests |
| Participant | An athlete or team competing in a contest |
| Segment | A period within a contest (e.g., quarter, round, inning) |
| Event | A scoring action within a contest (e.g., goal, basket, touchdown) |

---

## 2. Overall Description

### 2.1 Product Perspective
The Multi-Sport Platform is a cloud-native, multi-tenant SaaS application designed for sports organizations, leagues, and event managers to create, manage, and score sporting competitions.

### 2.2 Product Features

#### 2.2.1 Multi-Tenant Architecture
- Tenant isolation using authenticated claims and explicit tenant ID/slug context
- Custom branding per tenant (colors, logo, name)
- Subscription-based feature entitlements
- Lease expiry controls

#### 2.2.2 Sports Management
- Universal sport definition framework
- Sport-specific rulesets and configurations
- Sport packages for Football, Basketball, Rugby, Golf, MMA/Boxing, Athletics, and Cricket T20
- Configurable segments, events, and metrics per sport

#### 2.2.3 Competition Management
- Competition creation wizard
- Multiple format engines:
  - Round-robin
  - Knockout (single elimination)
  - Group + Knockout
  - Best-of-series
  - Multi-round leaderboard (golf)
  - Heat qualification (athletics)
  - Fight card (MMA/boxing)
- Automated schedule generation
- Venue and resource management

#### 2.2.4 Participant Management
- Athlete profiles with statistics
- Team/club profiles with rosters
- Configurable registration forms
- Registration workflow (draft, submit, review, approve, reject)
- Bulk import via CSV

#### 2.2.5 Live Scoring
- Real-time event submission
- SignalR-based live updates
- Sport-specific scoring consoles
- Offline scoring with sync
- Correction workflow with approval
- Result submission and approval

#### 2.2.6 Reporting and Analytics
- Competition reports
- Participant statistics
- Contest sheets (printable)
- Tenant data export (JSON/CSV/Excel)

#### 2.2.7 Commercial Features
- Sponsor management
- Feature entitlements
- Usage metering

#### 2.2.8 Security and Compliance
- JWT authentication
- Role-based access control
- Audit logging
- Rate limiting
- Privacy controls (consent, data export, retention)

### 2.3 User Classes

| User Class | Description |
|------------|-------------|
| Platform Admin | Manages tenants, subscriptions, global settings |
| Tenant Admin | Manages their organization's competitions, users, branding |
| Competition Manager | Creates and manages competitions and contests |
| Official/Scorer | Records live events and manages scoring |
| Participant | Views schedules, results, and statistics |
| Spectator/Supporter | Views live scores and competition information |
| Sponsor | Views sponsorship dashboard and analytics |

### 2.4 Constraints

#### 2.4.1 Technical Constraints
- Backend requires .NET 9 SDK
- Mobile app requires Flutter 3.x
- Server database: PostgreSQL in development, test-container, staging, and production workflows
- Mobile offline database: Drift over SQLite on the device
- Real-time: SignalR with WebSocket support

#### 2.4.2 Operational Constraints
- Multi-tenant isolation must be enforced through authorization, explicit query/write ownership checks, relational constraints, and tenant-isolation tests
- Offline scoring requires local storage and sync mechanism
- All API endpoints must be rate-limited

---

## 3. Specific Requirements

### 3.1 Functional Requirements

#### 3.1.1 Authentication and Authorization
| ID | Requirement | Priority |
|----|-------------|----------|
| AUTH-001 | Users shall be able to register with email and password | HIGH |
| AUTH-002 | Users shall be able to login and receive JWT tokens | HIGH | 
| AUTH-003 | Users shall be able to refresh expired tokens | HIGH |
| AUTH-004 | Users shall be able to logout and invalidate tokens | MEDIUM |
| AUTH-005 | System shall support role-based access control | HIGH |
| AUTH-006 | Multi-tenant isolation shall be enforced via middleware | HIGH |

#### 3.1.2 Tenant Management
| ID | Requirement | Priority |
|----|-------------|----------|
| TENANT-001 | Tenants shall be able to customize branding (colors, logo) | HIGH |
| TENANT-002 | Tenants shall have configurable feature entitlements | HIGH |
| TENANT-003 | System shall enforce subscription lease expiry | HIGH |
| TENANT-004 | Tenant-owned reads, writes, hubs, and background work shall enforce explicit tenant scope | HIGH |

#### 3.1.3 Sports Configuration
| ID | Requirement | Priority |
|----|-------------|----------|
| SPORT-001 | System shall support configurable sport definitions | HIGH |
| SPORT-002 | Each sport shall define segments, events, and metrics | HIGH |
| SPORT-003 | System shall validate event submissions against sport rules | HIGH |

#### 3.1.4 Competition Management
| ID | Requirement | Priority |
|----|-------------|----------|
| COMP-001 | Admins shall be able to create competitions via wizard | HIGH |
| COMP-002 | System shall support multiple competition formats | HIGH |
| COMP-003 | System shall generate schedules based on format | HIGH |
| COMP-004 | Competitions shall have configurable statuses | HIGH |

#### 3.1.5 Contest Management
| ID | Requirement | Priority |
|----|-------------|----------|
| CONTEST-001 | Contests shall have configurable lifecycle states | HIGH |
| CONTEST-002 | Participants shall be able to check-in to contests | HIGH |
| CONTEST-003 | Scorers shall be able to submit live events | HIGH |
| CONTEST-004 | System shall broadcast real-time updates via SignalR | HIGH |
| CONTEST-005 | Corrections shall require approval workflow | HIGH |
| CONTEST-006 | Results shall require approval before finalization | HIGH |

#### 3.1.6 Participant Management
| ID | Requirement | Priority |
|----|-------------|----------|
| PART-001 | System shall support athlete and team profiles | HIGH |
| PART-002 | Registration forms shall be configurable | HIGH |
| PART-003 | Registration workflow shall support multiple statuses | HIGH |
| PART-004 | Bulk import shall support CSV format | MEDIUM |

#### 3.1.7 Mobile Application
| ID | Requirement | Priority |
|----|-------------|----------|
| MOBILE-001 | App shall support offline scoring | HIGH |
| MOBILE-002 | App shall sync data when connectivity restored | HIGH |
| MOBILE-003 | App shall receive real-time updates via SignalR | HIGH |
| MOBILE-004 | App shall support push notifications | MEDIUM |

### 3.2 Non-Functional Requirements

#### 3.2.1 Performance
| ID | Requirement | Target |
|----|-------------|--------|
| PERF-001 | API response time | < 200ms (p95) |
| PERF-002 | Real-time update latency | < 500ms |
| PERF-003 | Mobile app launch time | < 3 seconds |
| PERF-004 | Support 1000 concurrent users per tenant | - |

#### 3.2.2 Reliability
| ID | Requirement | Target |
|----|-------------|--------|
| REL-001 | System availability | 99.9% |
| REL-002 | Offline data loss | < 1% |
| REL-003 | Recovery time objective | < 15 minutes |

#### 3.2.3 Security
| ID | Requirement | Priority |
|----|-------------|----------|
| SEC-001 | Administrative and mutation endpoints shall require authentication; only explicitly designated public projections may allow anonymous access | HIGH |
| SEC-002 | Sensitive data shall be encrypted at rest | HIGH |
| SEC-003 | Rate limiting shall prevent abuse | HIGH |
| SEC-004 | Security-sensitive and domain-significant mutations shall create append-only audit history | HIGH |

#### 3.2.4 Scalability
| ID | Requirement | Target |
|----|-------------|--------|
| SCALE-001 | Horizontal scaling support | Yes |
| SCALE-002 | Tenant-aware shared-schema indexes and isolation | Yes |
| SCALE-003 | CDN support for static assets | Yes |

---

## 4. Sport-Specific Requirements

### 4.1 Football
- Segments: 2 halves (45 minutes each)
- Events: Goal, Yellow Card, Red Card, Substitution, Foul, Penalty, Own Goal
- Metrics: Score, Possession, Shots, Corners, Fouls, Cards
- Ranking: Points, Goal Difference, Goals Scored, Head-to-Head

### 4.2 Basketball
- Segments: 4 quarters (12 minutes each) + overtime
- Events: 1-point, 2-point, 3-point, Free Throw, Foul, Timeout, Turnover
- Metrics: Score, Fouls, Timeouts, Possession
- Ranking: Win Percentage, Points For/Against

### 4.3 Rugby
- Segments: 2 halves (40 minutes each)
- Events: Try, Conversion, Penalty Goal, Drop Goal, Yellow Card, Red Card, Substitution
- Metrics: Score and discipline/event counts
- Ranking: Competition points, points difference, points scored, configured tie-breakers

### 4.4 Golf
- Segments: 4 rounds (18 holes each)
- Events: Birdie, Eagle, Bogey, Double Bogey, Par, Hole-in-One
- Metrics: Strokes, Score to Par, Total Score
- Ranking: Lowest total score (cut after 2 rounds)

### 4.5 MMA/Boxing
- Segments: 3-5 rounds (3-5 minutes each)
- Events: Knockdown, TKO, Submission, Decision, Draw
- Metrics: Judge Scores, Knockdowns, Control Time
- Ranking: Win/Loss record, Title defenses

### 4.6 Athletics
- Segments: Heats, Semifinals, Finals
- Events: Finish, DNF, DQ, False Start
- Metrics: Time, Wind, Position
- Ranking: Fastest time (qualification by place or time)

### 4.7 Cricket T20
- Segments: 2 innings (20 overs each)
- Events: Run, Wicket, Wide, No Ball, Boundary, Six
- Metrics: Runs, Wickets, Run Rate, Overs
- Ranking: Points, Net Run Rate

---

## 5. API Requirements

### 5.1 Authentication Endpoints
```
POST /api/auth/register - User registration
POST /api/auth/login - User login
POST /api/auth/refresh - Token refresh
POST /api/auth/logout - User logout
```

### 5.2 Competition Endpoints
```
GET /api/competitions - List competitions
GET /api/competitions/{id} - Get competition details
POST /api/competitions - Create competition
PUT /api/competitions/{id} - Update competition
DELETE /api/competitions/{id} - Delete competition
GET /api/competitions/{id}/contests - Get competition contests
```

### 5.3 Contest Endpoints
```
GET /api/contests - List contests
GET /api/contests/live - Get live contests
GET /api/contests/upcoming - Get upcoming contests
GET /api/contests/{id} - Get contest details
POST /api/contests/{id}/events - Submit live event
POST /api/contests/{id}/checkin - Check in participant
```

### 5.4 Participant Endpoints
```
GET /api/participants/{id} - Get participant details
GET /api/participants/{id}/stats - Get participant statistics
```

### 5.5 SignalR Hub
```
ContestUpdated - Contest status changed
ScoreUpdated - Score changed
EventRecorded - New event recorded
```

---

## 6. Data Models

### 6.1 Core Entities
- **Tenant**: Organization using the platform
- **User**: Platform user with roles
- **Sport**: Sport definition with configurations
- **Competition**: Tournament or league
- **Contest**: Single match or event
- **Participant**: Athlete or team
- **Registration**: Competition registration
- **ContestEvent**: Scoring event within contest

### 6.2 Entity Relationships
```
Tenant (1) ── (*) User
Tenant (1) ── (*) Competition
Tenant (1) ── (*) Participant
Competition (1) ── (*) Contest
Contest (1) ── (*) ContestEvent
Contest (*) ── (*) Participant (via ContestParticipant)
Sport (1) ── (*) Competition
```

---

## 7. Acceptance requirements

The following are required outcomes. Their current verification status is
reported in `task-progress.md`; unchecked Markdown boxes are intentionally not
used here because this document is not a progress tracker.

### 7.1 Authentication
- Users can register with valid email/password.
- Users can login and receive JWT tokens.
- Invalid credentials return 401.
- Expired tokens can be refreshed.
- Multi-tenant isolation is enforced.

### 7.2 Competition Management
- Competitions can be created through the administrative workflow.
- Format engines generate valid brackets/schedules.
- Competition statuses transition correctly.
- Schedules respect configured venue/resource constraints.

### 7.3 Live Scoring
- Events are submitted through an authoritative persisted command path.
- SignalR publishes tenant-safe updates after persistence.
- Corrections require approval.
- Results are approved before publication.
- Offline scoring synchronizes in order when connectivity returns.

### 7.4 Mobile App
- The app opens on a useful public experience with login available as an option.
- Offline scoring works without connectivity.
- Data synchronizes when connectivity returns.
- Configured push notifications are received.

### 7.5 Security
- Protected endpoints require authentication and permission checks.
- Explicit public projections disclose only published tenant-safe data.
- Rate limiting prevents abuse.
- Append-only audit history captures domain-significant and security-sensitive mutations.
- Tenant data is isolated.

---

## 8. Appendix

### 8.1 Glossary
See Section 1.3

### 8.2 References
- API Documentation: docs/api-reference.md
- Architecture Documentation: docs/architecture.md
- Architecture Decisions: docs/adr/README.md
- Deployment Guide: infrastructure/deployment/

### 8.3 Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | July 2026 | Development Team | Initial release |
| 1.1 | 2026-08-10 | Development Team | Aligned tenancy, persistence, public API, rugby, mobile entry, audit, and architecture references with the authoritative backlog and implementation |
