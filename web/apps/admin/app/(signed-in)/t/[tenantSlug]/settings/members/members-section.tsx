"use client";

import { Badge } from "@msp/ui/components/badge";
import { Button } from "@msp/ui/components/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@msp/ui/components/dropdown-menu";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { useMutation } from "@tanstack/react-query";
import { MoreHorizontal } from "lucide-react";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { StatusBadge } from "@/components/status-badge";
import { useMe } from "@/lib/account";
import { call, isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { ifMatch } from "@/lib/concurrency";
import { formatDate, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { ownerRole, useMembers, useRefreshMembership, type Member, type MembershipStatus } from "./members-data";
import { RolesDialog } from "./roles-dialog";

type StatusChange = "suspend" | "restore" | "remove";

// The membership state machine (state-machines.md): what each status allows, and the action each change needs.
const changes: Readonly<
  Record<
    StatusChange,
    {
      action: string;
      from: readonly MembershipStatus[];
      label: string;
      title: (email: string) => string;
      description: string;
      withReason: boolean;
      destructive: boolean;
      /** Said when the member is the user themself. */
      self: string;
    }
  >
> = {
  suspend: {
    action: "membership.suspend",
    from: ["ACTIVE"],
    label: "Suspend",
    title: (email) => `Suspend ${email}?`,
    description:
      "They can't open this organisation until they are restored. They keep their roles, and the invitations they sent and that are still pending are revoked.",
    withReason: true,
    destructive: true,
    self: "you lose access to this organisation as soon as you confirm, until someone restores you.",
  },
  restore: {
    action: "membership.restore",
    from: ["SUSPENDED"],
    label: "Restore",
    title: (email) => `Restore ${email}?`,
    description: "They can open this organisation again with the roles they had.",
    withReason: false,
    destructive: false,
    self: "you get your access back.",
  },
  remove: {
    action: "membership.remove",
    from: ["ACTIVE", "SUSPENDED"],
    label: "Remove",
    title: (email) => `Remove ${email}?`,
    description:
      "They lose access to this organisation for good; to come back they need a new invitation. The invitations they sent and that are still pending are revoked.",
    withReason: true,
    destructive: true,
    self: "you lose access to this organisation for good as soon as you confirm.",
  },
};

const statusFilters: readonly MembershipStatus[] = ["ACTIVE", "SUSPENDED", "REMOVED"];

/** Columns shown from tablet width up. */
const wide = "hidden tablet:table-cell";

function StatusReason({ member }: Readonly<{ member: Member }>) {
  return member.statusReason ? (
    <p className="mt-1 max-w-64 text-sm break-words whitespace-normal text-muted-foreground">
      {member.statusReason}
    </p>
  ) : null;
}

function RoleBadges({ member }: Readonly<{ member: Member }>) {
  if (member.roles.length === 0) {
    return <span className="text-muted-foreground">No roles</span>;
  }
  return (
    <div className="flex flex-wrap gap-1">
      {member.roles.map((role) => (
        <Badge key={role.assignmentId} variant="outline">
          {humanizeCode(role.roleCode)}
          {role.scopeType === "TENANT" ? null : ` (${humanizeCode(role.scopeType).toLowerCase()})`}
        </Badge>
      ))}
    </div>
  );
}

/** The tenant's members: their tenant-wide roles and status, and the changes the user's capabilities allow. */
export function MembersSection({ isOwner }: Readonly<{ isOwner: boolean }>) {
  const { client, can } = useTenantWorkspace();
  const refresh = useRefreshMembership();
  const myUserId = useMe().data?.id;
  const timeZone = useTenantTimeZone();
  const [status, setStatus] = useState<MembershipStatus>("ACTIVE");
  const members = useMembers(status);
  const [rolesOf, setRolesOf] = useState<Member | null>(null);
  const [pending, setPending] = useState<{ member: Member; change: StatusChange } | null>(null);
  // After a change the member usually leaves the filtered list, menu button and all: focus goes to the heading.
  const heading = useRef<HTMLHeadingElement>(null);
  const changed = useRef(false);

  const changeStatus = useMutation({
    mutationFn: ({ member, change, reason }: { member: Member; change: StatusChange; reason?: string }) => {
      const params = { path: { membershipId: member.membershipId }, header: ifMatch(member.version) };
      switch (change) {
        case "suspend":
          return call(
            client.POST("/api/admin/v1/members/{membershipId}/suspend", { params, body: { reason: reason ?? null } }),
          );
        case "restore":
          return call(client.POST("/api/admin/v1/members/{membershipId}/restore", { params }));
        case "remove":
          return call(
            client.POST("/api/admin/v1/members/{membershipId}/remove", { params, body: { reason: reason ?? null } }),
          );
      }
    },
    onSuccess: async () => {
      await refresh();
      changed.current = true;
      setPending(null);
    },
    // Refused because the member changed meanwhile (suspended, removed): show them as they are now.
    onError: (error) => {
      if (isApiProblem(error, "AUTH_MEMBERSHIP_TRANSITION_INVALID")) {
        void refresh();
      }
    },
  });

  const canChangeRoles = can("role.assign") || can("role.revoke");
  // Suspending, restoring or removing an owner is an owner's change (S03 D17): the server refuses anyone else.
  const allowedChanges = (member: Member) =>
    !isOwner && member.roles.some((role) => role.roleCode === ownerRole && role.scopeType === "TENANT")
      ? []
      : (Object.keys(changes) as StatusChange[]).filter(
          (change) => can(changes[change].action) && changes[change].from.includes(member.status),
        );
  const rows = members.data?.pages.flatMap((page) => page.items) ?? [];
  const open = pending ? changes[pending.change] : null;

  return (
    <section className="grid gap-4" aria-labelledby="members-heading">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h2 id="members-heading" ref={heading} tabIndex={-1} className="text-xl font-semibold outline-none">
          Members
        </h2>
        <div className="w-48">
          <SelectField
            id="member-status"
            label="Members to show"
            value={status}
            onChange={(event) => setStatus(event.target.value as MembershipStatus)}
          >
            {statusFilters.map((value) => (
              <option key={value} value={value}>
                {humanizeCode(value)}
              </option>
            ))}
          </SelectField>
        </div>
      </div>

      {members.isPending ? <Loading /> : null}
      {members.isError ? <ProblemAlert error={members.error} /> : null}
      {members.isSuccess && rows.length === 0 ? (
        <EmptyState title={`No ${humanizeCode(status).toLowerCase()} members`}>
          {status === "ACTIVE" ? "Invite someone below to get started." : "Choose another status to see others."}
        </EmptyState>
      ) : null}
      {rows.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Email</TableHead>
              <TableHead className={wide}>Roles</TableHead>
              <TableHead className={wide}>Status</TableHead>
              <TableHead className={wide}>Joined</TableHead>
              <TableHead>
                <span className="sr-only">Actions</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((member) => {
              const actions = allowedChanges(member);
              const rolesEditable = canChangeRoles && member.status !== "REMOVED";
              return (
                <TableRow key={member.membershipId}>
                  <TableCell className="max-w-56 tablet:max-w-64">
                    <p className="flex min-w-0 items-center gap-2 font-medium">
                      <span className="truncate" title={member.email}>
                        {member.email}
                      </span>
                      {member.userId === myUserId ? <Badge variant="secondary">You</Badge> : null}
                    </p>
                    {/* On a phone the roles and status sit under the email instead of in their own columns. */}
                    <div className="mt-2 grid gap-2 tablet:hidden">
                      <RoleBadges member={member} />
                      <StatusBadge status={member.status} />
                      <StatusReason member={member} />
                    </div>
                  </TableCell>
                  <TableCell className={wide}>
                    <RoleBadges member={member} />
                  </TableCell>
                  <TableCell className={wide}>
                    <StatusBadge status={member.status} />
                    <StatusReason member={member} />
                  </TableCell>
                  <TableCell className={wide}>{formatDate(member.joinedAt, timeZone)}</TableCell>
                  <TableCell className="text-right">
                    {actions.length > 0 || rolesEditable ? (
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <Button variant="ghost" size="icon" aria-label={`Change ${member.email}`}>
                            <MoreHorizontal aria-hidden="true" />
                          </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          {rolesEditable ? (
                            <DropdownMenuItem onSelect={() => setRolesOf(member)}>Change roles</DropdownMenuItem>
                          ) : null}
                          {actions.map((change) => (
                            <DropdownMenuItem
                              key={change}
                              variant={changes[change].destructive ? "destructive" : "default"}
                              onSelect={() => {
                                changeStatus.reset();
                                setPending({ member, change });
                              }}
                            >
                              {changes[change].label}
                            </DropdownMenuItem>
                          ))}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    ) : null}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}
      {members.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={members.isFetchingNextPage}
          onClick={() => void members.fetchNextPage()}
        >
          {members.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}

      {rolesOf ? (
        <RolesDialog
          member={rolesOf}
          isOwner={isOwner}
          isSelf={rolesOf.userId === myUserId}
          onOpenChange={() => setRolesOf(null)}
        />
      ) : null}
      {pending && open ? (
        <ActionDialog
          open
          onOpenChange={(next) => (next ? null : setPending(null))}
          title={open.title(pending.member.email)}
          description={
            pending.member.userId === myUserId ? (
              <>
                {open.description} <strong>This is you:</strong> {open.self}
              </>
            ) : (
              open.description
            )
          }
          confirmLabel={open.label}
          destructive={open.destructive}
          withReason={open.withReason}
          pending={changeStatus.isPending}
          error={changeStatus.error}
          onConfirm={(reason) => changeStatus.mutate({ ...pending, reason })}
          onReload={() => {
            void refresh();
            setPending(null);
          }}
          onCloseAutoFocus={(event) => {
            if (changed.current) {
              changed.current = false;
              event.preventDefault();
              heading.current?.focus();
            }
          }}
        />
      ) : null}
    </section>
  );
}
