"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { PageCard } from "@/components/page-card";
import { ProblemAlert } from "@/components/problem-alert";
import { api, call, isApiProblem } from "@/lib/api";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";
import { useLinkToken } from "@/lib/link-token";
import { newPasswordSchema, passwordHint, type NewPasswordValues } from "@/lib/new-password";
import { useHydrated } from "@/lib/hydrated";

// The API calls the password `newPassword`.
const fields = { newPassword: "password" } as const;

const askAgain = (
  <Link href="/forgot-password" className="text-sm text-primary underline-offset-4 hover:underline">
    Ask for a new link
  </Link>
);

export function ResetPasswordForm() {
  const hydrated = useHydrated();
  const link = useLinkToken();
  const form = useForm<NewPasswordValues>({
    resolver: zodResolver(newPasswordSchema),
    defaultValues: { password: "", confirmPassword: "" },
  });
  const reset = useMutation({
    mutationFn: ({ password }: NewPasswordValues) =>
      call(
        api.POST("/api/account/v1/password/reset", {
          body: { token: link.ready ? link.token : null, newPassword: password },
        }),
      ),
    onError: (error) => applyFieldErrors(error, fields, form.setError),
  });

  if (!link.ready) {
    return <PageCard title="Choose a new password">Loading…</PageCard>;
  }

  if (!link.token) {
    return (
      <PageCard title="This link is incomplete">
        <p>Open the link from the email again, or ask for a new one.</p>
        {askAgain}
      </PageCard>
    );
  }

  if (reset.isSuccess) {
    return (
      <PageCard title="Password changed">
        <p>Your new password works now. Any other place you were signed in has been signed out.</p>
        <Button asChild>
          <Link href="/sign-in">Sign in</Link>
        </Button>
      </PageCard>
    );
  }

  const tokenRefused = isApiProblem(reset.error, "AUTH_RESET_TOKEN_INVALID");
  const { errors } = form.formState;

  return (
    <PageCard title="Choose a new password">
      <form className="grid gap-4" noValidate onSubmit={form.handleSubmit((values) => reset.mutate(values))}>
        {reset.isError && needsAlert(reset.error, fields) ? (
          <ProblemAlert error={reset.error}>{tokenRefused ? askAgain : null}</ProblemAlert>
        ) : null}
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
        <Button type="submit" disabled={!hydrated || reset.isPending}>
          {reset.isPending ? "Saving…" : "Save the new password"}
        </Button>
      </form>
    </PageCard>
  );
}
