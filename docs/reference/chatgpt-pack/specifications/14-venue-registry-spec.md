# Venue Registry — Full Specification

## 1. Purpose

Provide canonical venue identity for scheduling, competition operations, public presentation, and historical records.

---

# 2. Scope

In scope:
- Venue identity
- Venue type
- Location
- Capacity
- Surface/facility metadata
- Supported sports
- Availability metadata
- Public profile
- Aliases
- External IDs
- Lifecycle

Out of scope:
- Fixture ownership
- Booking/payment system
- Weather
- Tenant branding

---

# 3. Venue Types

Examples:

```text
STADIUM
ARENA
SPORTS_GROUND
COURT
FIELD
GOLF_COURSE
TRACK
GYMNASIUM
COMBAT_VENUE
MULTI_SPORT_COMPLEX
OTHER
```

---

# 4. Core Entity

```text
id
tenant_id
name
short_name
venue_type
address_line
city
region
country_code
latitude
longitude
capacity
timezone
surface_type
status
public_enabled
created_at
updated_at
version
```

Additional structured facilities may be child entities.

---

# 5. Dependencies

- Tenant Management
- Reference Data
- Audit

Downstream:
- Team Registry
- Scheduling
- Contest Operations
- Public UI
- Reports

---

# 6. Business Rules

## RULE-VEN-001
Venue timezone must be explicit or reliably derived.

## RULE-VEN-002
A venue may support multiple sports.

## RULE-VEN-003
Historical contests retain venue reference even after venue archive.

## RULE-VEN-004
Venue availability is advisory input; Scheduling owns fixture conflict decisions.

## RULE-VEN-005
Public APIs expose approved location fields only.

---

# 7. Lifecycle

```text
ACTIVE
TEMPORARILY_UNAVAILABLE
INACTIVE
CLOSED
ARCHIVED
```

---

# 8. Commands

```text
venue.create
venue.update
venue.sport.enable
venue.availability.set
venue.deactivate
venue.archive
venue.alias.add
venue.external_id.add
```

---

# 9. Queries

```text
Search venues
Get venue
Get public venue
Find venues by sport
Get availability
Get upcoming contests (projection)
```

---

# 10. Events

```text
VenueCreated
VenueUpdated
VenueAvailabilityChanged
VenueArchived
```

---

# 11. Permissions

```text
venue.read
venue.create
venue.update
venue.availability.manage
venue.lifecycle.manage
```

---

# 12. Integrity

- Tenant scoped unless future shared venue registry introduced
- Unique provider+external ID
- Valid coordinates
- Valid timezone
- Archive instead of destructive delete

---

# 13. UI

Venue Directory  
Venue Detail  
Facilities  
Supported Sports  
Availability  
Upcoming Events  
History

---

# 14. Seed/Test

Include football stadium, indoor arena, rugby ground, golf course, athletics track, combat venue, multi-sport complex.

---

# 15. Definition of Done

Complete when scheduling and public presentation can consistently resolve venues and their sport/timezone characteristics.
