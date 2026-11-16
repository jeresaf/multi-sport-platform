// The viewer's tabs say "None" for an empty list instead of drawing a table with no rows.
import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { ringball } from "@/test/sport-definitions";
import { PeriodsTab, PositionsTab, StatisticsTab } from "./definition-tabs";
import { EventsTab } from "./events-tab";

afterEach(cleanup);

const empty = { ...ringball, periods: [], positions: [], actorRoles: {}, officialRoles: [], staffRoles: [], statistics: [] };

describe("definition tabs", () => {
  it("write each kind of event detail in words", () => {
    const ring = ringball.eventTypes.find((event) => event.code === "RING")!;
    const metadata = {
      note: { type: "TEXT" as const, values: [], min: null, max: null, optional: true },
      distance: { type: "NUMBER" as const, values: [], min: null, max: null, optional: false },
      spin: { type: "NUMBER" as const, values: [], min: 1, max: 5, optional: false },
    };
    render(<EventsTab definition={{ ...ringball, eventTypes: [{ ...ring, metadata }] }} />);

    expect(screen.getByText("Note: text (optional); Distance: a number; Spin: a number 1–5")).toBeTruthy();
  });

  it("say None for empty lists", () => {
    render(
      <>
        <PeriodsTab definition={empty} />
        <PositionsTab definition={empty} />
        <StatisticsTab definition={empty} />
      </>,
    );

    expect(screen.queryAllByRole("table")).toEqual([]);
    expect(screen.getAllByText("None")).toHaveLength(6);
    for (const title of ["Positions", "Roles in events", "Staff roles", "Officials"]) {
      expect(within(screen.getByRole("heading", { name: title }).parentElement!).getByText("None")).toBeTruthy();
    }
  });
});
