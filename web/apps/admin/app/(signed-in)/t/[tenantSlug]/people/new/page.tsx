"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Plus } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { EmptyState } from "@/components/empty-state";
import { FormField } from "@/components/form-field";
import { Loading } from "@/components/loading";
import { AgeBadge } from "@/components/people/person-badges";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { StatusBadge } from "@/components/status-badge";
import { ForbiddenView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { call, isApiProblem } from "@/lib/api";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useCountries } from "@/lib/reference-data";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { useSportLabels } from "../[personId]/sport-labels";
import {
  nameMatchLimit,
  personQueryKey,
  useNameMatches,
  type CheckDuplicatesResponse,
  type DuplicateCandidate,
  type Person,
} from "../people-data";
import { DuplicateReview } from "./duplicate-review";
import {
  apiFields,
  defaultValues,
  displayNameOf,
  emptyIdentifier,
  limits,
  nameFields,
  schema,
  toCheckRequest,
  toCreateRequest,
  type Values,
} from "./person-form";

type Step = "name" | "details" | "review";
type Review = { values: Values; checkToken: string; candidates: DuplicateCandidate[]; notice: string | null };
type Attempt = { values: Values; reviewed: Review | null; reason: string | null };
type Outcome = { kind: "created"; person: Person } | { kind: "review"; review: Review };

const steps: Readonly<Record<Step, { number: number; title: string }>> = {
  name: { number: 1, title: "Who are you adding?" },
  details: { number: 2, title: "Their details" },
  review: { number: 3, title: "Check for duplicates" },
};

/** How long the name search waits after the last key, as the directory's search does. */
const searchDelay = 300;

const staleNotice =
  "Someone added a person who may match while you were reviewing, so the check ran again. Review the list again.";
const expiredNotice = "The check was more than 30 minutes old and the list has changed. Review it again.";

const sameCandidates = (a: readonly DuplicateCandidate[], b: readonly DuplicateCandidate[]) =>
  a.length === b.length && a.every((candidate) => b.some((other) => other.personId === candidate.personId));

const dateFormat = new Intl.DateTimeFormat("en-GB", { dateStyle: "long", timeZone: "UTC" });

/**
 * Registry → People → Add person (S06 D5, D6; admin-app.md): search-first. The name first, with the people it
 * already finds; then the details; then the duplicate check, and its review when it finds anyone. A check that went stale or
 * expired before the save runs again once, by itself.
 */
