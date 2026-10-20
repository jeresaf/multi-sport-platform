import type { Metadata } from "next";
import { ResetPasswordForm } from "./reset-password-form";

export const metadata: Metadata = { title: "Choose a new password · MSP Admin" };

export default function Page() {
  return <ResetPasswordForm />;
}
