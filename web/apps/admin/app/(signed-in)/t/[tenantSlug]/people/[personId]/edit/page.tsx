"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { ConflictAlert } from "@/components/conflict-alert";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { ForbiddenView, NotFoundView } from "@/components/status-page";
import { UnavailableNote } from "@/components/unavailable-note";
import { BackLink } from "@/components/viewer/facts";
import { call, isApiProblem } from "@/lib/api";
import { ifMatch, isConcurrencyConflict } from "@/lib/concurrency";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useCountries } from "@/lib/reference-data";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { tenantPath } from "@/lib/workspace";
import { checkedSave, type Attempt, type Review } from "../../checked-save";
import { DetailFields, NameFields } from "../../new/biodata-fields";
import { DuplicateReview } from "../../new/duplicate-review";
import {
  biodataApiFields,
  biodataSchema,
  changesIdentity,
  displayNameOf,
  fromPerson,
  toUpdateCheckRequest,
  toUpdateRequest,
  type BiodataValues,
} from "../../new/person-form";
import { personPrivateQueryKey, personQueryKey, usePerson, usePersonPrivate, type Person } from "../../people-data";
import { useSportLabels } from "../sport-labels";
import { editDetailsAccess } from "./edit-access";

const dateFormat = new Intl.DateTimeFormat("en-GB", { dateStyle: "long", timeZone: "UTC" });

/**
 * Registry → People → a person → Edit details (S06 D8; T7c-2b): the whole biodata on a page of its own. The `PUT`
 * replaces it all, the date of birth included, so the page needs the private read (`person.read_private`) as well as
 * `person.update` and `person.duplicate_check` (owner, T7c-2b plan). Identifiers and aliases have their own tab; the
 * status its own actions.
 */
export default function EditPersonPage() {
  const { personId } = useParams<{ personId: string }>();
  const { tenant, can, unavailable } = useTenantWorkspace();
  const { allowed, reason } = editDetailsAccess(can, unavailable);
  const queryClient = useQueryClient();
  // Both read fresh on opening: a kept date of birth may be older than the person's version, and the update would put
  // it back under that version (review #113 R1). One audited private read per visit.
  const person = usePerson(personId, true);
  const privateRead = usePersonPrivate(personId, allowed, false, true);
  const countries = useCountries();
  // After a conflict the form starts over once both reads are fresh; a background read of the person (its photo's URLs
  // expire) never resets what is being typed (pre-review m1).
  const [reloading, setReloading] = useState(false);
  const reload = async () => {
    setReloading(true);
    try {
      await Promise.all([
        queryClient.refetchQueries({ queryKey: personQueryKey(tenant.tenantId, personId) }),
        queryClient.refetchQueries({ queryKey: personPrivateQueryKey(tenant.tenantId, personId) }),
      ]);
    } finally {
      setReloading(false);
    }
  };

  if (!can("person.read")) {
    return <ForbiddenView />;
  }
  if (isApiProblem(person.error, "PERSON_NOT_FOUND")) {
    return <NotFoundView />;
  }
  const personHref = `${tenantPath(tenant.slug)}/people/${personId}`;
  const back = <BackLink href={personHref}>{person.data?.displayName ?? "Back"}</BackLink>;
  if (!allowed) {
    return reason ? (
      <section className="grid gap-4">
        {back}
        <h1 className="text-2xl font-semibold">Edit details</h1>
        <UnavailableNote id="edit-person-unavailable" reason={reason} />
      </section>
    ) : (
      <ForbiddenView />
    );
  }
  // The countries too: a nationality select without its options would show, and save, none.
  const fresh = person.isFetchedAfterMount && privateRead.isFetchedAfterMount;
  if (!person.data || !privateRead.data || !fresh || reloading || !countries.data) {
    const error = person.error ?? privateRead.error ?? countries.error;
    return (
      <section className="grid gap-4">
        {back}
        {error ? <ProblemAlert error={error} /> : <Loading />}
      </section>
    );
  }
  return (
    <EditDetails
      person={person.data}
      dateOfBirth={privateRead.data.dateOfBirth}
      personHref={personHref}
      onReload={() => void reload()}
    />
  );
}

