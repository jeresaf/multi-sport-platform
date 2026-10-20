"use client";

import { Button } from "@msp/ui/components/button";
import { ProblemAlert } from "@/components/problem-alert";

/** A screen that failed unexpectedly: the message (and the reference, for a server error) and a way to try again. */
export default function ErrorPage({ error, reset }: Readonly<{ error: Error; reset: () => void }>) {
  return (
    <main className="mx-auto grid w-full max-w-xl gap-4 px-4 py-12">
      <ProblemAlert error={error} title="Something went wrong" />
      <Button variant="outline" className="justify-self-start" onClick={reset}>
        Try again
      </Button>
    </main>
  );
}
