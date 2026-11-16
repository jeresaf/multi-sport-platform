import { describe, expect, it } from "vitest";
import { versionParam } from "./version-param";

describe("an item page's ?version=", () => {
  it("is the latest without the parameter", () => {
    expect(versionParam(null)).toBeUndefined();
  });

  it("is a version number from 1 up to nine digits, inside the API's int32 range", () => {
    expect(versionParam("1")).toBe(1);
    expect(versionParam("42")).toBe(42);
    expect(versionParam("999999999")).toBe(999_999_999);
  });

  it("is not a version (page not found, no request) for anything else", () => {
    for (const value of ["", "0", "01", "-1", "1.0", " 1", "abc", "latest", "1000000000", "2147483648"]) {
      expect(versionParam(value), value).toBeNull();
    }
  });
});
