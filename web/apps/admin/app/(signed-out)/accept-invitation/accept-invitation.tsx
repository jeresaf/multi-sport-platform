"use client";

import type { components } from "@msp/api-client";
import { Alert, AlertDescription, AlertTitle } from "@msp/ui/components/alert";
import { Button } from "@msp/ui/components/button";
import { useMutation, useQuery } from "@tanstack/react-query";
import { UserRound } from "lucide-react";
import Link from "next/link";
import { PageCard } from "@/components/page-card";
import { ProblemAlert } from "@/components/problem-alert";
import { SignInForm } from "@/components/sign-in-form";
import { useMe, useSignOut } from "@/lib/account";
import { api, call, isApiProblem, isUnauthenticated } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { useLinkToken } from "@/lib/link-token";
import { NewAccountForm } from "./new-account-form";

type Invitation = components["schemas"]["LookupInvitationResponse"];

const sameEmail = (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: "accent" }) === 0;

export function AcceptInvitation() {
  const link = useLinkToken();
  const token = link.ready ? link.token : null;

  const lookup = useQuery({
    queryKey: ["invitation", "lookup", token],
    queryFn: () => call(api.POST("/api/account/v1/invitations/lookup", { body: { token } })),
    enabled: token !== null,
  });
  const me = useMe();
  const signOut = useSignOut();
  const accept = useMutation({
    mutationFn: (password?: string) =>
      call(api.POST("/api/account/v1/invitations/accept", { body: { token, password: password ?? null } })),
  });

  if (!link.ready || (token && lookup.isPending)) {
    return <PageCard title="Invitation">Loading…</PageCard>;
  }

  if (!token) {
    return (
      <PageCard title="This link is incomplete">
        <p>Open the link from the invitation email again. If it still doesn&apos;t work, ask for a new invitation.</p>
      </PageCard>
    );
  }

  if (lookup.isError) {
    return (
      <PageCard title="Invitation">
        <ProblemAlert error={lookup.error} title="This invitation can't be used">
          {isApiProblem(lookup.error, "AUTH_INVITATION_ALREADY_ACCEPTED") ? <SignInLink /> : null}
        </ProblemAlert>
      </PageCard>
    );
  }

  const invitation = lookup.data!;
  const signedInAs = me.data?.email;
  const signedOut = me.isError && isUnauthenticated(me.error);

  if (accept.isSuccess) {
    return <Accepted invitation={invitation} signedInAs={signedInAs} />;
  }

  const mismatch = signedInAs !== undefined && !sameEmail(signedInAs, invitation.email);

  return (
    <PageCard title={`Join ${invitation.tenantDisplayName}`} description={<InvitationSummary invitation={invitation} />}>
      {accept.isError && !(invitation.accountExists === false && isApiProblem(accept.error, "AUTH_PASSWORD_REJECTED")) ? (
        <ProblemAlert error={accept.error} />
      ) : null}
      {me.isError && !signedOut ? <ProblemAlert error={me.error} /> : null}
      {signOut.isError ? <ProblemAlert error={signOut.error} title="Couldn't sign you out" /> : null}

      {mismatch ? (
        <Alert>
          <UserRound aria-hidden="true" />
          <AlertTitle>You&apos;re signed in as {signedInAs}</AlertTitle>
          <AlertDescription>
            <p>
              This invitation is for <strong>{invitation.email}</strong>.{" "}
              {invitation.accountExists
                ? "Sign out, then sign in as that account to accept it."
                : "Accepting creates a new account for that address; you stay signed in as yourself. You can sign out first."}
            </p>
            <Button variant="outline" disabled={signOut.isPending} onClick={() => signOut.mutate()}>
              Sign out
            </Button>
          </AlertDescription>
        </Alert>
      ) : null}

      {invitation.accountExists ? (
        me.isPending ? null : signedOut ? (
          <>
            <p>
              <strong>{invitation.email}</strong> already has an account. Sign in to accept the invitation.
            </p>
            <SignInForm email={invitation.email} onSignedIn={() => undefined} />
          </>
        ) : mismatch ? null : (
          <Button disabled={accept.isPending} onClick={() => accept.mutate(undefined)}>
            {accept.isPending ? "Accepting…" : "Accept invitation"}
          </Button>
        )
      ) : (
        <NewAccountForm
          email={invitation.email}
          pending={accept.isPending}
          error={accept.error}
          onSubmit={(password) => accept.mutate(password)}
        />
      )}
    </PageCard>
  );
}

function InvitationSummary({ invitation }: Readonly<{ invitation: Invitation }>) {
  const roles = invitation.roleCodes.map(humanizeCode).join(", ");
  const expires = new Intl.DateTimeFormat("en-GB", { dateStyle: "long" }).format(new Date(invitation.expiresAt));
  return (
    <>
      You&apos;re invited as {roles} for {invitation.email}. The invitation works until {expires}.
    </>
  );
}

function Accepted({ invitation, signedInAs }: Readonly<{ invitation: Invitation; signedInAs?: string }>) {
  const signedInAsInvitee = signedInAs !== undefined && sameEmail(signedInAs, invitation.email);
  return (
    <PageCard title={`You've joined ${invitation.tenantDisplayName}`}>
      {signedInAsInvitee ? (
        <Button asChild>
          <Link href="/">Continue</Link>
        </Button>
      ) : (
        <>
          <p>
            Sign in as <strong>{invitation.email}</strong> to start.
            {signedInAs ? ` You're still signed in as ${signedInAs}: sign out first.` : ""}
          </p>
          <SignInLink />
        </>
      )}
    </PageCard>
  );
}

function SignInLink() {
  return (
    <Button asChild>
      <Link href="/sign-in">Sign in</Link>
    </Button>
  );
}
