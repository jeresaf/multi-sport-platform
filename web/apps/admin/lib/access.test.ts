import { describe, expect, it } from "vitest";
import { accessNotice, nextDay, type CapabilityAccess } from "./access";

// KMSA's pilot period (pilot-customer.md): 1 Oct 2026 to 31 Mar 2027, 14 days of grace.
const period = { startsOn: "2026-10-01", endsOn: "2027-03-31", graceEndsOn: "2027-04-14", cancelledOn: null };
const access = (overrides: Partial<CapabilityAccess>): CapabilityAccess => ({
  mode: "FULL",
  subscriptionStatus: "ACTIVE",
  ...period,
  timeZone: "Africa/Kampala",
  ...overrides,
});
const none = { subscriptionStatus: null, startsOn: null, endsOn: null, graceEndsOn: null, cancelledOn: null };

describe("accessNotice", () => {
  it("says nothing while the subscription is active, or while a tenant is set up without one", () => {
    expect(accessNotice(access({}))).toBeNull();
    expect(accessNotice(access({ ...none }))).toBeNull();
  });

  it("warns, with full access, while the subscription is expiring or in grace", () => {
    expect(accessNotice(access({ subscriptionStatus: "EXPIRING" }))).toEqual({
      tone: "warning",
      title: "The subscription ends soon",
      message: "It ends on 31 Mar 2027. If it hasn't been renewed, contact the platform team.",
    });
    expect(accessNotice(access({ subscriptionStatus: "GRACE" }))?.message).toBe(
      "It ended on 31 Mar 2027. Full access continues until 14 Apr 2027; after that the workspace is read-only. If it hasn't been renewed, contact the platform team.",
    );
  });

  it.each([
    ["EXPIRED", {}, "Since 15 Apr 2027: the subscription ended on 31 Mar 2027."],
    ["CANCELLED", { cancelledOn: "2026-10-20" }, "Since 20 Oct 2026: the subscription was cancelled."],
    ["SCHEDULED", { startsOn: "2026-11-01" }, "Until 1 Nov 2026: the subscription hasn't started yet."],
  ] as const)("says why a read-only workspace is read-only and since or until when: %s", (status, dates, reason) => {
    const notice = accessNotice(access({ mode: "READ_ONLY", subscriptionStatus: status, ...dates }));
    expect(notice?.tone).toBe("readOnly");
    expect(notice?.title).toBe("This workspace is read-only");
    expect(notice?.message).toBe(
      `${reason} You can still see everything, but nothing can be changed. To restore full access, contact the platform team.`,
    );
  });

  it("doesn't date a cancellation made before the period started", () => {
    const notice = accessNotice(
      access({ mode: "READ_ONLY", subscriptionStatus: "CANCELLED", startsOn: "2027-05-01", cancelledOn: "2027-04-10" }),
    );
    expect(notice?.message).toMatch(/^The subscription was cancelled\. /);
  });

  it("says a read-only workspace has no subscription when it has none", () => {
    expect(accessNotice(access({ mode: "READ_ONLY", ...none }))?.message).toMatch(/^The organisation has no subscription\./);
  });
});

describe("nextDay", () => {
  it("crosses months and years", () => {
    expect(nextDay("2027-04-14")).toBe("2027-04-15");
    expect(nextDay("2026-12-31")).toBe("2027-01-01");
  });
});
