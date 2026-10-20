"use client";

import { Button } from "@msp/ui/components/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@msp/ui/components/dropdown-menu";
import { KeyRound, LogOut, UserRound } from "lucide-react";
import { humanizeCode } from "@/lib/codes";
import type { Me } from "@/lib/workspace";

/** Who is signed in, and signing out. Changing the password has no screen yet, so it says so. */
export function UserMenu({ me, onSignOut }: Readonly<{ me: Me; onSignOut: () => void }>) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" aria-label="Account menu" className="min-w-0">
          <UserRound aria-hidden="true" />
          <span className="hidden truncate desktop:inline">{me.email}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-72">
        <DropdownMenuLabel className="grid gap-0.5">
          <span className="truncate">{me.email}</span>
          {me.platformRoles.length > 0 ? (
            <span className="text-xs font-normal text-muted-foreground">
              {me.platformRoles.map(humanizeCode).join(", ")}
            </span>
          ) : null}
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem disabled>
          <KeyRound aria-hidden="true" />
          Change password (not available yet)
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={onSignOut}>
          <LogOut aria-hidden="true" />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
