import type { ReactNode } from "react";

/** The pages people use before they have a session: one column, centred, the app's name above. */
export default function SignedOutLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <main className="mx-auto flex min-h-screen w-full max-w-md flex-col justify-center gap-6 px-4 py-12">
      <p className="text-center text-sm font-semibold tracking-wide text-muted-foreground">MSP Admin</p>
      {children}
    </main>
  );
}
