"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { useMutation } from "@tanstack/react-query";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { PageCard } from "@/components/page-card";
import { ProblemAlert } from "@/components/problem-alert";
import { api, call } from "@/lib/api";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";

const schema = z.object({ email: z.string().trim().min(1, "Enter your email address.") });
type ForgotValues = z.infer<typeof schema>;
const fields = { email: "email" } as const;

export function ForgotPasswordForm() {
  const form = useForm<ForgotValues>({ resolver: zodResolver(schema), defaultValues: { email: "" } });
  const forgot = useMutation({
    mutationFn: (values: ForgotValues) => call(api.POST("/api/account/v1/password/forgot", { body: values })),
    onError: (error) => applyFieldErrors(error, fields, form.setError),
  });

  if (forgot.isSuccess) {
    // The API answers the same whether or not the address has an account (no account enumeration, S03 D15).
    return (
      <PageCard title="Check your email">
        <p>
          If an account uses <strong>{forgot.variables.email}</strong>, we have sent it a link to choose a new
          password. The link works once, for one hour.
        </p>
        <Link href="/sign-in" className="text-sm text-primary underline-offset-4 hover:underline">
          Back to sign in
        </Link>
      </PageCard>
    );
  }

  return (
    <PageCard title="Forgot your password?" description="We'll email you a link to choose a new one.">
      <form className="grid gap-4" noValidate onSubmit={form.handleSubmit((values) => forgot.mutate(values))}>
        {forgot.isError && needsAlert(forgot.error, fields) ? <ProblemAlert error={forgot.error} /> : null}
        <FormField
          id="email"
          label="Email"
          type="email"
          autoComplete="username"
          error={form.formState.errors.email?.message}
          {...form.register("email")}
        />
        <Button type="submit" disabled={forgot.isPending}>
          {forgot.isPending ? "Sending…" : "Send the link"}
        </Button>
      </form>
      <Link href="/sign-in" className="text-sm text-primary underline-offset-4 hover:underline">
        Back to sign in
      </Link>
    </PageCard>
  );
}
