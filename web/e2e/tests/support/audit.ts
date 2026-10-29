// Reads the audit trail through the platform API (GET /api/platform/v1/audit-entries; no Audit screen in S03).
import { expect, type APIRequestContext } from "@playwright/test";

export type AuditEntry = {
  actionCode: string;
  outcome: "SUCCESS" | "FAILURE" | "DENIED";
  errorCode: string | null;
  actorKind: string;
  actorId: string;
  tenantId: string | null;
  entityType: string | null;
  entityId: string | null;
  reason: string | null;
  metadata: unknown;
};

/** The user's ID from `/me`, for the session `request` carries. */
export async function myUserId(request: APIRequestContext): Promise<string> {
  const me = await request.get("/api/account/v1/me");
  expect(me.status()).toBe(200);
  return ((await me.json()) as { id: string }).id;
}

/**
 * Waits until the entries matching `filter` (the list's query parameters) include one for each of `expected`, then
 * returns them. `request` must be signed in as a platform administrator. Entries recorded through a module's outbox
 * arrive shortly after the action, so the list is read again, a few seconds apart: every read is audited too, so it
 * isn't read more often.
 */
export async function auditEntries(
  request: APIRequestContext,
  filter: Record<string, string>,
  expected: ((entry: AuditEntry) => boolean)[],
): Promise<AuditEntry[]> {
  let entries: AuditEntry[] = [];
  await expect
    .poll(
      async () => {
        const response = await request.get("/api/platform/v1/audit-entries", { params: { ...filter, limit: 100 } });
        expect(response.status()).toBe(200);
        entries = ((await response.json()) as { items: AuditEntry[] }).items;
        return expected.filter((matches) => !entries.some(matches)).length;
      },
      { message: `audit entries for ${JSON.stringify(filter)}`, intervals: [2_000, 3_000], timeout: 30_000 },
    )
    .toBe(0);
  return entries;
}
