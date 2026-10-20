"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import type { components } from "@msp/api-client";
import { errorMessage } from "@msp/api-client/errors";
import { Button } from "@msp/ui/components/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@msp/ui/components/dialog";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { ProblemAlert } from "@/components/problem-alert";
import { api, call, isApiProblem } from "@/lib/api";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import type { Tenant } from "../tenants-data";

export type OwnerInvitation = components["schemas"]["InvitationResponse"];

const schema = z.object({ email: z.email("Enter an email address, such as name@example.org.") });
type Values = z.infer<typeof schema>;
const fields = { email: "email" } as const;

/** Invites a tenant's owner by email (`tenant.owner.invite`); they get a link that lasts 7 days. */
export function OwnerInviteDialog({
  tenant,
  onOpenChange,
  onInvited,
}: Readonly<{
  tenant: Tenant;
  onOpenChange: (open: boolean) => void;
  onInvited: (invitation: OwnerInvitation) => void;
}>) {
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { email: "" } });

  const invite = useMutation({
    mutationFn: (values: Values) =>
      call(
        api.POST("/api/platform/v1/tenants/{tenantId}/owner-invitations", {
          params: { path: { tenantId: tenant.id } },
          body: values,
        }),
      ),
    onSuccess: (invitation) => {
      onInvited(invitation);
      onOpenChange(false);
    },
    onError: (error) => {
      applyFieldErrors(error, fields, form.setError);
      // Already a member: a fact about the address, so it goes under the address.
      if (isApiProblem(error, "AUTH_ALREADY_MEMBER")) {
        form.setError("email", { type: "server", message: errorMessage("AUTH_ALREADY_MEMBER") });
      }
    },
  });

  const alert =
    invite.isError && !isApiProblem(invite.error, "AUTH_ALREADY_MEMBER") && needsAlert(invite.error, fields);

  return (
    <Dialog open onOpenChange={(open) => !invite.isPending && onOpenChange(open)}>
      <DialogContent>
        <form className="grid gap-4" noValidate onSubmit={form.handleSubmit((values) => invite.mutate(values))}>
          <DialogHeader>
            <DialogTitle>Invite the owner of {tenant.displayName}</DialogTitle>
            <DialogDescription>
              They get an email with a link that works for 7 days and become the tenant&apos;s owner when they
              accept. A pending invitation to the same address is replaced.
            </DialogDescription>
          </DialogHeader>
          {alert ? <ProblemAlert error={invite.error} /> : null}
          <FormField
            id="owner-email"
            label="Email"
            type="email"
            autoComplete="off"
            error={form.formState.errors.email?.message}
            {...form.register("email")}
          />
          <DialogFooter>
            <Button variant="outline" disabled={invite.isPending} onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={invite.isPending}>
              {invite.isPending ? "Sending…" : "Send invitation"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
