import { QueryClientProvider } from "@tanstack/react-query";
import { renderToString } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { SignInForm } from "@/components/sign-in-form";
import { createQueryClient } from "./query-client";

describe("useHydrated", () => {
  it("keeps a server-rendered form's submit button disabled, so it can't submit natively before hydration", () => {
    const html = renderToString(
      <QueryClientProvider client={createQueryClient()}>
        <SignInForm onSignedIn={() => {}} />
      </QueryClientProvider>,
    );

    expect(html).toMatch(/<button[^>]*type="submit"[^>]*disabled=""/);
  });
});
