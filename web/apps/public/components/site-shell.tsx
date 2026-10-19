import type { ReactNode } from "react";
import type { Site } from "@/lib/site";

/** The frame of every tenant page: the tenant's name on top, the footer below. Branding arrives with S22. */
export function SiteShell({ site, children }: { site: Site; children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-border bg-surface">
        <div className="mx-auto flex min-h-14 max-w-6xl items-center px-4 desktop:px-6">
          <p className="text-lg font-semibold">{site.displayName}</p>
        </div>
      </header>
      <div className="flex flex-1 flex-col">{children}</div>
      <footer className="border-t border-border bg-surface">
        <p className="mx-auto max-w-6xl px-4 py-6 text-sm text-text-muted desktop:px-6">
          {site.displayName} · Runs on MSP
        </p>
      </footer>
    </div>
  );
}
