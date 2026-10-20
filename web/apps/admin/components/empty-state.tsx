import type { ReactNode } from "react";

/** A list with nothing in it: what that means and what to do next (design-system.md). */
export function EmptyState({ title, children }: Readonly<{ title: string; children?: ReactNode }>) {
  return (
    <div className="grid gap-2 rounded-lg border border-dashed p-8 text-center">
      <p className="font-medium">{title}</p>
      {children ? <div className="text-sm text-muted-foreground">{children}</div> : null}
    </div>
  );
}