function EditDetails({
  person,
  dateOfBirth,
  personHref,
  onReload,
}: Readonly<{ person: Person; dateOfBirth: string | null; personHref: string; onReload: () => void }>) {
  const { tenant, client } = useTenantWorkspace();
  const router = useRouter();
  const queryClient = useQueryClient();
  const countries = useCountries();
  // The person as the page opened on them: the change's basis (#110 R1) and what the check compares with.
  const [opened] = useState(() => ({ version: person.version, values: fromPerson(person, dateOfBirth) }));
  const [review, setReview] = useState<Review<BiodataValues> | null>(null);
  const form = useForm<BiodataValues>({
    resolver: zodResolver(biodataSchema),
    defaultValues: opened.values,
    mode: "onTouched",
  });
  const heading = useRef<HTMLHeadingElement>(null);
  const firstRender = useRef(true);
  const labels = useSportLabels(review?.candidates.flatMap((candidate) => candidate.athleteVariants) ?? []);

  // The form and the review each start at their heading, so a screen reader hears where it is.
  const step = review ? "review" : "details";
  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    heading.current?.focus();
  }, [step]);

  const save = useMutation({
    mutationFn: (attempt: Attempt<BiodataValues>) =>
      checkedSave(attempt, {
        check: (values) =>
          call(
            client.POST("/api/admin/v1/people/duplicate-check", { body: toUpdateCheckRequest(values, person.id) }),
          ),
        save: (values, checkToken, reason) =>
          call(
            client.PUT("/api/admin/v1/people/{personId}", {
              params: { path: { personId: person.id }, header: ifMatch(opened.version) },
              body: toUpdateRequest(values, checkToken, reason),
            }),
          ),
        needsCheck: (values) => changesIdentity(opened.values, values),
      }),
    onSuccess: (outcome) => {
      if (outcome.kind === "review") {
        setReview(outcome.review);
        return;
      }
      queryClient.setQueryData(personQueryKey(tenant.tenantId, person.id), outcome.person);
      // The date of birth may have changed: the person page reads the private details again (marked stale, not read
      // here: each read is audited, pre-review m2).
      void queryClient.invalidateQueries({
        queryKey: personPrivateQueryKey(tenant.tenantId, person.id),
        refetchType: "none",
      });
      void queryClient.invalidateQueries({ queryKey: ["people", tenant.tenantId] });
      router.push(personHref);
    },
    onError: (error) => {
      applyFieldErrors(error, biodataApiFields, form.setError);
      // A refused field of the form is shown under it, on the form (a refused reason stays on the review).
      const fields = isApiProblem(error) ? (error.problem.errors ?? []).map((failure) => failure.field) : [];
      if (fields.some((field) => Object.hasOwn(biodataApiFields, field))) {
        setReview(null);
      }
    },
  });

  const countryName = (code: string) =>
    code ? (countries.data?.items.find((country) => country.code === code)?.name ?? code) : "Not recorded";

  return (
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <BackLink href={personHref}>{person.displayName}</BackLink>
      <header className="grid gap-1">
        <p className="text-sm text-muted-foreground break-words">{person.displayName}</p>
        <h1 ref={heading} tabIndex={-1} className="text-2xl font-semibold outline-none">
          {review ? "Check for duplicates" : "Edit details"}
        </h1>
      </header>

      {isConcurrencyConflict(save.error) ? (
        <ConflictAlert onReload={onReload} />
      ) : save.isError && needsAlert(save.error, biodataApiFields) ? (
        <ProblemAlert error={save.error} />
      ) : null}

      {review ? (
        <DuplicateReview
          mode="edit"
          newPerson={{
            displayName: displayNameOf(review.values),
            born: review.values.dateOfBirth
              ? dateFormat.format(new Date(`${review.values.dateOfBirth}T00:00:00Z`))
              : "Not recorded",
            nationality: countryName(review.values.nationality),
          }}
          candidates={review.candidates}
          notice={review.notice}
          personHref={(id) => `${tenantPath(tenant.slug)}/people/${id}`}
          variantLabel={labels.variant}
          busy={save.isPending}
          canOpen
          onProceed={(reason) => save.mutate({ values: review.values, reviewed: review, reason })}
          onChangeDetails={() => {
            save.reset();
            setReview(null);
          }}
        />
      ) : (
        <form
          className="grid gap-5"
          noValidate
          onSubmit={form.handleSubmit((values) => save.mutate({ values, reviewed: null, reason: null }))}
        >
          <p className="text-muted-foreground">
            A change to the names, the date of birth or a nationality is checked against everyone registered before it
            is saved. Identifiers and aliases are changed on their own tab.
          </p>
          <NameFields form={form} />
          <DetailFields
            form={form}
            countries={countries.data?.items ?? []}
            displayNameHint="How they are named on screens and team sheets. Empty: first and last name."
          />
          <div className="flex flex-wrap gap-3">
            <Button type="submit" disabled={save.isPending}>
              {save.isPending ? "Saving…" : "Save"}
            </Button>
            <Button asChild variant="outline">
              <Link href={personHref}>Cancel</Link>
            </Button>
          </div>
        </form>
      )}
    </section>
  );
}
