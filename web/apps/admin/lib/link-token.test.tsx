import { renderHook, waitFor } from "@testing-library/react";
import { StrictMode, type ReactNode } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { useLinkToken } from "./link-token";

const strict = ({ children }: { children: ReactNode }) => <StrictMode>{children}</StrictMode>;

afterEach(() => window.history.replaceState(null, "", "/"));

describe("useLinkToken", () => {
  it("reads the token once and removes it from the address bar, keeping the rest", async () => {
    window.history.replaceState(null, "", "/reset-password?token=0199-abc.secret&source=email#top");

    const { result } = renderHook(() => useLinkToken(), { wrapper: strict });

    await waitFor(() => expect(result.current).toEqual({ ready: true, token: "0199-abc.secret" }));
    expect(window.location.pathname + window.location.search + window.location.hash).toBe(
      "/reset-password?source=email#top",
    );
  });

  it("gives null when the link has no token", async () => {
    window.history.replaceState(null, "", "/accept-invitation");

    const { result } = renderHook(() => useLinkToken());

    await waitFor(() => expect(result.current).toEqual({ ready: true, token: null }));
  });
});
