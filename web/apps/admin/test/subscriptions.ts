// Test data for the console's plans and subscriptions: two plans shaped like the seed (S04 D2) with one entitlement of
// each type, and a subscription as `GET …/tenants/{tenantId}/subscription` answers.
import type { components } from "@msp/api-client";

type Plan = components["schemas"]["PlanResponse"];
type Definition = components["schemas"]["PlanEntitlementResponse"];
type Subscription = components["schemas"]["SubscriptionResponse"];
type Period = components["schemas"]["SubscriptionPeriodResponse"];

const definitions: Omit<Definition, "value">[] = [
  {
    code: "BRANDING",
    name: "Branding",
    description: "How far the tenant can brand its public site.",
    type: "LEVEL",
    unit: null,
    levels: ["BASIC", "FULL"],
    default: { level: null },
    enforcedBy: "Branding & Sponsorship",
  },
  {
    code: "CUSTOM_DOMAIN",
    name: "Custom domain",
    description: "The public site on the tenant's own domain.",
    type: "BOOLEAN",
    unit: null,
    levels: [],
    default: { enabled: false },
    enforcedBy: "Tenancy & Commercial",
  },
  {
    code: "MAX_ENABLED_SPORTS",
    name: "Enabled sports",
    description: "Sports the tenant can enable at once.",
    type: "QUOTA",
    unit: "sports",
    levels: [],
    default: { limit: 0 },
    enforcedBy: "Tenancy & Commercial",
  },
  {
    code: "MAX_STAFF_USERS",
    name: "Staff users",
    description: "Members and pending invitations.",
    type: "QUOTA",
    unit: "users",
    levels: [],
    default: { limit: 0 },
    enforcedBy: "Identity & Access",
  },
];

const withValues = (values: Record<string, Definition["value"]>): Definition[] =>
  definitions.map((definition) => ({ ...definition, value: values[definition.code] ?? null }));

export const standard: Plan = {
  code: "STANDARD",
  name: "Standard",
  rank: 1,
  entitlements: withValues({
    BRANDING: { level: "BASIC" },
    CUSTOM_DOMAIN: { enabled: false },
    MAX_ENABLED_SPORTS: { limit: 3 },
    MAX_STAFF_USERS: { limit: 100 },
  }),
};

export const pro: Plan = {
  code: "PRO",
  name: "Pro",
  rank: 2,
  entitlements: withValues({
    BRANDING: { level: "FULL" },
    CUSTOM_DOMAIN: { enabled: true },
    MAX_ENABLED_SPORTS: { limit: null },
    MAX_STAFF_USERS: { limit: 1000 },
  }),
};

export const plansAnswer = { status: 200, body: { items: [standard, pro] } };

export const kmsaId = "0199a3f0-0000-7000-8000-00000000000a";

/** KMSA's pilot period on STANDARD (pilot-customer.md): 1 Oct 2026 to 31 Mar 2027, active on 29 Oct 2026. */
export const pilotPeriod: Period = {
  id: "0199a3f0-0000-7000-8000-0000000000p1",
  planCode: "STANDARD",
  kind: "COMPLIMENTARY",
  status: "ACTIVE",
  startsOn: "2026-10-01",
  endsOn: "2027-03-31",
  expiringWindowDays: 30,
  graceDays: 14,
  expiringFrom: "2027-03-01",
  graceEndsOn: "2027-04-14",
  createdAt: "2026-09-30T07:00:00Z",
  createdBy: "0199a3f0-0000-7000-8000-000000000001",
  cancelledAt: null,
  cancelledBy: null,
  cancelReason: null,
};

type Override = components["schemas"]["EntitlementOverrideResponse"];

/** The staff-user override in place (the slice demo's), and the one it replaced. */
export const staffOverride: Override = {
  id: "0199a3f0-0000-7000-8000-0000000000o2",
  entitlementCode: "MAX_STAFF_USERS",
  value: { limit: 5 },
  reason: "Pilot offer: five staff seats while the association trials the platform",
  effectiveUntil: "2027-03-31",
  inEffect: true,
  grantedAt: "2026-10-20T21:30:00Z",
  grantedBy: "0199a3f0-0000-7000-8000-000000000001",
  removedAt: null,
  removedBy: null,
  removeReason: null,
};
export const replacedOverride: Override = {
  ...staffOverride,
  id: "0199a3f0-0000-7000-8000-0000000000o1",
  value: { limit: 3 },
  reason: "Pilot offer: three staff seats",
  effectiveUntil: null,
  inEffect: false,
  grantedAt: "2026-10-02T08:00:00Z",
  removedAt: "2026-10-20T21:30:00Z",
  removedBy: "0199a3f0-0000-7000-8000-000000000001",
  removeReason: "Replaced by a new override",
};

export function subscription(overrides: Partial<Subscription> = {}): Subscription {
  return {
    tenantId: kmsaId,
    today: "2026-10-29",
    timeZone: "Africa/Kampala",
    currentPeriodId: pilotPeriod.id,
    nextPeriodId: null,
    periods: [pilotPeriod],
    overrides: [staffOverride, replacedOverride],
    entitlements: [
      { code: "BRANDING", value: { level: "BASIC" }, source: "PLAN" },
      { code: "CUSTOM_DOMAIN", value: { enabled: false }, source: "PLAN" },
      { code: "MAX_ENABLED_SPORTS", value: { limit: 3 }, source: "PLAN" },
      { code: "MAX_STAFF_USERS", value: { limit: 5 }, source: "OVERRIDE" },
    ],
    usage: [
      { code: "MAX_ENABLED_SPORTS", measured: false, used: null, limit: 3, warning: false },
      { code: "MAX_STAFF_USERS", measured: true, used: 4, limit: 5, warning: true },
    ],
    ...overrides,
  };
}

/** A tenant without any period: read-only once active (S04 D9). */
export const noSubscription = subscription({
  currentPeriodId: null,
  periods: [],
  overrides: [],
  entitlements: [
    { code: "BRANDING", value: { level: null }, source: "DEFAULT" },
    { code: "CUSTOM_DOMAIN", value: { enabled: false }, source: "DEFAULT" },
    { code: "MAX_ENABLED_SPORTS", value: { limit: 0 }, source: "DEFAULT" },
    { code: "MAX_STAFF_USERS", value: { limit: 0 }, source: "DEFAULT" },
  ],
  usage: [
    { code: "MAX_ENABLED_SPORTS", measured: false, used: null, limit: 0, warning: false },
    { code: "MAX_STAFF_USERS", measured: true, used: 0, limit: 0, warning: false },
  ],
});
