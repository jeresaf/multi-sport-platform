"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { SignInForm } from "@/components/sign-in-form";
import { safeReturnTo } from "@/lib/return-to";

export function SignInPage() {
  const router = useRouter();
  const returnTo = safeReturnTo(useSearchParams().get("returnTo"));

  return (
    <>
      <SignInForm onSignedIn={() => router.replace(returnTo)} />
      <Link href="/forgot-password" className="text-sm text-primary underline-offset-4 hover:underline">
        Forgot your password?
      </Link>
    </>
  );
}
