import type { ReactNode } from "react";

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

/** A titled part of a tab. */
export function ViewerSection({ title, children }: Readonly<{ title: string; children: ReactNode }>) {
  return (
    <section className="grid gap-3">
      <h3 className="text-lg font-semibold">{title}</h3>
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
