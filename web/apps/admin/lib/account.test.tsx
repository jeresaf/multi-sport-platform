import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem } from "@/test/render";
import { useSignOut } from "./account";

afterEach(() => vi.unstubAllGlobals());

function renderSignOut() {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  queryClient.setQueryData(["account", "me"], { email: "grace.nakato@kmsa.example" });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return { queryClient, ...renderHook(() => useSignOut(), { wrapper }) };
}

describe("useSignOut", () => {
  it("signs out and forgets everything cached", async () => {
    const requests = fakeApi({ "POST /api/account/v1/logout": { status: 204 } });
    const { result, queryClient } = renderSignOut();

    result.current.mutate();

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(requests[0]!.headers.get("X-CSRF")).toBe("1");
    expect(queryClient.getQueryData(["account", "me"])).toBeUndefined();
  });

  it("counts a 401 as signed out: the session had already ended (#43 N2)", async () => {
    fakeApi({ "POST /api/account/v1/logout": { status: 401, body: problem("AUTH_UNAUTHENTICATED", 401) } });
    const { result } = renderSignOut();

    result.current.mutate();

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it("reports any other failure", async () => {
    fakeApi({ "POST /api/account/v1/logout": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) } });
    const { result } = renderSignOut();

    result.current.mutate();

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
