import { describe, expect, it } from "vitest";
import { numberRange, parameterAllowed, parameterTypeLabel, parameterValueLabel, type Parameter } from "./parameters";

const parameter = (type: Parameter["type"], rest: Partial<Parameter> = {}): Parameter =>
  ({ type, label: "A parameter", description: null, min: null, max: null, values: [], default: 0, ...rest }) as Parameter;

describe("parameters in words", () => {
  it("names each type", () => {
    expect(["NUMBER", "FLAG", "CHOICE", "LIST"].map((type) => parameterTypeLabel(type as Parameter["type"]))).toEqual([
      "Whole number",
      "Yes or no",
      "One of a list",
      "Ordered list",
    ]);
  });

  it("writes a value by its type, a list in its order", () => {
    expect(parameterValueLabel(3)).toBe("3");
    expect(parameterValueLabel(0)).toBe("0");
    expect(parameterValueLabel(true)).toBe("Yes");
    expect(parameterValueLabel(false)).toBe("No");
    expect(parameterValueLabel("BY_ENTRY_TIME")).toBe("By entry time");
    expect(parameterValueLabel(["MOST_WINS", "FEWEST_LOSSES"])).toBe("Most wins, then Fewest losses");
    expect(parameterValueLabel([])).toBe("None");
  });

  it("takes the caller's labels for codes", () => {
    const label = (code: string) => ({ MOST_WINS: "Wins", FEWEST_LOSSES: "Losses" })[code] ?? code;
    expect(parameterValueLabel(["MOST_WINS", "FEWEST_LOSSES"], label)).toBe("Wins, then Losses");
    expect(parameterAllowed(parameter("CHOICE", { values: ["MOST_WINS"] }), label)).toBe("One of: Wins");
  });

  it("says what a competition may choose", () => {
    expect(parameterAllowed(parameter("NUMBER", { min: 1, max: 4 }))).toBe("1 to 4");
    expect(parameterAllowed(parameter("FLAG"))).toBe("Yes or no");
    expect(parameterAllowed(parameter("CHOICE", { values: ["RANKED", "RANDOM"] }))).toBe("One of: Ranked, Random");
    expect(parameterAllowed(parameter("LIST", { values: ["MOST_WINS", "FEWEST_LOSSES"] }))).toBe(
      "Any of, each once, in order: Most wins, Fewest losses",
    );
  });

  it("writes a range with one end, both, the same, or neither", () => {
    expect(numberRange(1, 4)).toBe("1 to 4");
    expect(numberRange(2, 2)).toBe("Only 2");
    expect(numberRange(1, null)).toBe("At least 1");
    expect(numberRange(null, 4)).toBe("At most 4");
    expect(numberRange(null, null)).toBe("Any whole number");
  });
});
