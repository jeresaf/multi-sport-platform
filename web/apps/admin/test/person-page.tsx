// The API answers the person page needs (S06 T7): the signed-in registrar at KMSA, the tenant's settings, the test
// sport, the person and their status history. Each test file mocks `next/navigation` itself (vi.mock is hoisted per
// file) and adds the answers its case needs.
import type { components } from "@msp/api-client";
import { tenantCapabilities } from "./capabilities";
import type { Answer, Answers } from "./render";
import { ringball, ringballVariant } from "./sport-definitions";

type Person = components["schemas"]["PersonResponse"];
type TenantCapabilities = components["schemas"]["TenantCapabilitiesResponse"];

export const tenantId = "0199a3f0-0000-7000-8000-00000000000a";

const me: Answer = {
  status: 200,
  body: {
    id: "0199a3f0-0000-7000-8000-000000000001",
    email: "brian.ssemakula@kmsa.example",
    platformRoles: [],
    tenants: [
      { tenantId, slug: "kmsa", displayName: "Kampala Metro Sports Association", status: "ACTIVE", roles: ["REGISTRAR"] },
    ],
  },
};

const settings: Answer = {
  status: 200,
  body: {
    timeZone: "Africa/Kampala",
    currency: "UGX",
    languageCode: "en",
    startingSoonMinutes: 30,
    requireGuardianConsentForMinors: true,
    publicEnabled: true,
    version: "1",
  },
};

/** The answers for the page showing `shown`, with these capabilities (an action list, or the whole answer). */
export function personPageAnswers(shown: Person, capabilities: string[] | TenantCapabilities): Answers {
  return {
    "GET /api/account/v1/me": me,
    "GET /api/admin/v1/me/capabilities": {
      status: 200,
      body: Array.isArray(capabilities) ? tenantCapabilities(capabilities) : capabilities,
    },
    "GET /api/admin/v1/tenant/settings": settings,
    "GET /api/public/v1/reference/countries": {
      status: 200,
      body: { items: [{ code: "UG", alpha3Code: "UGA", numericCode: "800", name: "Uganda" }], page: { nextCursor: null, hasMore: false } },
    },
    "GET /api/admin/v1/sports": { status: 200, body: { items: [ringballVariant] } },
    "GET /api/admin/v1/sports/RINGBALL_SEVENS": { status: 200, body: ringball },
    [`GET /api/admin/v1/people/${shown.id}`]: { status: 200, body: shown },
    [`GET /api/admin/v1/people/${shown.id}/status-changes`]: {
      status: 200,
      body: {
        items: [
          {
            id: "0199a3f0-0000-7000-8000-0000000000f9",
            fromStatus: null,
            toStatus: "ACTIVE",
            reason: null,
            actorKind: "USER",
            actorId: "0199a3f0-0000-7000-8000-000000000001",
            occurredAt: "2026-11-02T21:30:00Z",
          },
        ],
      },
    },
  };
}
