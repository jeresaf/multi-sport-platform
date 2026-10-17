# Notification System — Full Specification

## 1. Purpose

Deliver tenant-branded, preference-aware notifications triggered by platform and sporting events.

---

# 2. Channels

Potential:

```text
PUSH
EMAIL
SMS
IN_APP
WEB_PUSH
```

Availability depends on tenant entitlement/configuration.

---

# 3. Notification Categories

```text
ACCOUNT
TENANT
COMPETITION
SCHEDULE
LIVE_EVENT
RESULT
REGISTRATION
ROSTER
OFFICIAL_ASSIGNMENT
SUBSCRIPTION
SYSTEM
```

---

# 4. Dependencies

Consumes events from:
- Tenant
- Subscription
- Scheduling
- Contest Operations
- Timeline
- Results
- Registration
- Rosters
- Officials

Also:
- Branding
- User preferences
- Entitlements
- Audit

---

# 5. Core Model

```text
NotificationDefinition
NotificationPreference
NotificationSubscription
NotificationDelivery
NotificationTemplate
NotificationDigest
```

---

# 6. Business Rules

## RULE-NTF-001
Do not send every detailed timeline event by default.

## RULE-NTF-002
User preferences must be respected except mandatory service/security notices.

## RULE-NTF-003
Duplicate events must not generate duplicate notifications.

## RULE-NTF-004
Tenant branding applies to templates where appropriate.

## RULE-NTF-005
Notification content must not expose private data to unintended recipients.

---

# 7. Sporting Examples

```text
Match starting
Goal
Try
Wicket
Final result
Postponement
Cancellation
```

Sport-specific trigger catalog derives from canonical events.

---

# 8. Commands

```text
notification.preference.update
notification.subscription.follow
notification.subscription.unfollow
notification.send_test
```

---

# 9. UI

Notification Settings  
Follow/Unfollow  
Match Alerts  
Competition Alerts  
Delivery Preferences

---

# 10. Tests

- preference
- deduplication
- tenant branding
- goal alert
- result alert
- postponed alert
- entitlement disabled
- private data check

---

# 11. Definition of Done

Complete when users can follow relevant entities/events and receive safe, non-duplicated, tenant-branded notifications.
