"use client";

import { Button } from "@msp/ui/components/button";
import { Sheet, SheetContent, SheetDescription, SheetTitle, SheetTrigger } from "@msp/ui/components/sheet";
import { Menu } from "lucide-react";
import { useState, type ReactNode } from "react";
import { ProblemAlert } from "@/components/problem-alert";
import { useMe, useSignOutAndLeave } from "@/lib/account";
import type { NavItem } from "@/lib/navigation";
import { NavList } from "./nav-list";
import { UserMenu } from "./user-menu";
import { WorkspaceSwitcher } from "./workspace-switcher";

/**
 * The signed-in frame (admin-app.md): workspace switcher and user menu at the top, the workspace's navigation on the
 * left — a sidebar on desktop, an icon rail on tablet, a sheet behind the menu button on phone.
 */
export function AppShell({
  workspace,
  navigation,
  children,
}: Readonly<{ workspace: string; navigation: readonly NavItem[]; children: ReactNode }>) {
  const me = useMe();
  const signOut = useSignOutAndLeave();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex min-h-16 items-center gap-2 border-b px-4">
        {navigation.length > 0 ? (
          <Sheet open={menuOpen} onOpenChange={setMenuOpen}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" aria-label="Open navigation" className="tablet:hidden">
                <Menu aria-hidden="true" />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="p-4 pt-14">
              <SheetTitle className="sr-only">Navigation</SheetTitle>
              <SheetDescription className="sr-only">{workspace}</SheetDescription>
              <nav aria-label="Workspace">
                <NavList items={navigation} onNavigate={() => setMenuOpen(false)} />
              </nav>
            </SheetContent>
          </Sheet>
        ) : null}
        <span className="hidden shrink-0 text-sm font-semibold tracking-wide text-muted-foreground tablet:inline">
          MSP Admin
        </span>
        <div className="min-w-0 flex-1">
          {me.data ? <WorkspaceSwitcher me={me.data} current={workspace} /> : null}
        </div>
        {me.data ? (
          <UserMenu me={me.data} onSignOut={() => signOut.mutate()} />
        ) : null}
      </header>
      <div className="flex flex-1">
        {navigation.length > 0 ? (
          <nav aria-label="Workspace" className="hidden shrink-0 border-r p-2 tablet:block tablet:w-16 desktop:w-60">
            <NavList items={navigation} compact />
          </nav>
        ) : null}
        <main className="grid min-w-0 flex-1 content-start gap-6 p-4 tablet:p-6">
          {signOut.isError ? <ProblemAlert error={signOut.error} title="Couldn't sign you out" /> : null}
          {children}
        </main>
      </div>
    </div>
  );
}
