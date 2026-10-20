import type { Metadata } from "next";
import { ForgotPasswordForm } from "./forgot-password-form";

export const metadata: Metadata = { title: "Forgot password · MSP Admin" };

export default function Page() {
  return <ForgotPasswordForm />;
}
