import { describeProblem } from "@msp/api-client/errors";
import Link from "next/link";
import type { ReactNode } from "react";
import { ApiProblem } from "@/lib/api";

/** A page that answers instead of the screen asked for: not found, not allowed, not available yet. */
export function StatusPage({
  status,
  title,
  children,
}: Readonly<{ status?: string; title: string; children: ReactNode }>) {
  return (
    <section className="mx-auto grid w-full max-w-xl gap-3 py-12 text-center">
      {status ? <p className="text-sm font-semibold tracking-wide text-muted-foreground">{status}</p> : null}
      <h1 className="text-2xl font-semibold">{title}</h1>
      <div className="grid gap-3 text-muted-foreground">{children}</div>
    </section>
  );
}

const homeLink = (
  <Link href="/" className="text-primary underline-offset-4 hover:underline">
    Go to your workspace
  </Link>
);

export function NotFoundView() {
  return (
    <StatusPage status="404" title="Page not found">
      <p>There is no page at this address, or it isn&apos;t one you can open.</p>
      <p>{homeLink}</p>
    </StatusPage>
  );
}

/**
 * A 403: the API's refusal when there is one (its code says why), else the capabilities didn't allow the screen. It is
 * always shown in the shell, so it points to the workspace menu: a link to `/` could lead straight back here.
 */
export function ForbiddenView({ error }: Readonly<{ error?: unknown }>) {
  const described = error instanceof ApiProblem ? describeProblem(error.problem) : undefined;
  return (
    <StatusPage status="403" title="You can't open this page">
      <p>{described?.message ?? "Your roles in this workspace don't include it. Ask an administrator if you need it."}</p>
      {described?.correlationId ? <p>Reference: {described.correlationId}</p> : null}
      <p>Open another workspace from the workspace menu at the top of the page.</p>
    </StatusPage>
  );
}

/** A screen that is planned but not built: say so instead of showing an empty or made-up page. */
export function NotAvailableView({ label }: Readonly<{ label: string }>) {
  return (
    <StatusPage title={label}>
      <p>This screen isn&apos;t available yet.</p>
    </StatusPage>
  );
}
