import type { Metadata } from "next";
import { Suspense } from "react";
import { PageCard } from "@/components/page-card";
import { SignInPage } from "./sign-in-page";

export const metadata: Metadata = { title: "Sign in · MSP Admin" };

export default function Page() {
  return (
    <PageCard title="Sign in">
      {/* The return address comes from the query string, which is only known in the browser. */}
      <Suspense>
        <SignInPage />
      </Suspense>
    </PageCard>
  );
}
