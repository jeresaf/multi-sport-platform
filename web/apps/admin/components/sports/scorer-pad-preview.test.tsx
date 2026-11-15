// S05 T5 acceptance: the scorer pad preview is built from the definition, not hard-coded. Its groups, their order,
// the button labels and the notes all come from a test-only definition; change the definition and the pad changes.
import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { SportDefinition } from "@/lib/sport-definition";
import { ringball } from "@/test/sport-definitions";
import { ScorerPadPreview } from "./scorer-pad-preview";

afterEach(cleanup);

const groups = () =>
  screen.getAllByRole("region").map((region) => ({
    label: within(region).getByRole("heading").textContent,
    buttons: within(region)
      .getAllByRole("listitem")
      .map((item) => item.textContent),
  }));

describe("scorer pad preview", () => {
  it("shows the definition's groups in order, each button labelled by its event", () => {
    render(<ScorerPadPreview definition={ringball} />);

    expect(groups()).toEqual([
      { label: "Rings", buttons: ["Ring", "Long ring", "Ring voided"] },
      { label: "Conduct", buttons: ["Warning card", "Ringer swap"] },
      { label: "Ring-off", buttons: ["Ring-off hit", "Ring-off miss"] },
      // A generic event on the pad takes its generic label.
      { label: "Fine detail", buttons: ["Turnover", "Note"] },
    ]);
  });

  it("says when a group shows, from the definition's period labels and coverage", () => {
    render(<ScorerPadPreview definition={ringball} />);

    expect(within(screen.getByRole("region", { name: "Ring-off" })).getByText("Only during Ring-off")).toBeTruthy();
    expect(within(screen.getByRole("region", { name: "Fine detail" })).getByText("From detailed coverage")).toBeTruthy();
    expect(within(screen.getByRole("region", { name: "Rings" })).queryByText(/Only during|coverage/)).toBeNull();
  });

  it("uses the definition's word for a contest and records nothing", () => {
    render(<ScorerPadPreview definition={ringball} />);

    expect(screen.getByText(/What a scorer sees during a bout/)).toBeTruthy();
    expect(screen.queryAllByRole("button")).toEqual([]);
  });

  it("follows the definition: other groups and labels give another pad", () => {
    const changed: SportDefinition = {
      ...ringball,
      eventTypes: ringball.eventTypes.map((event) => (event.code === "RING" ? { ...event, label: "Clean ring" } : event)),
      scorerPad: { kind: "BUTTONS", groups: [{ label: "Only rings", buttons: ["LONG_RING", "RING"], phases: [], coverage: null }] },
    };
    render(<ScorerPadPreview definition={changed} />);

    expect(groups()).toEqual([{ label: "Only rings", buttons: ["Long ring", "Clean ring"] }]);
  });

  it("says a pad kind it can't draw isn't available, instead of drawing one", () => {
    render(<ScorerPadPreview definition={{ ...ringball, scorerPad: { kind: "DELIVERY_PAD", groups: [] } }} />);

    expect(screen.getByText("Preview not available")).toBeTruthy();
    expect(screen.getByText(/delivery pad/)).toBeTruthy();
    expect(screen.queryAllByRole("listitem")).toEqual([]);
  });
});
