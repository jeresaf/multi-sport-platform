"use client";

import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { actionAccess } from "@/lib/action-access";
import { call } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { acceptsNewChildren } from "../../organisations/organisations-data";
import { useTenantSports } from "../../settings/sports/sports-settings-data";
import { useSportVariants } from "../../sports/sports-data";
import { TeamEditor, type TeamSave } from "../team-editor";
import { emptyTeam, toCreateRequest } from "../team-form";
import { addTeamActions, usePresetOrganisation, useKeepTeam } from "../teams-data";

/**
 * Registry → Teams → Add team (S07 D6, D7, D8, D13, D23): the team's details with the live duplicate warning. The sport
 * is one of the tenant's enabled sports (D6), named from its definition (AR-29); with none enabled there is nothing to
 * add yet. The web address is proposed by the server from the name and category (D9). Opened from an organisation's
 * Teams tab (`?organisationId=`), that organisation is chosen to start with. Saved, it opens the new team. Aliases,
 * identifiers and images are added on its page.
 */
export default function AddTeamPage() {
  const { tenant, client, can, unavailable } = useTenantWorkspace();
  const router = useRouter();
  const searchParams = useSearchParams();
  const keep = useKeepTeam();
  const { allowed, reason } = actionAccess(addTeamActions, can, unavailable);
  const root = tenantPath(tenant.slug);
  const listHref = `${root}/teams`;
  const tenantSports = useTenantSports(allowed);
  const published = useSportVariants();
  const preset = usePresetOrganisation(searchParams.get("organisationId") ?? "");
  const save = useMutation({
    mutationFn: ({ values, decision }: TeamSave) =>
      call(
        client.POST("/api/admin/v1/teams", {
          body: toCreateRequest(values, decision),
        }),
      ),
    onSuccess: (team) => {
      keep(team);
      router.push(`${listHref}/${team.id}`);
    },
  });

  if (!allowed && !reason) {
    return <ForbiddenView />;
  }
  const enabled = (tenantSports.data?.variants ?? []).filter((variant) => variant.enabled);
  const sports = enabled.map(({ variant, name }) => ({ variant, name }));
  const sportName = (code: string) =>
    published.data?.items.find((item) => item.variant === code)?.name ??
    sports.find((sport) => sport.variant === code)?.name ??
    humanizeCode(code);
  // The organisation chosen to start with, once read; an unknown one, or one that can't take a team (D6), none.
  const presetOrganisation =
    preset.data && acceptsNewChildren(preset.data.status)
      ? { id: preset.data.id, name: preset.data.displayName }
      : null;
  const waiting = tenantSports.isPending || (preset.wanted && preset.isPending);

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={listHref}>Teams</BackLink>
      <header className="grid gap-1">
        <h1 className="text-2xl font-semibold">Add team</h1>
        <p className="text-muted-foreground">
          A team in one of your sports, in one category. Its names, sport, category and organisation are checked against
          the teams you already have. Other names, identifiers, the logo and the badge are added on its page.
        </p>
      </header>
      {!allowed ? (
        <UnavailableNote id="add-team-unavailable" reason={reason!} />
      ) : !can("tenant.sport.read") ? (
        <p className="text-muted-foreground">Adding a team needs access to your enabled sports.</p>
      ) : tenantSports.isError ? (
        <ProblemAlert error={tenantSports.error} />
      ) : waiting ? (
        <Loading />
      ) : sports.length === 0 ? (
        <p className="text-muted-foreground">
          No sport is enabled for your organisation yet. A team is for a sport: enable one in{" "}
          <Link href={`${root}/settings/sports`} className="text-primary underline-offset-4 hover:underline">
            Settings → Enabled sports
          </Link>
          .
        </p>
      ) : (
        <TeamEditor
          initial={emptyTeam({
            variant: sports.length === 1 ? sports[0]!.variant : "",
            organisationId: presetOrganisation?.id ?? "",
          })}
          sports={sports}
          sportName={sportName}
          presetOrganisation={presetOrganisation}
          save={save}
          submitLabel="Add team"
          pendingLabel="Adding…"
          cancelHref={presetOrganisation ? `${root}/organisations/${presetOrganisation.id}?tab=teams` : listHref}
        />
      )}
    </section>
  );
}
