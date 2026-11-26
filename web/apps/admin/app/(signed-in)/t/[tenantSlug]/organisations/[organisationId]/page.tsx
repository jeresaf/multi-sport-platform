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
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useOrganisation, type Organisation } from "../organisations-data";
import { HistoryTab } from "./history-tab";
import { LogoControl } from "./logo-control";
import { NamesTab } from "./names-tab";
import { OrganisationActions, organisationStatusId } from "./organisation-actions";
import { OverviewTab } from "./overview-tab";
import { TeamsTab } from "./teams-tab";

const organisationTabs = [
  { value: "overview", label: "Overview", content: OverviewTab },
  { value: "teams", label: "Teams", content: TeamsTab },
  { value: "history", label: "History", content: HistoryTab },
  { value: "names", label: "Names & IDs", content: NamesTab },
] as const satisfies ReadonlyArray<{
  value: string;
  label: string;
  content: (props: { organisation: Organisation }) => ReactNode;
}>;

type OrganisationTab = (typeof organisationTabs)[number]["value"];

/**
 * Registry → Organisations → an organisation (S07 D23): the header (logo, name, status, type and city, the status
 * moves and Edit details, the logo control), then the tabs Overview (with the parent and member organisations), Teams,
 * History and Names & IDs (aliases and external identifiers). The tab is in the address (`?tab=`).
 */
export default function OrganisationPage() {
  const { organisationId } = useParams<{ organisationId: string }>();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const { tenant, can } = useTenantWorkspace();
  const organisation = useOrganisation(organisationId);

  if (!can("organisation.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(organisation.error, "ORGANISATION_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = <BackLink href={`${tenantPath(tenant.slug)}/organisations`}>Organisations</BackLink>;
  if (!organisation.data) {
    return (
      <section className="grid gap-4">
        {back}
        {organisation.isError ? <ProblemAlert error={organisation.error} /> : <Loading />}
      </section>
    );
  }

  const data = organisation.data;
  const tab: OrganisationTab = organisationTabs.find((item) => item.value === searchParams.get("tab"))?.value ?? "overview";
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
          <RecordLogo name={data.displayName} url={data.logo?.mediumUrl} />
          <div className="grid min-w-0 gap-2">
            <h1 className="text-2xl font-semibold break-words">{data.displayName}</h1>
            <div className="flex flex-wrap items-center gap-2">
              <span
                id={organisationStatusId}
                tabIndex={-1}
                className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <StatusBadge status={data.status} />
              </span>
              <span className="text-muted-foreground">
                {[humanizeCode(data.type), data.city].filter(Boolean).join(" · ")}
              </span>
            </div>
          </div>
        </div>
        <OrganisationActions organisation={data} />
        <LogoControl organisation={data} />
      </header>

      <Tabs value={tab} onValueChange={changeTab} className="min-w-0">
        <TabsList aria-label="Organisation">
          {organisationTabs.map(({ value, label }) => (
            <TabsTrigger key={value} value={value}>
              {label}
            </TabsTrigger>
          ))}
        </TabsList>
        {organisationTabs.map(({ value, label, content: Content }) => (
          <TabsContent key={value} value={value} className="grid grid-cols-[minmax(0,1fr)] gap-6">
            <h2 className="sr-only">{label}</h2>
            <Content organisation={data} />
          </TabsContent>
        ))}
      </Tabs>
    </section>
  );
}