export default function AddPersonPage() {
  const { tenant, client, can, unavailable } = useTenantWorkspace();
  const router = useRouter();
  const queryClient = useQueryClient();
  const countries = useCountries();
  const [step, setStep] = useState<Step>("name");
  const [review, setReview] = useState<Review | null>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const firstRender = useRef(true);

  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues, mode: "onTouched" });
  const identifierRows = useFieldArray({ control: form.control, name: "externalIdentifiers" });
  const [firstName, lastName, displayName] = useWatch({
    control: form.control,
    name: ["firstName", "lastName", "displayName"],
  });

  // The name search follows the typing once it pauses.
  const typed = `${firstName.trim()} ${lastName.trim()}`.trim();
  const [search, setSearch] = useState("");
  useEffect(() => {
    const timer = setTimeout(() => setSearch(typed), searchDelay);
    return () => clearTimeout(timer);
  }, [typed]);
  const matches = useNameMatches(step === "name" ? search : "");

  const labels = useSportLabels(review?.candidates.flatMap((candidate) => candidate.athleteVariants) ?? []);

  // Each step starts at its heading, so a screen reader hears where it is.
  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    heading.current?.focus();
  }, [step]);

  const check = (values: Values): Promise<CheckDuplicatesResponse> =>
    call(client.POST("/api/admin/v1/people/duplicate-check", { body: toCheckRequest(values) }));
  const create = (values: Values, checkToken: string, reason: string | null): Promise<Person> =>
    call(client.POST("/api/admin/v1/people", { body: toCreateRequest(values, checkToken, reason) }));

  const save = useMutation({
    mutationFn: async ({ values, reviewed, reason }: Attempt): Promise<Outcome> => {
      let checkToken: string;
      if (reviewed) {
        checkToken = reviewed.checkToken;
      } else {
        const checked = await check(values);
        if (checked.candidates.length > 0) {
          return { kind: "review", review: { values, ...checked, notice: null } };
        }
        checkToken = checked.checkToken;
      }

      try {
        return { kind: "created", person: await create(values, checkToken, reason) };
      } catch (error) {
        const stale = isApiProblem(error, "PERSON_DUPLICATE_CHECK_STALE");
        if (!stale && !isApiProblem(error, "PERSON_DUPLICATE_CHECK_REQUIRED")) {
          throw error;
        }
        // Checked again once: with no one to review, or (an expired token) the same people already reviewed with a
        // reason, the save goes ahead; otherwise the registrar reviews the new list.
        const again = await check(values);
        const confirmed = !stale && reason !== null && sameCandidates(again.candidates, reviewed?.candidates ?? []);
        if (again.candidates.length === 0 || confirmed) {
          return {
            kind: "created",
            person: await create(values, again.checkToken, again.candidates.length > 0 ? reason : null),
          };
        }
        return {
          kind: "review",
          review: { values, ...again, notice: stale ? staleNotice : expiredNotice },
        };
      }
    },
    onSuccess: (outcome) => {
      if (outcome.kind === "review") {
        setReview(outcome.review);
        setStep("review");
        return;
      }
      const { person } = outcome;
      queryClient.setQueryData(personQueryKey(tenant.tenantId, person.id), person);
      void queryClient.invalidateQueries({ queryKey: ["people", tenant.tenantId] });
      router.push(`${tenantPath(tenant.slug)}/people/${person.id}`);
    },
    onError: (error) => {
      applyFieldErrors(error, apiFields, form.setError);
      // A refused field is shown on its step; a taken identifier is changed on the details.
      const fields = isApiProblem(error) ? (error.problem.errors ?? []).map((failure) => failure.field) : [];
      if (fields.some((field) => (nameFields as readonly string[]).includes(field))) {
        setStep("name");
      } else if (
        fields.some((field) => Object.hasOwn(apiFields, field)) ||
        isApiProblem(error, "PERSON_EXTERNAL_ID_EXISTS")
      ) {
        setStep("details");
      }
    },
  });

  if (!can("person.create")) {
    const reason = unavailable("person.create");
    return reason ? (
      <section className="grid gap-4">
        <h1 className="text-2xl font-semibold">Add person</h1>
        <UnavailableNote id="add-person-unavailable" reason={reason} />
      </section>
    ) : (
      <ForbiddenView />
    );
  }

  const peopleHref = `${tenantPath(tenant.slug)}/people`;
  const personHref = (id: string) => `${peopleHref}/${id}`;
  const { errors } = form.formState;
  const name = displayNameOf({ firstName, lastName, displayName });
  const countryName = (code: string) =>
    code ? (countries.data?.items.find((country) => country.code === code)?.name ?? code) : "Not recorded";
  const matchItems = matches.data?.items ?? [];

  const goToDetails = async () => {
    if (await form.trigger([...nameFields])) {
      save.reset();
      setStep("details");
    }
  };
  const submitDetails = form.handleSubmit((values) => save.mutate({ values, reviewed: null, reason: null }));

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <Link
        href={peopleHref}
        className="flex items-center gap-1 justify-self-start text-sm text-primary underline-offset-4 hover:underline"
      >
        <ArrowLeft aria-hidden="true" className="size-4" />
        People
      </Link>
      <header className="grid gap-1">
        <p className="text-sm text-muted-foreground">
          Add person · Step {steps[step].number} of 3
        </p>
        <h1 ref={heading} tabIndex={-1} className="text-2xl font-semibold outline-none">
          {steps[step].title}
        </h1>
      </header>

      {save.isError && needsAlert(save.error, apiFields) ? <ProblemAlert error={save.error} /> : null}

      {step === "name" ? (
        <form
          className="grid gap-5"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            void goToDetails();
          }}
        >
          <p className="text-muted-foreground">
            Start with their name: if they are already registered, open their record instead of adding them again.
          </p>
          <div className="grid items-start gap-4 tablet:grid-cols-2">
            <FormField
              id="firstName"
              label="First name"
              autoComplete="off"
              error={errors.firstName?.message}
              {...form.register("firstName")}
            />
            <FormField
              id="lastName"
              label="Last name"
              autoComplete="off"
              error={errors.lastName?.message}
              {...form.register("lastName")}
            />
            <FormField
              id="middleName"
              label="Middle name (optional)"
              autoComplete="off"
              error={errors.middleName?.message}
              {...form.register("middleName")}
            />
            <FormField
              id="otherNames"
              label="Other names (optional)"
              hint="A clan name, say."
              autoComplete="off"
              error={errors.otherNames?.message}
              {...form.register("otherNames")}
            />
          </div>

          <section className="grid gap-3" aria-labelledby="matches-heading" aria-live="polite">
            <h2 id="matches-heading" className="text-lg font-semibold">
              Already registered
            </h2>
            {search.length < 2 ? (
              <p className="text-sm text-muted-foreground">People with a similar name show here as you type.</p>
            ) : matches.isPending ? (
              <Loading />
            ) : matches.isError ? (
              <ProblemAlert error={matches.error} title="The search didn't work" />
            ) : matchItems.length === 0 ? (
              <EmptyState title="No one with this name yet">Continue to add them.</EmptyState>
            ) : (
              <>
                <ul className="grid gap-2">
                  {matchItems.map((person) => (
                    <li key={person.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-md border p-3">
                      <Link
                        href={personHref(person.id)}
                        className="break-words text-primary underline-offset-4 hover:underline"
                      >
                        {person.displayName}
                      </Link>
                      <span className="text-sm text-muted-foreground">Born {person.birthYear ?? "not recorded"}</span>
                      <span className="text-sm text-muted-foreground">{countryName(person.nationality ?? "")}</span>
                      <AgeBadge ageStatus={person.ageStatus} />
                      <StatusBadge status={person.status} />
                    </li>
                  ))}
                </ul>
                {matches.data?.page.hasMore ? (
                  <p className="text-sm text-muted-foreground">
                    The first {nameMatchLimit} are shown: type more of the name to narrow them down.
                  </p>
                ) : null}
              </>
            )}
          </section>

          <Button type="submit" className="justify-self-start">
            Continue
          </Button>
        </form>
      ) : null}

      {step === "details" ? (
        <form className="grid gap-5" noValidate onSubmit={submitDetails}>
          <p className="text-muted-foreground">
            Only first and last name are needed: add what you know. {name} is checked against everyone registered
            before being added.
          </p>
          {countries.isError ? (
            <ProblemAlert error={countries.error} title="The list of countries didn't load" />
          ) : null}
          <div className="grid items-start gap-4 tablet:grid-cols-2">
            <FormField
              id="displayName"
              label="Display name (optional)"
              hint={`How they are named on screens and team sheets. Without one: ${typed}.`}
              autoComplete="off"
              error={errors.displayName?.message}
              {...form.register("displayName")}
            />
            <SelectField id="gender" label="Gender" error={errors.gender?.message} {...form.register("gender")}>
              <option value="">Not recorded</option>
              <option value="FEMALE">Female</option>
              <option value="MALE">Male</option>
            </SelectField>
            <FormField
              id="dateOfBirth"
              label="Date of birth (optional)"
              type="date"
              hint="Under 18s are kept private until a guardian agrees."
              error={errors.dateOfBirth?.message}
              {...form.register("dateOfBirth")}
            />
            <SelectField
              id="nationality"
              label="Nationality"
              error={errors.nationality?.message}
              {...form.register("nationality")}
            >
              <option value="">Not recorded</option>
              {(countries.data?.items ?? []).map((country) => (
                <option key={country.code} value={country.code}>
                  {country.name}
                </option>
              ))}
            </SelectField>
            <SelectField
              id="secondaryNationality"
              label="Second nationality"
              error={errors.secondaryNationality?.message}
              {...form.register("secondaryNationality")}
            >
              <option value="">None</option>
              {(countries.data?.items ?? []).map((country) => (
                <option key={country.code} value={country.code}>
                  {country.name}
                </option>
              ))}
            </SelectField>
            <FormField
              id="heightCm"
              label="Height in cm (optional)"
              inputMode="numeric"
              error={errors.heightCm?.message}
              {...form.register("heightCm")}
            />
            <FormField
              id="weightKg"
              label="Weight in kg (optional)"
              inputMode="decimal"
              error={errors.weightKg?.message}
              {...form.register("weightKg")}
            />
          </div>

          <fieldset className="grid gap-3">
            <legend className="text-lg font-semibold">External identifiers</legend>
            <p className="text-sm text-muted-foreground">
              Numbers a federation, a school or your organisation gave them, such as KMSA_REG_NO. National ID and
              passport numbers aren&apos;t recorded.
            </p>
            {identifierRows.fields.map((row, index) => (
              <div
                key={row.id}
                className="grid gap-3 rounded-md border p-3 tablet:grid-cols-[1fr_1fr_1fr_auto] tablet:items-end"
              >
                <FormField
                  id={`identifier-${index}-authority`}
                  label="Issued by (code)"
                  autoCapitalize="characters"
                  autoComplete="off"
                  error={errors.externalIdentifiers?.[index]?.authority?.message}
                  {...form.register(`externalIdentifiers.${index}.authority`)}
                />
                <FormField
                  id={`identifier-${index}-value`}
                  label="Identifier"
                  autoComplete="off"
                  error={errors.externalIdentifiers?.[index]?.value?.message}
                  {...form.register(`externalIdentifiers.${index}.value`)}
                />
                <FormField
                  id={`identifier-${index}-validFrom`}
                  label="Valid from (optional)"
                  type="date"
                  error={errors.externalIdentifiers?.[index]?.validFrom?.message}
                  {...form.register(`externalIdentifiers.${index}.validFrom`)}
                />
                <Button type="button" variant="outline" onClick={() => identifierRows.remove(index)}>
                  Remove
                </Button>
              </div>
            ))}
            {identifierRows.fields.length < limits.identifiers ? (
              <Button
                type="button"
                variant="outline"
                className="justify-self-start"
                onClick={() => identifierRows.append({ ...emptyIdentifier })}
              >
                <Plus aria-hidden="true" />
                Add an identifier
              </Button>
            ) : null}
          </fieldset>

          <div className="flex flex-wrap gap-3">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? "Checking…" : "Check and add"}
            </Button>
            <Button type="button" variant="outline" onClick={() => setStep("name")}>
              Back
            </Button>
          </div>
        </form>
      ) : null}

      {step === "review" && review ? (
        <DuplicateReview
          newPerson={{
            displayName: displayNameOf(review.values),
            born: review.values.dateOfBirth
              ? dateFormat.format(new Date(`${review.values.dateOfBirth}T00:00:00Z`))
              : "Not recorded",
            nationality: countryName(review.values.nationality),
          }}
          candidates={review.candidates}
          notice={review.notice}
          personHref={personHref}
          variantLabel={labels.variant}
          busy={save.isPending}
          onProceed={(reason) => save.mutate({ values: review.values, reviewed: review, reason })}
          onChangeDetails={() => {
            save.reset();
            setStep("details");
          }}
        />
      ) : null}
    </section>
  );
}
