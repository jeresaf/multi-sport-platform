import { z } from "zod";

// The password policy's length (S03 D15); the API checks the full policy and its answer is shown under the field.
export const minimumPasswordLength = 12;
export const passwordHint = `At least ${minimumPasswordLength} characters. A few unrelated words make a good one.`;

/** A new password typed twice. */
export const newPasswordSchema = z
  .object({
    password: z.string().min(minimumPasswordLength, `Use at least ${minimumPasswordLength} characters.`),
    confirmPassword: z.string(),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: "The two passwords don't match.",
    path: ["confirmPassword"],
  });

export type NewPasswordValues = z.infer<typeof newPasswordSchema>;
