import { describe, expect, it } from "vitest";
import { safeReturnTo, signInPath } from "./return-to";

describe("safeReturnTo", () => {
  it.each([
    ["/t/kmsa/settings", "/t/kmsa/settings"],
    ["/platform?tab=tenants#top", "/platform?tab=tenants#top"],
  ])("keeps a path on this site: %s", (value, expected) => {
    expect(safeReturnTo(value)).toBe(expected);
  });

  it.each([
    null,
    undefined,
    "",
    "https://evil.example/",
    "//evil.example/",
    "/\\evil.example",
    "javascript:alert(1)",
    "t/kmsa",
    "/sign-in?returnTo=/platform",
  ])("falls back to / for %j", (value) => {
    expect(safeReturnTo(value)).toBe("/");
  });
});

describe("signInPath", () => {
  it("carries the return address, encoded", () => {
    expect(signInPath("/t/kmsa?x=1")).toBe("/sign-in?returnTo=%2Ft%2Fkmsa%3Fx%3D1");
  });

  it("leaves it out for the home page", () => {
    expect(signInPath("/")).toBe("/sign-in");
  });
});
