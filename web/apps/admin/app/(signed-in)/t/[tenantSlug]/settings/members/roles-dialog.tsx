"use client";

import { rolesAssignableAt } from "@msp/api-client/roles";
import { Button } from "@msp/ui/components/button";
import { Checkbox } from "@msp/ui/components/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@msp/ui/components/dialog";
import { Label } from "@msp/ui/components/label";
import { useMutation } from "@tanstack/react-query";
import { useState } from "react";
import { ConflictAlert } from "@/components/conflict-alert";
import { ProblemAlert } from "@/components/problem-alert";
import { call } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { ifMatch, isConcurrencyConflict } from "@/lib/concurrency";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { ownerRole, useRefreshMembership, type Member } from "./members-data";

const tenantRoles = rolesAssignableAt("TENANT");

export type RoleChanges = { assign: string[]; revoke: { roleCode: string; assignmentId: string }[] };

/** The tenant-wide roles to grant and to take away to go from the member's roles to the ones chosen. */
export function roleChanges(member: Member, chosen: ReadonlySet<string>): RoleChanges {
  const held = member.roles.filter((role) => role.scopeType === "TENANT");
  const heldCodes = new Set(held.map((role) => role.roleCode));
  return {
    assign: tenantRoles.map((role) => role.code).filter((code) => chosen.has(code) && !heldCodes.has(code)),
    revoke: held
      .filter((role) => !chosen.has(role.roleCode))
      .map(({ roleCode, assignmentId }) => ({ roleCode, assignmentId })),
  };
}

/**
 * Changes a member's tenant-wide roles. Each grant or removal is its own request, sent one after another with the
 * version the previous one returned; if one is refused, the ones before it stay and the dialog says which.
 */
export function RolesDialog({
  member,
  isOwner,
  onOpenChange,
}: Readonly<{ member: Member; isOwner: boolean; onOpenChange: (open: boolean) => void }>) {
  const { client } = useTenantWorkspace();
  const refresh = useRefreshMembership();
  const [chosen, setChosen] = useState(
    () => new Set(member.roles.filter((role) => role.scopeType === "TENANT").map((role) => role.roleCode)),
  );
  const [applied, setApplied] = useState<string[]>([]);
  const otherScopes = member.roles.filter((role) => role.scopeType !== "TENANT");
  const changes = roleChanges(member, chosen);
  const unchanged = changes.assign.length === 0 && changes.revoke.length === 0;

  const save = useMutation({
    mutationFn: async ({ assign, revoke }: RoleChanges) => {
      const done: string[] = [];
      setApplied(done);
      let version = member.version;
      const path = { membershipId: member.membershipId };
      try {
        // Grants first: taking the last owner role away before another owner exists would be refused.
        for (const roleCode of assign) {
          const updated = await call(
            client.POST("/api/admin/v1/members/{membershipId}/roles", {
              params: { path, header: ifMatch(version) },
              body: { roleCode },
            }),
          );
          version = updated.version;
          done.push(`${humanizeCode(roleCode)} granted`);
        }
        for (const { roleCode, assignmentId } of revoke) {
          const updated = await call(
            client.POST("/api/admin/v1/members/{membershipId}/roles/{assignmentId}/revoke", {
              params: { path: { ...path, assignmentId }, header: ifMatch(version) },
            }),
          );
          version = updated.version;
          done.push(`${humanizeCode(roleCode)} taken away`);
        }
      } finally {
        setApplied([...done]);
        await refresh();
      }
    },
    onSuccess: () => onOpenChange(false),
  });

  const toggle = (code: string, on: boolean) =>
    setChosen((current) => {
      const next = new Set(current);
      if (on) {
        next.add(code);
      } else {
        next.delete(code);
      }
      return next;
    });

  return (
    <Dialog open onOpenChange={(open) => !save.isPending && onOpenChange(open)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto">
        <form
          className="grid gap-4"
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            save.mutate(changes);
          }}
        >
          <DialogHeader>
            <DialogTitle>Roles of {member.email}</DialogTitle>
            <DialogDescription>Roles given here apply across the whole organisation.</DialogDescription>
          </DialogHeader>
          {isConcurrencyConflict(save.error) ? (
            <ConflictAlert onReload={() => onOpenChange(false)} />
          ) : save.isError ? (
            <ProblemAlert error={save.error}>
              {applied.length > 0 ? <p>Already saved: {applied.join(", ")}.</p> : null}
            </ProblemAlert>
          ) : null}
          <fieldset className="grid gap-1" disabled={save.isPending || save.isError}>
            <legend className="mb-2 text-sm font-medium">Organisation-wide roles</legend>
            {tenantRoles.map((role) => {
              const ownerOnly = role.code === ownerRole && !isOwner;
              const id = `role-${role.code}`;
              return (
                <div key={role.code} className="flex min-h-11 items-start gap-3 py-1">
                  <Checkbox
                    id={id}
                    className="mt-0.5"
                    checked={chosen.has(role.code)}
                    disabled={ownerOnly}
                    onCheckedChange={(checked) => toggle(role.code, checked === true)}
                    aria-describedby={`${id}-description`}
                  />
                  <div className="grid gap-0.5">
                    <Label htmlFor={id}>{humanizeCode(role.code)}</Label>
                    <p id={`${id}-description`} className="text-sm text-muted-foreground">
                      {ownerOnly ? "Only an owner can give or take away this role." : role.description}
                    </p>
                  </div>
                </div>
              );
            })}
          </fieldset>
          {otherScopes.length > 0 ? (
            <p className="text-sm text-muted-foreground">
              Also holds {otherScopes.map((role) => humanizeCode(role.roleCode)).join(", ")} for part of the
              organisation; those roles can&apos;t be changed here yet.
            </p>
          ) : null}
          <DialogFooter>
            <Button variant="outline" disabled={save.isPending} onClick={() => onOpenChange(false)}>
              {save.isError ? "Close" : "Cancel"}
            </Button>
            {save.isError ? null : (
              <Button type="submit" disabled={unchanged || save.isPending}>
                {save.isPending ? "Saving…" : "Save roles"}
              </Button>
            )}
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
