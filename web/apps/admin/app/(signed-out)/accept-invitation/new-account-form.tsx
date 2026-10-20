"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { FormField } from "@/components/form-field";
import { applyFieldErrors } from "@/lib/form-errors";
import { newPasswordSchema, passwordHint, type NewPasswordValues } from "@/lib/new-password";

const fields = { password: "password" } as const;

/** The invited email has no account yet: the invitee chooses a password and the account is created on accepting. */
export function NewAccountForm({
  email,
  pending,
  error,
  onSubmit,
}: Readonly<{ email: string; pending: boolean; error: unknown; onSubmit: (password: string) => void }>) {
  const form = useForm<NewPasswordValues>({
    resolver: zodResolver(newPasswordSchema),
    defaultValues: { password: "", confirmPassword: "" },
  });
  const { setError } = form;

  useEffect(() => {
    applyFieldErrors(error, fields, setError);
  }, [error, setError]);

  const { errors } = form.formState;

  return (
    <form className="grid gap-4" noValidate onSubmit={form.handleSubmit(({ password }) => onSubmit(password))}>
      <div className="grid gap-2">
        <Label htmlFor="invitee-email">Email</Label>
        <Input id="invitee-email" type="email" autoComplete="username" value={email} readOnly />
      </div>
      <FormField
        id="password"
        label="Choose a password"
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
      <Button type="submit" disabled={pending}>
        {pending ? "Creating your account…" : "Create account and accept"}
      </Button>
    </form>
  );
}
