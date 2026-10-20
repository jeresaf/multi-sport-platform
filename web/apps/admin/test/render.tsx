// Test helpers: render with a fresh query client, and a fake API answering by method and path.
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { vi } from "vitest";

export function renderWithQuery(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

export type Answer = { status: number; body?: unknown };
export type RecordedRequest = { method: string; path: string; headers: Headers; body: unknown };

/** Replaces fetch: `answers["POST /api/account/v1/login"]` answers that request; anything else is a 404. */
export function fakeApi(answers: Record<string, Answer | (() => Answer)>) {
  const requests: RecordedRequest[] = [];
  vi.stubGlobal("fetch", async (request: Request) => {
    const path = new URL(request.url).pathname;
    const text = await request.text();
    requests.push({ method: request.method, path, headers: request.headers, body: text ? JSON.parse(text) : undefined });

    const entry = answers[`${request.method} ${path}`];
    const answer = typeof entry === "function" ? entry() : (entry ?? { status: 404, body: problem("PLATFORM_ROUTE_NOT_FOUND", 404) });
    const contentType = answer.status >= 400 ? "application/problem+json" : "application/json";
    return new Response(answer.body === undefined ? null : JSON.stringify(answer.body), {
      status: answer.status,
      headers: { "Content-Type": contentType },
    });
  });
  return requests;
}

export function problem(code: string, status: number, extra: Record<string, unknown> = {}) {
  return { code, status, title: code, correlationId: "kmsa-admin.test", ...extra };
}
