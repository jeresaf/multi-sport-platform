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
import { useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { StatusBadge } from "@/components/status-badge";
import { call } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { ifMatch } from "@/lib/concurrency";
import { formatDate, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useMembers, useRefreshMembership, type Member, type MembershipStatus } from "./members-data";
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
  },
  restore: {
    action: "membership.restore",
    from: ["SUSPENDED"],
    label: "Restore",
    title: (email) => `Restore ${email}?`,
    description: "They can open this organisation again with the roles they had.",
    withReason: false,
    destructive: false,
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
  },
};

const statusFilters: readonly MembershipStatus[] = ["ACTIVE", "SUSPENDED", "REMOVED"];

/** The tenant's members: their tenant-wide roles and status, and the changes the user's capabilities allow. */
export function MembersSection({ isOwner }: Readonly<{ isOwner: boolean }>) {
  const { client, can } = useTenantWorkspace();
  const refresh = useRefreshMembership();
  const timeZone = useTenantTimeZone();
  const [status, setStatus] = useState<MembershipStatus>("ACTIVE");
  const members = useMembers(status);
  const [rolesOf, setRolesOf] = useState<Member | null>(null);
  const [pending, setPending] = useState<{ member: Member; change: StatusChange } | null>(null);

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
      setPending(null);
    },
  });

  const canChangeRoles = can("role.assign") || can("role.revoke");
  const allowedChanges = (member: Member) =>
    (Object.keys(changes) as StatusChange[]).filter(
      (change) => can(changes[change].action) && changes[change].from.includes(member.status),
    );
  const rows = members.data?.pages.flatMap((page) => page.items) ?? [];
  const open = pending ? changes[pending.change] : null;

  return (
    <section className="grid gap-4" aria-labelledby="members-heading">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h2 id="members-heading" className="text-xl font-semibold">
          Members
        </h2>
        <div className="w-48">
          <SelectField
            id="member-status"
            label="Show"
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
              <TableHead>Roles</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Joined</TableHead>
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
                  <TableCell className="max-w-64 truncate font-medium" title={member.email}>
                    {member.email}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1">
                      {member.roles.length === 0 ? (
                        <span className="text-muted-foreground">No roles</span>
                      ) : (
                        member.roles.map((role) => (
                          <Badge key={role.assignmentId} variant="outline">
                            {humanizeCode(role.roleCode)}
                            {role.scopeType === "TENANT" ? null : ` (${humanizeCode(role.scopeType).toLowerCase()})`}
                          </Badge>
                        ))
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={member.status} />
                    {member.statusReason ? (
                      <p className="mt-1 max-w-56 truncate text-sm text-muted-foreground" title={member.statusReason}>
                        {member.statusReason}
                      </p>
                    ) : null}
                  </TableCell>
                  <TableCell>{formatDate(member.joinedAt, timeZone)}</TableCell>
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

      {rolesOf ? <RolesDialog member={rolesOf} isOwner={isOwner} onOpenChange={() => setRolesOf(null)} /> : null}
      {pending && open ? (
        <ActionDialog
          open
          onOpenChange={(next) => (next ? null : setPending(null))}
          title={open.title(pending.member.email)}
          description={open.description}
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
        />
      ) : null}
    </section>
  );
}
