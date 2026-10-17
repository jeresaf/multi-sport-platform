# Sponsorship System — Full Specification

## 1. Purpose

Manage sponsor identities, campaigns, assets, placements, competition associations, and presentation rules independently of tenant branding.

---

# 2. Scope

In scope:
- Sponsor profile
- Campaign
- Asset
- Placement
- Start/end dates
- Tenant/competition association
- Visibility
- Impression/click event references
- Sponsor-free configuration
- Ad-provider placement integration hooks

Out of scope:
- Tenant visual identity
- Financial accounting
- Google AdSense implementation details
- Competition ownership

---

# 3. Core Model

```text
Sponsor
SponsorCampaign
SponsorAsset
SponsorPlacement
SponsorAssociation
SponsorMetric
```

Placement examples:

```text
HOME_FEED_BANNER
COMPETITION_HEADER
MATCH_DETAIL
SIDEBAR
MOBILE_INLINE
FOOTER
```

---

# 4. Business Rules

## RULE-SPN-001
Campaign visibility respects effective dates.

## RULE-SPN-002
Assets must meet placement constraints.

## RULE-SPN-003
Sponsor removal must not break layout.

## RULE-SPN-004
Competition sponsor association is distinct from tenant-global sponsor.

## RULE-SPN-005
Ad-provider content and direct sponsor content should use separate placement source types.

---

# 5. Commands

```text
sponsor.create
campaign.create
campaign.activate
campaign.pause
asset.upload
placement.assign
association.set
```

---

# 6. UI

Sponsors  
Campaigns  
Assets  
Placements  
Preview  
Analytics

---

# 7. Tests

- campaign dates
- tenant/global vs competition sponsor
- missing asset
- placement fallback
- sponsor-free mode

---

# 8. Definition of Done

Complete when tenant/competition sponsorship can be managed without hard-coding ads into UI components.
