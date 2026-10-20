"use client";

import { Badge } from "@msp/ui/components/badge";
import { Button } from "@msp/ui/components/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@msp/ui/components/dropdown-menu";
import { Check, ChevronsUpDown } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { humanizeCode } from "@/lib/codes";
import { platformPath, tenantPath, type Me } from "@/lib/workspace";

/** Switches between the user's tenants (from `/me`, blocked ones marked) and, for platform staff, the console. */
export function WorkspaceSwitcher({ me, current }: Readonly<{ me: Me; current: string }>) {
  const pathname = usePathname();
  const isOpen = (path: string) => pathname === path || pathname.startsWith(`${path}/`);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" className="max-w-full min-w-0 justify-between" aria-label={`Workspace: ${current}`}>
          <span className="truncate">{current}</span>
          <ChevronsUpDown aria-hidden="true" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-72">
        <DropdownMenuLabel>Organisations</DropdownMenuLabel>
        {me.tenants.length === 0 ? (
          <DropdownMenuItem disabled>You aren&apos;t a member of any organisation yet</DropdownMenuItem>
        ) : (
          me.tenants.map((tenant) => (
            <DropdownMenuItem key={tenant.tenantId} asChild>
              <Link href={tenantPath(tenant.slug)} aria-current={isOpen(tenantPath(tenant.slug)) ? "page" : undefined}>
                <CurrentMark shown={isOpen(tenantPath(tenant.slug))} />
                <span className="truncate">{tenant.displayName}</span>
                {tenant.status === "ACTIVE" ? null : (
                  <Badge variant="outline" className="ml-auto">
                    {humanizeCode(tenant.status)}
                  </Badge>
                )}
              </Link>
            </DropdownMenuItem>
          ))
        )}
        {me.platformRoles.length > 0 ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link href={platformPath} aria-current={isOpen(platformPath) ? "page" : undefined}>
                <CurrentMark shown={isOpen(platformPath)} />
                Platform console
              </Link>
            </DropdownMenuItem>
          </>
        ) : null}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** A check before the open workspace (named for screen readers by `aria-current`); the same space before the others. */
function CurrentMark({ shown }: Readonly<{ shown: boolean }>) {
  return <Check aria-hidden="true" className={shown ? "size-4" : "invisible size-4"} />;
}
