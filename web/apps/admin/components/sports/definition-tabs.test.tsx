// The viewer's tabs say "None" for an empty list instead of drawing a table with no rows.
import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { ringball } from "@/test/sport-definitions";
import { PeriodsTab, PositionsTab, StatisticsTab } from "./definition-tabs";

afterEach(cleanup);

const empty = { ...ringball, periods: [], positions: [], actorRoles: {}, officialRoles: [], staffRoles: [], statistics: [] };

describe("definition tabs", () => {
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
