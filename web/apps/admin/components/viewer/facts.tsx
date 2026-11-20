// The read-only viewers' building blocks (S05 D17): facts, sections, "None" and the phone-friendly row name. Shared by
// the sport variant, format and ruleset template viewers, so they live here rather than under one of them.
import { cn } from "@msp/ui/lib/utils";
import { ArrowLeft } from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";
import { formatDate } from "@/lib/tenant-settings";

/**
 * Label and value pairs (a description list), two columns from tablet up. The label column has one width in every
 * list, so the values of a tab's sections line up.
 */
export function Facts({ items }: Readonly<{ items: ReadonlyArray<readonly [string, ReactNode]> }>) {
  return (
    <dl className="grid gap-x-6 gap-y-3 tablet:grid-cols-[14rem_1fr]">
      {items.map(([label, value]) => (
        <div key={label} className="grid gap-y-0.5 tablet:col-span-2 tablet:grid-cols-subgrid">
          <dt className="text-sm text-muted-foreground">{label}</dt>
          <dd className="min-w-0 break-words">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** A titled part of a tab (`h3`, under the tab's `h2`), or of a page without tabs (`level` 2). */
export function ViewerSection({
  title,
  level = 3,
  headingId,
  children,
}: Readonly<{
  title: string;
  level?: 2 | 3;
  /** Makes the heading focusable by this id: where the focus goes after a change to the section. */
  headingId?: string;
  children: ReactNode;
}>) {
  const Heading = level === 2 ? "h2" : "h3";
  return (
    <section className="grid gap-3">
      {/* A page's sections (h2) read above a tab's or a group's parts (h3). */}
      <Heading
        id={headingId}
        tabIndex={headingId ? -1 : undefined}
        className={cn(
          level === 2 ? "text-xl font-semibold" : "text-lg font-semibold",
          headingId && "outline-none focus-visible:ring-2 focus-visible:ring-ring",
        )}
      >
        {title}
      </Heading>
      {children}
    </section>
  );
}

/** "None" for an empty list, rather than a blank. */
export function orNone(items: readonly string[]): string {
  return items.length > 0 ? items.join(", ") : "None";
}

/** A list's content, or "None" when it is empty (a table with no rows says nothing). */
export function NoneOr({ empty, children }: Readonly<{ empty: boolean; children: ReactNode }>) {
  return empty ? <p className="text-muted-foreground">None</p> : children;
}

/** A column shown from tablet up; on a phone its value is stacked under the row's name (`RowName`). */
export const wide = "hidden tablet:table-cell";

/**
 * A row header's content: the name, and on a phone the values of the row's `wide` columns under it, as admin-app.md's
 * Members table does, so a phone shows every value without scrolling sideways.
 */
export function RowName({ name, details }: Readonly<{ name: ReactNode; details: ReadonlyArray<readonly [string, ReactNode]> }>) {
  return (
    <>
      <span className="font-medium">{name}</span>
      <dl className="mt-1 grid gap-0.5 text-sm font-normal text-muted-foreground tablet:hidden">
        {details.map(([label, value]) => (
          <div key={label}>
            <dt className="inline">{label}: </dt>
            <dd className="inline">{value}</dd>
          </div>
        ))}
      </dl>
    </>
  );
}

/** A viewer's link back to its list, above the title ("Sports", "Formats", "Rules templates"). */
export function BackLink({ href, children }: Readonly<{ href: string; children: ReactNode }>) {
  return (
    <Link
      href={href}
      className="inline-flex min-h-11 w-fit items-center gap-1 text-sm text-primary underline-offset-4 hover:underline"
    >
      <ArrowLeft aria-hidden className="size-4" />
      {children}
    </Link>
  );
}

/**
 * A publication date in a time zone: the tenant's in a workspace, UTC in the platform console (which has no zone) or
 * for someone who can't read the tenant's settings. A date in UTC says so ("16 Nov 2026 (UTC)").
 */
export function publishedOn(instant: string, timeZone: string): string {
  const date = formatDate(instant, timeZone);
  return timeZone === "UTC" ? `${date} (UTC)` : date;
}
