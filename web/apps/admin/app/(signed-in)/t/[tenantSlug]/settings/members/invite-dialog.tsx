"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { rolesAssignableAt } from "@msp/api-client/roles";
import { errorMessage } from "@msp/api-client/errors";
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
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { ProblemAlert } from "@/components/problem-alert";
import { call, isApiProblem } from "@/lib/api";
import { humanizeCode } from "@/lib/codes";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { ownerRole, useRefreshMembership } from "./members-data";

const tenantRoles = rolesAssignableAt("TENANT");

const schema = z.object({
  email: z.email("Enter an email address, such as name@example.org."),
  roleCodes: z.array(z.string()).min(1, "Choose at least one role."),
});
type Values = z.infer<typeof schema>;
const fields = { email: "email", roleCodes: "roleCodes" } as const;

/** Invites a person by email with tenant-wide roles; they get a link that lasts 7 days. */
export function InviteDialog({
  isOwner,
  onOpenChange,
}: Readonly<{ isOwner: boolean; onOpenChange: (open: boolean) => void }>) {
  const { tenant, client } = useTenantWorkspace();
  const refresh = useRefreshMembership();
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { email: "", roleCodes: [] } });

  const invite = useMutation({
    mutationFn: (values: Values) => call(client.POST("/api/admin/v1/invitations", { body: values })),
    onSuccess: async () => {
      await refresh();
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

  const { errors } = form.formState;
  const alert =
    invite.isError && !isApiProblem(invite.error, "AUTH_ALREADY_MEMBER") && needsAlert(invite.error, fields);

  return (
    <Dialog open onOpenChange={(open) => !invite.isPending && onOpenChange(open)}>
      <DialogContent className="max-h-[90vh] overflow-y-auto">
        <form className="grid gap-4" noValidate onSubmit={form.handleSubmit((values) => invite.mutate(values))}>
          <DialogHeader>
            <DialogTitle>Invite someone to {tenant.displayName}</DialogTitle>
            <DialogDescription>
              They get an email with a link that works for 7 days. A pending invitation to the same address is
              replaced.
            </DialogDescription>
          </DialogHeader>
          {alert ? <ProblemAlert error={invite.error} /> : null}
          <FormField
            id="invite-email"
            label="Email"
            type="email"
            autoComplete="off"
            error={errors.email?.message}
            {...form.register("email")}
          />
          <Controller
            control={form.control}
            name="roleCodes"
            render={({ field }) => (
              <fieldset
                className="grid gap-1"
                aria-describedby={errors.roleCodes ? "invite-roles-error" : undefined}
                aria-invalid={errors.roleCodes ? true : undefined}
              >
                <legend className="mb-2 text-sm font-medium">Roles</legend>
                {tenantRoles
                  .filter((role) => isOwner || role.code !== ownerRole)
                  .map((role) => {
                    const id = `invite-role-${role.code}`;
                    return (
                      <div key={role.code} className="flex min-h-11 items-start gap-3 py-1">
                        <Checkbox
                          id={id}
                          className="mt-0.5"
                          checked={field.value.includes(role.code)}
                          onCheckedChange={(checked) =>
                            field.onChange(
                              checked === true
                                ? [...field.value, role.code]
                                : field.value.filter((code) => code !== role.code),
                            )
                          }
                          aria-describedby={`${id}-description`}
                        />
                        <div className="grid gap-0.5">
                          <Label htmlFor={id}>{humanizeCode(role.code)}</Label>
                          <p id={`${id}-description`} className="text-sm text-muted-foreground">
                            {role.description}
                          </p>
                        </div>
                      </div>
                    );
                  })}
                {errors.roleCodes ? (
                  <p id="invite-roles-error" className="text-sm text-destructive">
                    {errors.roleCodes.message}
                  </p>
                ) : null}
              </fieldset>
            )}
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
