import { describe, expect, it } from "vitest";
import { ringball } from "@/test/sport-definitions";
import { codeLabel, derivationLabel, eventLabel, formatSeconds, listLabel, phaseLabel } from "./sport-definition";

const statistic = (code: string) => ringball.statistics.find((s) => s.code === code)!;

describe("sport definition words", () => {
  it("writes a code as words", () => {
    expect(codeLabel("RING_OFF_HIT")).toBe("Ring off hit");
    expect(codeLabel("HEAD_TO_HEAD")).toBe("Head to head");
    expect(codeLabel("U19")).toBe("U19");
  });

  it("writes lengths and lists", () => {
    expect(formatSeconds(600)).toBe("10 min");
    expect(formatSeconds(630)).toBe("10 min 30 s");
    expect(formatSeconds(40)).toBe("40 s");
    expect(listLabel(["A"])).toBe("A");
    expect(listLabel(["A", "B", "C"])).toBe("A, B or C");
    expect(listLabel(["A", "B"], "and")).toBe("A and B");
  });

  it("labels events and phases from the definition", () => {
    expect(eventLabel(ringball, "LONG_RING")).toBe("Long ring");
    expect(eventLabel(ringball, "PERIOD_START")).toBe("Period start");
    expect(eventLabel(ringball, "NOT_DECLARED")).toBe("Not declared");
    expect(phaseLabel(ringball, "SHOOTOUT")).toBe("Ring-off");
    expect(phaseLabel(ringball, "PLAY")).toBe("First spell, Second spell or Golden spell");
    expect(phaseLabel({ ...ringball, periods: [] }, "BREAK")).toBe("Break");
  });

  it("describes each kind of derivation with the definition's labels", () => {
    expect(derivationLabel(ringball, statistic("RINGS"))).toBe("Counts Ring or Long ring events by Thrower");
    expect(derivationLabel(ringball, statistic("FAR_RINGS"))).toBe("Counts Ring events where reach is Far");
    // A ratio is `builtin: RATIO` with its parts: read as the parts, not as "Built in: Ratio".
    expect(statistic("RING_RATE").derive.builtin).toBe("RATIO");
    expect(derivationLabel(ringball, statistic("RING_RATE"))).toBe("Rings divided by Far rings");
    expect(derivationLabel(ringball, statistic("WARNINGS"))).toBe("Entered by hand");
    const sum = { ...statistic("RINGS"), derive: { ...statistic("RINGS").derive, countEvents: [], sumPoints: ["RING"], actorRoles: [] } };
    expect(derivationLabel(ringball, sum)).toBe("Adds up the points of Ring events");
    const flag = { ...statistic("FAR_RINGS"), derive: { ...statistic("FAR_RINGS").derive, where: { blocked: "false" } } };
    expect(derivationLabel(ringball, flag)).toBe("Counts Ring events where blocked is no");
    const builtin = { ...statistic("RINGS"), derive: { ...statistic("RINGS").derive, builtin: "MINUTES_PLAYED" } };
    expect(derivationLabel(ringball, builtin)).toBe("Built in: Minutes played");
  });
});
