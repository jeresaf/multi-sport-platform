"use client";

// A sport variant's definition, read-only (S05 D17): a header, then the tabs Overview, Periods, Events, Positions &
// roles, Statistics and Scorer pad. It takes the definition rather than reading it, so the tenant workspace and the
// platform console (T5b2) show the same viewer over their own reads.
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@msp/ui/components/tabs";
import type { ReactNode } from "react";
import { formatDate } from "@/lib/tenant-settings";
import { codeLabel, type SportDefinition } from "@/lib/sport-definition";
import { OverviewTab, PeriodsTab, PositionsTab, StatisticsTab } from "./definition-tabs";
import { EventsTab } from "./events-tab";
import { ScorerPadPreview } from "./scorer-pad-preview";

export const sportTabs = [
  { value: "overview", label: "Overview", content: OverviewTab },
  { value: "periods", label: "Periods", content: PeriodsTab },
  { value: "events", label: "Events", content: EventsTab },
  { value: "positions", label: "Positions & roles", content: PositionsTab },
  { value: "statistics", label: "Statistics", content: StatisticsTab },
  { value: "scorer-pad", label: "Scorer pad", content: ScorerPadPreview },
] as const;

export type SportTab = (typeof sportTabs)[number]["value"];

/** A tab from the address (`?tab=`); the overview for a missing or unknown one. */
export function sportTab(value: string | null | undefined): SportTab {
  return sportTabs.find((tab) => tab.value === value)?.value ?? "overview";
}

export function SportViewer({
  definition,
  tab,
  onTabChange,
  timeZone,
  back,
}: Readonly<{
  definition: SportDefinition;
  tab: SportTab;
  onTabChange: (tab: SportTab) => void;
  /** Where the publication date is shown: the tenant's zone in a workspace. */
  timeZone: string;
  /** A link back to the list, above the title. */
  back?: ReactNode;
}>) {
  return (
    // `minmax(0, 1fr)`: grid tracks would otherwise grow to the tab list's or a table's width and widen the page on a phone.
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="grid gap-2">
        {back}
        <h1 className="text-2xl font-semibold">{definition.name}</h1>
        <p className="text-muted-foreground">
          {codeLabel(definition.sport)} · version {definition.version} · published{" "}
          {formatDate(definition.publishedAt, timeZone)}
        </p>
        <p className="text-sm text-muted-foreground">
          Read-only: a sport changes only by publishing a new version. Content hash{" "}
          <code className="font-mono text-xs break-all">{definition.contentHash}</code>
        </p>
      </header>

      <Tabs value={tab} onValueChange={(value) => onTabChange(sportTab(value))} className="min-w-0">
        <TabsList aria-label="Definition">
          {sportTabs.map(({ value, label }) => (
            <TabsTrigger key={value} value={value}>
              {label}
            </TabsTrigger>
          ))}
        </TabsList>
        {sportTabs.map(({ value, label, content: Content }) => (
          <TabsContent key={value} value={value} className="grid grid-cols-[minmax(0,1fr)] gap-4">
            <h2 className="sr-only">{label}</h2>
            <Content definition={definition} />
          </TabsContent>
        ))}
      </Tabs>
    </section>
  );
}
