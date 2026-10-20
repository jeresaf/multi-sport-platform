"use client";

import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { useMutation } from "@tanstack/react-query";
import { UserPlus } from "lucide-react";
import { useRef, useState } from "react";
import { ActionDialog } from "@/components/action-dialog";
import { EmptyState } from "@/components/empty-state";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { SelectField } from "@/components/select-field";
import { StatusBadge } from "@/components/status-badge";
import { call, isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { formatDate, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { InviteDialog } from "./invite-dialog";
import {
  ownerRole,
  useInvitations,
  useRefreshMembership,
  type Invitation,
  type InvitationStatus,
} from "./members-data";

// "OPEN" is the API's default list: pending invitations with the expired ones.
type Filter = "OPEN" | InvitationStatus;
const filters: readonly { value: Filter; label: string }[] = [
  { value: "OPEN", label: "Pending and expired" },
  { value: "PENDING", label: "Pending" },
  { value: "ACCEPTED", label: "Accepted" },
  { value: "REVOKED", label: "Revoked" },
];

/** Columns shown from tablet width up. */
const wide = "hidden tablet:table-cell";

/** Invitations: who was asked to join with which roles, inviting someone, and revoking a pending invitation. */
export function InvitationsSection({ isOwner }: Readonly<{ isOwner: boolean }>) {
  const { client, can } = useTenantWorkspace();
  const refresh = useRefreshMembership();
  const timeZone = useTenantTimeZone();
  const [filter, setFilter] = useState<Filter>("OPEN");
  const invitations = useInvitations(filter === "OPEN" ? undefined : filter);
  const [inviting, setInviting] = useState(false);
  const [revoking, setRevoking] = useState<Invitation | null>(null);
  // A revoked invitation leaves the default list with its button: focus goes to the heading.
  const heading = useRef<HTMLHeadingElement>(null);
  const revoked = useRef(false);

  const revoke = useMutation({
    // No If-Match: sending the email (the outbox, moments after the invitation) changes its version, so a version
    // read with the list would refuse the revoke as if someone had changed it. The state machine still refuses an
    // invitation that was accepted or revoked meanwhile (409 AUTH_INVITATION_TRANSITION_INVALID).
    mutationFn: (invitation: Invitation) =>
      call(
        client.POST("/api/admin/v1/invitations/{invitationId}/revoke", {
          params: { path: { invitationId: invitation.invitationId } },
        }),
      ),
    onSuccess: async () => {
      await refresh();
      revoked.current = true;
      setRevoking(null);
    },
    // Accepted or revoked meanwhile: show it as it is now.
    onError: (error) => {
      if (isApiProblem(error, "AUTH_INVITATION_TRANSITION_INVALID")) {
        void refresh();
      }
    },
  });

  // Only an owner revokes an invitation that carries the owner role (S03-T4b pre-review).
  const revocable = (invitation: Invitation) =>
    can("invitation.revoke") &&
    invitation.status === "PENDING" &&
    (isOwner || !invitation.roleCodes.includes(ownerRole));
  const rows = invitations.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <section className="grid gap-4" aria-labelledby="invitations-heading">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h2 id="invitations-heading" ref={heading} tabIndex={-1} className="text-xl font-semibold outline-none">
          Invitations
        </h2>
        <div className="flex flex-wrap items-end gap-3">
          <div className="w-56">
            <SelectField
              id="invitation-status"
              label="Invitations to show"
              value={filter}
              onChange={(event) => setFilter(event.target.value as Filter)}
            >
              {filters.map(({ value, label }) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </SelectField>
          </div>
          {can("membership.invite") ? (
            <Button onClick={() => setInviting(true)}>
              <UserPlus aria-hidden="true" />
              Invite someone
            </Button>
          ) : null}
        </div>
      </div>

      {invitations.isPending ? <Loading /> : null}
      {invitations.isError ? <ProblemAlert error={invitations.error} /> : null}
      {invitations.isSuccess && rows.length === 0 ? (
        <EmptyState title="No invitations to show">
          {can("membership.invite") ? "Invite someone to give them access to this organisation." : null}
        </EmptyState>
      ) : null}
      {rows.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Email</TableHead>
              <TableHead className={wide}>Roles</TableHead>
              <TableHead className={wide}>Status</TableHead>
              <TableHead className={wide}>Sent</TableHead>
              <TableHead className={wide}>Expires</TableHead>
              <TableHead>
                <span className="sr-only">Actions</span>
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((invitation) => (
              <TableRow key={invitation.invitationId}>
                <TableCell className="max-w-56 tablet:max-w-64">
                  <p className="truncate font-medium" title={invitation.email}>
                    {invitation.email}
                  </p>
                  {/* On a phone the roles and status sit under the email instead of in their own columns. */}
                  <div className="mt-2 grid gap-2 whitespace-normal tablet:hidden">
                    <span>{invitation.roleCodes.map(humanizeCode).join(", ")}</span>
                    <StatusBadge status={invitation.status} />
                    {invitation.status === "PENDING" ? (
                      <span className="text-sm text-muted-foreground">
                        Expires {formatDate(invitation.expiresAt, timeZone)}
                      </span>
                    ) : null}
                  </div>
                </TableCell>
                <TableCell className={`whitespace-normal ${wide}`}>
                  {invitation.roleCodes.map(humanizeCode).join(", ")}
                </TableCell>
                <TableCell className={wide}>
                  <StatusBadge status={invitation.status} />
                </TableCell>
                <TableCell className={wide}>{formatDate(invitation.invitedAt, timeZone)}</TableCell>
                <TableCell className={wide}>
                  {invitation.status === "PENDING" ? formatDate(invitation.expiresAt, timeZone) : "—"}
                </TableCell>
                <TableCell className="text-right">
                  {revocable(invitation) ? (
                    <Button
                      variant="outline"
                      aria-label={`Revoke the invitation to ${invitation.email}`}
                      onClick={() => {
                        revoke.reset();
                        setRevoking(invitation);
                      }}
                    >
                      Revoke
                    </Button>
                  ) : invitation.status === "EXPIRED" && can("membership.invite") ? (
                    <span className="text-sm whitespace-normal text-muted-foreground">Invite again for a new link</span>
                  ) : null}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : null}
      {invitations.hasNextPage ? (
        <Button
          variant="outline"
          className="justify-self-start"
          disabled={invitations.isFetchingNextPage}
          onClick={() => void invitations.fetchNextPage()}
        >
          {invitations.isFetchingNextPage ? "Loading…" : "Show more"}
        </Button>
      ) : null}

      {inviting ? <InviteDialog isOwner={isOwner} onOpenChange={setInviting} /> : null}
      {revoking ? (
        <ActionDialog
          open
          onOpenChange={(next) => (next ? null : setRevoking(null))}
          title={`Revoke the invitation to ${revoking.email}?`}
          description="The link in their email stops working. You can invite them again later."
          confirmLabel="Revoke"
          destructive
          pending={revoke.isPending}
          error={revoke.error}
          onConfirm={() => revoke.mutate(revoking)}
          onReload={() => {
            void refresh();
            setRevoking(null);
          }}
          onCloseAutoFocus={(event) => {
            if (revoked.current) {
              revoked.current = false;
              event.preventDefault();
              heading.current?.focus();
            }
          }}
        />
      ) : null}
    </section>
  );
}
