"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@msp/ui/components/tabs";
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import type { ReactNode } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { RecordLogo } from "@/components/registry/record-logo";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { teamCategoryLabel } from "@/lib/team-category";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportVariants } from "../../sports/sports-data";
import { useTeam, type Team } from "../teams-data";
import { HistoryTab } from "./history-tab";
import { OverviewTab } from "./overview-tab";
import { TeamActions, teamStatusId } from "./team-actions";

// Players and Staff are S08's (team memberships): their tabs aren't shown until then (non-negotiable 13). Names & IDs
// comes with T6c-2b.
const teamTabs = [
  { value: "overview", label: "Overview", content: OverviewTab },
  { value: "history", label: "History", content: HistoryTab },
] as const satisfies ReadonlyArray<{
  value: string;
  label: string;
  content: (props: { team: Team }) => ReactNode;
}>;

type TeamTab = (typeof teamTabs)[number]["value"];

/**
 * Registry → Teams → a team (S07 D23): the header (logo or initials, name, status, category and sport, the status
 * moves), then the tabs Overview (category, organisation, home venue, colours, code, motto) and History (status and
 * former names). The tab is in the address (`?tab=`).
 */
export default function TeamPage() {
  const { teamId } = useParams<{ teamId: string }>();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const { tenant, can } = useTenantWorkspace();
  const team = useTeam(teamId);
  const sports = useSportVariants();

  if (!can("team.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(team.error, "TEAM_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = <BackLink href={`${tenantPath(tenant.slug)}/teams`}>Teams</BackLink>;
  if (!team.data) {
    return (
      <section className="grid gap-4">
        {back}
        {team.isError ? <ProblemAlert error={team.error} /> : <Loading />}
      </section>
    );
  }

  const data = team.data;
  const sport = sports.data?.items.find((item) => item.variant === data.variant)?.name ?? humanizeCode(data.variant);
  const tab: TeamTab = teamTabs.find((item) => item.value === searchParams.get("tab"))?.value ?? "overview";
  const changeTab = (value: string) => {
    const next = new URLSearchParams(searchParams.toString());
    next.set("tab", value);
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  };

  return (
    // `minmax(0, 1fr)`: grid tracks would otherwise grow to the tab list's or a table's width and widen the page on a phone.
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="grid gap-3">
        {back}
        <div className="flex min-w-0 items-center gap-4">
          <RecordLogo name={data.name} url={data.logo?.mediumUrl} />
          <div className="grid min-w-0 gap-2">
            <h1 className="text-2xl font-semibold break-words">{data.name}</h1>
            <div className="flex flex-wrap items-center gap-2">
              <span
                id={teamStatusId}
                tabIndex={-1}
                className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <StatusBadge status={data.status} />
              </span>
              <span className="text-muted-foreground">{`${teamCategoryLabel(data)} · ${sport}`}</span>
            </div>
          </div>
        </div>
        <TeamActions team={data} />
      </header>

      <Tabs value={tab} onValueChange={changeTab} className="min-w-0">
        <TabsList aria-label="Team">
          {teamTabs.map(({ value, label }) => (
            <TabsTrigger key={value} value={value}>
              {label}
            </TabsTrigger>
          ))}
        </TabsList>
        {teamTabs.map(({ value, label, content: Content }) => (
          <TabsContent key={value} value={value} className="grid grid-cols-[minmax(0,1fr)] gap-6">
            <h2 className="sr-only">{label}</h2>
            <Content team={data} />
          </TabsContent>
        ))}
      </Tabs>
    </section>
  );
}
