import type { ReactNode } from "react";

/** Label and value pairs (a description list), two columns from tablet up. */
export function Facts({ items }: Readonly<{ items: ReadonlyArray<readonly [string, ReactNode]> }>) {
  return (
    <dl className="grid gap-x-6 gap-y-3 tablet:grid-cols-[minmax(10rem,auto)_1fr]">
      {items.map(([label, value]) => (
        <div key={label} className="grid gap-0.5 tablet:col-span-2 tablet:grid-cols-subgrid">
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
