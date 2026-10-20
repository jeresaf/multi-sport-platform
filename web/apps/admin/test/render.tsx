// Test helpers: render with a fresh query client, and a fake API answering by method and path.
import { QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { vi } from "vitest";
import { createQueryClient } from "@/lib/query-client";

export function renderWithQuery(ui: ReactElement) {
  const queryClient = createQueryClient();
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

export type Answer = { status: number; body?: unknown };
export type RecordedRequest = { method: string; path: string; search: string; headers: Headers; body: unknown };

/** An answer, or a function of the request it answers (its query string, say). */
export type Answers = Record<string, Answer | ((request: RecordedRequest) => Answer)>;

/** Replaces fetch: `answers["POST /api/account/v1/login"]` answers that request; anything else is a 404. */
export function fakeApi(answers: Answers) {
  const requests: RecordedRequest[] = [];
  vi.stubGlobal("fetch", async (request: Request) => {
    const { pathname: path, search } = new URL(request.url);
    // Recorded when it is made, so a test can tell a request was never sent; the body follows once read.
    const recorded: RecordedRequest = { method: request.method, path, search, headers: request.headers, body: undefined };
    requests.push(recorded);
    const text = await request.text();
    recorded.body = text ? JSON.parse(text) : undefined;

    const entry = answers[`${request.method} ${path}`];
    const answer = typeof entry === "function" ? entry(recorded) : (entry ?? { status: 404, body: problem("PLATFORM_ROUTE_NOT_FOUND", 404) });
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
