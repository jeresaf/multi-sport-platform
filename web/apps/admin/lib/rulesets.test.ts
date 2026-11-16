import { describe, expect, it } from "vitest";
import { quickBout, ringLeague } from "@/test/ruleset-templates";
import { ringball } from "@/test/sport-definitions";
import { ageLabel, namesByCode, officialLabel, periodLabel, positionLabel, rulesetValueLabel } from "./rulesets";

describe("ruleset template words", () => {
  it("labels periods, positions, groups and officials from the definition, else writes the code as words", () => {
    expect(periodLabel(ringball, "RING_OFF")).toBe("Ring-off");
    expect(periodLabel(undefined, "RING_OFF")).toBe("Ring off");
    expect(positionLabel(ringball, "HOOP_GUARD")).toBe("Hoop guard");
    expect(positionLabel(ringball, "RINGERS")).toBe("Any of: Wing ringer, Centre ringer");
    expect(positionLabel(ringball, "SWEEPER")).toBe("Sweeper");
    expect(officialLabel(ringball, "TIMEKEEPER")).toBe("Timekeeper");
    expect(officialLabel(undefined, "RING_JUDGE")).toBe("Ring judge");
  });

  it("labels tiebreakers from the template's catalogue entries, other codes as words", () => {
    expect(rulesetValueLabel(ringLeague, "COIN_TOSS")).toBe("Toss of a coin");
    expect(rulesetValueLabel(ringLeague, "FAR_RINGS")).toBe("Far rings");
    expect(rulesetValueLabel(quickBout, "EXTRA_PERIODS_SUDDEN_DEATH")).toBe("Extra periods sudden death");
  });

  it("writes age bounds in words", () => {
    expect(ageLabel(null, null)).toBe("No limit");
    expect(ageLabel(16, null)).toBe("At least 16");
    expect(ageLabel(null, 19)).toBe("At most 19");
    expect(ageLabel(12, 12)).toBe("Only 12");
  });

  it("maps codes to names, empty while the list isn't read", () => {
    expect(namesByCode([{ c: "A", n: "Alpha" }], (item) => item.c, (item) => item.n)).toEqual({ A: "Alpha" });
    expect(namesByCode<{ c: string }>(undefined, (item) => item.c, (item) => item.c)).toEqual({});
  });
});
