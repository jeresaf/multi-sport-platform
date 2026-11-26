"use client";

import Link from "next/link";
import { useState, type ReactNode } from "react";
import { Controller, useWatch, type UseFormReturn } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { ColourSwatch, hexColour } from "@/components/registry/colour-swatch";
import { RecordSearchField } from "@/components/registry/record-search-field";
import { SelectField } from "@/components/select-field";
import { humanizeCode } from "@/lib/codes";
import {
  ageGroupLabel,
  ageGroups,
  genderCategories,
  genderCategoryLabel,
  genderLabel,
  teamLevelLabel,
  teamLevels,
  type AgeGroup,
} from "@/lib/team-category";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { choosableVenue } from "../organisations/primary-venue-field";
import { limits, type TeamValues } from "./team-form";
import { useHomeVenueChoices, useOrganisationChoices, type Team } from "./teams-data";

function Section({ title, children }: Readonly<{ title: string; children: ReactNode }>) {
  return (
    <fieldset className="grid gap-4 rounded-md border p-4">
      <legend className="px-1 font-semibold">{title}</legend>
      {children}
    </fieldset>
  );
}

/** A sport the tenant has enabled, named from its definition (AR-29). */
export type SportChoice = { variant: string; name: string };

type VenueItem = {
  id: string;
  name: string;
  status: string;
  type: string;
  city: string | null;
  supportedVariants: string[];
};

/** Whether an organisation may take a new team: not dissolved or archived (D6, `TEAM_ORGANISATION_UNAVAILABLE`). */
const choosableOrganisation = (status: string) => status !== "DISSOLVED" && status !== "ARCHIVED";

const colourFields = [
  ["primaryColour", "Primary colour (optional)"],
  ["secondaryColour", "Secondary colour (optional)"],
  ["tertiaryColour", "Third colour (optional)"],
] as const;

/**
 * The team's details, Add team's and Edit details' (S07 D6, D7, D23): its sport, names and category, the organisation
 * it belongs to and its home venue, and its colours, code and motto. Add chooses the sport from the enabled ones and
 * the day the name was first used; Edit shows the sport and the name (another sport is another team; a new name is a
 * rename, D8) and adds the web address. The duplicate warning goes under the fields it compares.
 */
