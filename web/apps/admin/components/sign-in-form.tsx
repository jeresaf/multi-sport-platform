"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@msp/ui/components/button";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormField } from "@/components/form-field";
import { ProblemAlert } from "@/components/problem-alert";
import { meQueryKey } from "@/lib/account";
import { api, call } from "@/lib/api";
import { applyFieldErrors, needsAlert } from "@/lib/form-errors";

const schema = z.object({
  email: z.string().trim().min(1, "Enter your email address."),
  password: z.string().min(1, "Enter your password."),
});

type SignInValues = z.infer<typeof schema>;

const fields = { email: "email", password: "password" } as const;

/** Email and password sign-in. With `email`, the address is fixed (accepting an invitation as an existing user). */
export function SignInForm({ email, onSignedIn }: Readonly<{ email?: string; onSignedIn: () => void }>) {
  const queryClient = useQueryClient();
  const form = useForm<SignInValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: email ?? "", password: "" },
  });

  const signIn = useMutation({
    mutationFn: (values: SignInValues) => call(api.POST("/api/account/v1/login", { body: values })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: meQueryKey });
      onSignedIn();
    },
    onError: (error) => {
      form.resetField("password");
      applyFieldErrors(error, fields, form.setError);
    },
  });

  const { errors } = form.formState;

  return (
    <form className="grid gap-4" noValidate onSubmit={form.handleSubmit((values) => signIn.mutate(values))}>
      {signIn.isError && needsAlert(signIn.error, fields) ? <ProblemAlert error={signIn.error} title="Couldn't sign you in" /> : null}
      <FormField
        id="email"
        label="Email"
        type="email"
        autoComplete="username"
        readOnly={email !== undefined}
        error={errors.email?.message}
        {...form.register("email")}
      />
      <FormField
        id="password"
        label="Password"
        type="password"
        autoComplete="current-password"
        error={errors.password?.message}
        {...form.register("password")}
      />
      <Button type="submit" disabled={signIn.isPending}>
        {signIn.isPending ? "Signing in…" : "Sign in"}
      </Button>
    </form>
  );
}
