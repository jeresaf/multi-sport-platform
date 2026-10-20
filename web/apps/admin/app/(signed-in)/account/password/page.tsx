"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { errorMessage } from "@msp/api-client/errors";
import { Button } from "@msp/ui/components/button";
import { useMutation } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { ProblemAlert } from "@/components/problem-alert";
import { AppShell } from "@/components/shell/app-shell";
import { api, call, isApiProblem } from "@/lib/api";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useHydrated } from "@/lib/hydrated";
import { newPasswordSchema, passwordHint } from "@/lib/new-password";

const schema = newPasswordSchema.and(z.object({ currentPassword: z.string().min(1, "Enter your current password.") }));
type Values = z.infer<typeof schema>;

// The API calls the new password `newPassword`.
const fields = { currentPassword: "currentPassword", newPassword: "password" } as const;

/** Changing the signed-in user's own password; their other sessions end, this one stays. */
export default function ChangePasswordPage() {
  const hydrated = useHydrated();
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: "", password: "", confirmPassword: "" },
  });
  const change = useMutation({
    mutationFn: ({ currentPassword, password }: Values) =>
      call(api.POST("/api/account/v1/password/change", { body: { currentPassword, newPassword: password } })),
    onSuccess: () => form.reset(),
    onError: (error) => {
      applyFieldErrors(error, fields, form.setError);
      if (isApiProblem(error, "AUTH_CURRENT_PASSWORD_INVALID")) {
        form.setError("currentPassword", { type: "server", message: errorMessage("AUTH_CURRENT_PASSWORD_INVALID") });
      }
    },
  });

  const { errors } = form.formState;
  const alert =
    change.isError && !isApiProblem(change.error, "AUTH_CURRENT_PASSWORD_INVALID") && needsAlert(change.error, fields);

  return (
    <AppShell workspace="Your account" navigation={[]}>
      <section className="grid max-w-md gap-6">
        <header className="grid gap-1">
          <h1 className="text-2xl font-semibold">Change password</h1>
          <p className="text-muted-foreground">Anywhere else you are signed in is signed out within a few minutes.</p>
        </header>
        <form className="grid gap-4" noValidate onSubmit={form.handleSubmit((values) => change.mutate(values))}>
          {alert ? <ProblemAlert error={change.error} /> : null}
          <FormField
            id="currentPassword"
            label="Current password"
            type="password"
            autoComplete="current-password"
            error={errors.currentPassword?.message}
            {...form.register("currentPassword")}
          />
          <FormField
            id="password"
            label="New password"
            type="password"
            autoComplete="new-password"
            hint={passwordHint}
            error={errors.password?.message}
            {...form.register("password")}
          />
          <FormField
            id="confirmPassword"
            label="Type it again"
            type="password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            {...form.register("confirmPassword")}
          />
          <div className="flex flex-wrap items-center gap-3">
            <Button type="submit" disabled={!hydrated || change.isPending}>
              {change.isPending ? "Saving…" : "Change password"}
            </Button>
            <p role="status" className="text-sm text-muted-foreground">
              {change.isSuccess ? "Your password has been changed." : ""}
            </p>
          </div>
        </form>
      </section>
    </AppShell>
  );
}
