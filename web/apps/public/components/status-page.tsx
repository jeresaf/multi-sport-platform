import type { ReactNode } from "react";

type StatusPageProps = {
  title: string;
  children: ReactNode;
  /** Shown so a fan can quote it when reporting a problem (the Next.js error digest, also in the server log). */
  reference?: string;
};

/** A full-page message for when there is no site to show: unknown host or page, unavailable tenant, error. */
export function StatusPage({ title, children, reference }: StatusPageProps) {
  return (
    <main className="mx-auto flex min-h-screen max-w-xl flex-col items-center justify-center gap-4 px-4 text-center">
      <h1 className="text-2xl font-semibold tablet:text-3xl">{title}</h1>
      <div className="text-text-muted">{children}</div>
      {reference ? (
        <p className="text-sm text-text-muted">
          Reference: <code>{reference}</code>
        </p>
      ) : null}
    </main>
  );
}
