"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@msp/ui/components/tabs";
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import type { ReactNode } from "react";
import { Loading } from "@/components/loading";
import { AgeBadge } from "@/components/people/person-badges";
import { PersonPhoto } from "@/components/people/person-photo";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { BackLink } from "@/components/viewer/facts";
import { isApiProblem } from "@/lib/api";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { usePerson, usePersonPrivate, type Person, type PersonPrivate } from "../people-data";
import { ConsentTab, showsConsent } from "./consent-tab";
import { IdentifiersTab } from "./identifiers-tab";
import { OverviewTab } from "./overview-tab";
import { RolesTab } from "./roles-tab";

type TabProps = { person: Person; privateDetails: PersonPrivate | undefined };

const personTabs = [
  { value: "overview", label: "Overview", content: OverviewTab },
  { value: "roles", label: "Roles", content: RolesTab },
  { value: "consent", label: "Consent", content: ConsentTab },
  { value: "identifiers", label: "External IDs & aliases", content: IdentifiersTab },
] as const satisfies ReadonlyArray<{ value: string; label: string; content: (props: TabProps) => ReactNode }>;

type PersonTab = (typeof personTabs)[number]["value"];

/**
 * Registry → People → a person (S06 T7, admin-app.md): the header (photo, display name, status, Minor or Age unknown),
 * then the tabs Overview, Roles, Consent (only when it applies) and External IDs & aliases. The tab is in the address
 * (`?tab=`). Read-only until T7c's changes.
 */
export default function PersonPage() {
  const { personId } = useParams<{ personId: string }>();
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const { tenant, can } = useTenantWorkspace();
  const person = usePerson(personId);
  const privateRead = usePersonPrivate(personId);

  if (!can("person.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(person.error, "PERSON_NOT_FOUND")) {
    return <NotFoundView />;
  }

  const back = <BackLink href={`${tenantPath(tenant.slug)}/people`}>People</BackLink>;
  if (!person.data) {
    return (
      <section className="grid gap-4">
        {back}
        {person.isError ? <ProblemAlert error={person.error} /> : <Loading />}
      </section>
    );
  }

  const data = person.data;
  const tabs = personTabs.filter((tab) => tab.value !== "consent" || showsConsent(data));
  const tab: PersonTab = tabs.find((item) => item.value === searchParams.get("tab"))?.value ?? "overview";
  const changeTab = (value: string) => {
    const next = new URLSearchParams(searchParams.toString());
    next.set("tab", value);
    router.replace(`${pathname}?${next.toString()}`, { scroll: false });
  };
  const privateDetails = privateRead.data;

  return (
    // `minmax(0, 1fr)`: grid tracks would otherwise grow to the tab list's or a table's width and widen the page on a phone.
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="grid gap-3">
        {back}
        <div className="flex flex-wrap items-center gap-4">
          <PersonPhoto displayName={data.displayName} url={data.photo?.mediumUrl} />
          <div className="grid min-w-0 gap-2">
            <h1 className="text-2xl font-semibold break-words">{data.displayName}</h1>
            <div className="flex flex-wrap items-center gap-2">
              <StatusBadge status={data.status} />
              <AgeBadge ageStatus={data.ageStatus} />
              <span className="text-sm text-muted-foreground">
                Public profile {data.publicProfileEnabled ? "on" : "off"}
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* Without the private read the page still shows what `person.read` does: the birth year, no guardians. */}
      {privateRead.isError ? <ProblemAlert error={privateRead.error} /> : null}

      <Tabs value={tab} onValueChange={changeTab} className="min-w-0">
        <TabsList aria-label="Person">
          {tabs.map(({ value, label }) => (
            <TabsTrigger key={value} value={value}>
              {label}
            </TabsTrigger>
          ))}
        </TabsList>
        {tabs.map(({ value, label, content: Content }) => (
          <TabsContent key={value} value={value} className="grid grid-cols-[minmax(0,1fr)] gap-6">
            <h2 className="sr-only">{label}</h2>
            <Content person={data} privateDetails={privateDetails} />
          </TabsContent>
        ))}
      </Tabs>
    </section>
  );
}