export function TeamFields({
  form,
  sports,
  team,
  presetOrganisation,
  renameHref,
  warning,
}: Readonly<{
  form: UseFormReturn<TeamValues>;
  /** Add from an organisation's Teams tab: that organisation, chosen to start with. */
  presetOrganisation?: { id: string; name: string } | null;
  /** Add: the enabled sports to choose from. */
  sports: readonly SportChoice[];
  /** Edit: the team as saved. */
  team?: Team;
  /** Edit: Rename's page, when the user may rename the team. */
  renameHref?: string | null;
  warning?: ReactNode;
}>) {
  const { can } = useTenantWorkspace();
  const { register, control } = form;
  const errors = form.formState.errors;
  const edit = team !== undefined;
  const [variant, ageGroup, ...colours] = useWatch({
    control,
    name: ["variant", "ageGroup", "primaryColour", "secondaryColour", "tertiaryColour"],
  });
  // The venue last chosen from the search (its sports are known); the saved one's aren't read (`TeamVenueResponse`).
  const [chosenVenue, setChosenVenue] = useState<VenueItem | null>(null);
  const sportName = (code: string) => sports.find((sport) => sport.variant === code)?.name ?? humanizeCode(code);
  const youthAware = (ageGroups as readonly string[]).includes(ageGroup);

  return (
    <div className="grid gap-5">
      <Section title="The team">
        {edit ? (
          <dl className="grid gap-4 tablet:grid-cols-2">
            <div className="grid gap-1">
              <dt className="text-sm font-medium">Sport</dt>
              <dd className="break-words">{sportName(team.variant)}</dd>
              <dd className="text-sm text-muted-foreground">A team in another sport is another team.</dd>
            </div>
            <div className="grid gap-1">
              <dt className="text-sm font-medium">Name</dt>
              <dd className="break-words">{team.name}</dd>
              <dd className="text-sm text-muted-foreground">
                {renameHref ? (
                  <>
                    A new name is a{" "}
                    <Link href={renameHref} className="text-primary underline-offset-4 hover:underline">
                      rename
                    </Link>
                    : the old name stays on earlier results.
                  </>
                ) : (
                  "A new name is a rename: the old name stays on earlier results."
                )}
              </dd>
            </div>
          </dl>
        ) : (
          <>
            <SelectField id="team-sport" label="Sport" error={errors.variant?.message} {...register("variant")}>
              {sports.length === 1 ? null : <option value="">Choose…</option>}
              {sports.map((sport) => (
                <option key={sport.variant} value={sport.variant}>
                  {sport.name}
                </option>
              ))}
            </SelectField>
            <FormField
              id="team-name"
              label="Name"
              hint="The name it plays under, such as Ntinda Swifts FC."
              autoComplete="off"
              maxLength={limits.name}
              error={errors.name?.message}
              {...register("name")}
            />
          </>
        )}
        <div className="grid gap-4 tablet:grid-cols-2">
          <FormField
            id="team-short-name"
            label="Short name (optional)"
            hint="For tight spaces such as fixtures lists."
            autoComplete="off"
            maxLength={limits.shortName}
            error={errors.shortName?.message}
            {...register("shortName")}
          />
          <FormField
            id="team-code"
            label="Code (optional)"
            hint="Three letters for scoreboards, such as NTS."
            autoComplete="off"
            autoCapitalize="characters"
            className="uppercase"
            maxLength={3}
            error={errors.code?.message}
            {...register("code")}
          />
        </div>
        <div className="grid gap-4 tablet:grid-cols-3">
          <SelectField id="team-age-group" label="Age group" error={errors.ageGroup?.message} {...register("ageGroup")}>
            <option value="">Choose…</option>
            {ageGroups.map((code) => (
              <option key={code} value={code}>
                {ageGroupLabel(code)}
              </option>
            ))}
          </SelectField>
          <SelectField
            id="team-gender"
            label="Gender"
            error={errors.genderCategory?.message}
            {...register("genderCategory")}
          >
            <option value="">Choose…</option>
            {genderCategories.map((code) => (
              <option key={code} value={code}>
                {/* "Boys" and "Girls" in the youth age groups (Q53), both words until one is chosen. */}
                {youthAware ? genderCategoryLabel(code, ageGroup as AgeGroup) : genderLabel(code)}
              </option>
            ))}
          </SelectField>
          <SelectField id="team-level" label="Level" error={errors.level?.message} {...register("level")}>
            {teamLevels.map((code) => (
              <option key={code} value={code}>
                {teamLevelLabel(code)}
              </option>
            ))}
          </SelectField>
        </div>
        <div className="grid gap-4 tablet:grid-cols-2">
          <FormField
            id="team-founded"
            label="Founded (optional)"
            hint="The year, such as 1998."
            inputMode="numeric"
            autoComplete="off"
            maxLength={4}
            error={errors.foundedYear?.message}
            {...register("foundedYear")}
          />
          {edit ? null : (
            <FormField
              id="team-name-since"
              label="Name used since (optional)"
              hint="The first day it played under this name. Left empty, today."
              type="date"
              error={errors.nameSince?.message}
              {...register("nameSince")}
            />
          )}
        </div>
      </Section>

      <Section title="Organisation and ground">
        <Controller
          control={control}
          name="organisationId"
          render={({ field, fieldState }) => (
            <RecordSearchField
              id="team-organisation"
              noun="organisation"
              legend="Organisation (optional)"
              hint={`The club, school or other body it belongs to; none for an independent team. Dissolved organisations can't be chosen${team?.organisation ? ", except the one it already has." : "."}`}
              value={field.value}
              onChange={(id) => field.onChange(id)}
              saved={
                team?.organisation
                  ? {
                      id: team.organisation.id,
                      name: team.organisation.displayName,
                    }
                  : (presetOrganisation ?? null)
              }
              error={fieldState.error?.message}
              canSearch={can("organisation.read")}
              noAccess="Choosing an organisation needs access to the organisations."
              useChoices={useOrganisationChoices}
              nameOf={(item) => item.displayName}
              describe={(item) =>
                choosableOrganisation(item.status)
                  ? {
                      available: true,
                      detail: [humanizeCode(item.type), item.city].filter(Boolean).join(" · "),
                    }
                  : {
                      available: false,
                      detail: "Dissolved: it can't take a team.",
                    }
              }
              clearLabel="Independent team"
              chosenMessage={(name) => `${name} chosen as the organisation.`}
            />
          )}
        />
        <Controller
          control={control}
          name="homeVenueId"
          render={({ field, fieldState }) => (
            <RecordSearchField<VenueItem>
              id="team-home-venue"
              noun="venue"
              legend="Home venue (optional)"
              hint={`Where it usually plays at home. Closed venues can't be chosen${team?.homeVenue ? ", except the one it already has." : "."}`}
              value={field.value}
              onChange={(id, chosen) => {
                field.onChange(id);
                setChosenVenue(chosen);
              }}
              saved={team?.homeVenue ?? null}
              error={fieldState.error?.message}
              canSearch={can("venue.read")}
              noAccess="Choosing a venue needs access to the venues."
              useChoices={useHomeVenueChoices}
              nameOf={(item) => item.name}
              describe={(item) =>
                choosableVenue(item.status)
                  ? {
                      available: true,
                      detail: [humanizeCode(item.type), item.city].filter(Boolean).join(" · "),
                    }
                  : {
                      available: false,
                      detail: "Closed: it can't be a home venue.",
                    }
              }
              clearLabel="No home venue"
              chosenMessage={(name) => `${name} chosen as the home venue.`}
              // A venue that doesn't list the sport is accepted (D6): the form says so.
              note={
                chosenVenue &&
                chosenVenue.id === field.value &&
                variant &&
                !chosenVenue.supportedVariants.includes(variant)
                  ? `${chosenVenue.name} doesn't list ${sportName(variant)} among the sports it hosts. It can still be the home venue.`
                  : null
              }
            />
          )}
        />
      </Section>

      {warning}

      <Section title="Colours and motto">
        <div className="grid gap-4 tablet:grid-cols-3">
          {colourFields.map(([fieldName, label], index) => {
            const value = colours[index]?.trim() ?? "";
            return (
              <div key={fieldName} className="grid gap-2">
                <FormField
                  id={`team-${fieldName}`}
                  label={label}
                  hint="As # and six digits or letters A–F."
                  autoComplete="off"
                  autoCapitalize="characters"
                  maxLength={7}
                  error={errors[fieldName]?.message}
                  {...register(fieldName)}
                />
                {/* The swatch repeats what is typed: hidden from screen readers, which hear the field itself. */}
                {hexColour.test(value) ? (
                  <div aria-hidden="true">
                    <ColourSwatch colour={value} />
                  </div>
                ) : null}
              </div>
            );
          })}
        </div>
        <FormField
          id="team-motto"
          label="Motto (optional)"
          autoComplete="off"
          maxLength={limits.motto}
          error={errors.motto?.message}
          {...register("motto")}
        />
        {edit ? (
          <FormField
            id="team-slug"
            label="Web address"
            hint="The last part of the team's address on the public site, such as ntinda-swifts-fc."
            autoComplete="off"
            autoCapitalize="none"
            error={errors.slug?.message}
            {...register("slug")}
          />
        ) : null}
      </Section>
    </div>
  );
}
