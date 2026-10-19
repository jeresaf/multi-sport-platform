import { createApiClient } from "@msp/api-client";
import { describe, expect, it } from "vitest";
import { fetchSite, hostName } from "./site";

const kmsa = { slug: "kmsa", displayName: "Kampala Metro Sports Association", primaryDomain: "kmsa.localhost" };

function problem(status: number, code: string) {
  return new Response(
    JSON.stringify({ type: `https://errors.sportsplatform.invalid/${code}`, title: code, status, code, correlationId: "kmsa-public.42" }),
    { status, headers: { "Content-Type": "application/problem+json" } },
  );
}

function clientAnswering(respond: (request: Request) => Response | Promise<Response>) {
  const requests: Request[] = [];
  const client = createApiClient({
    baseUrl: "http://localhost:5080",
    fetch: async (request: Request) => {
      requests.push(request);
      return respond(request);
    },
  });
  return { client, requests };
}

describe("hostName", () => {
  it.each([
    ["kmsa.localhost:3000", "kmsa.localhost"],
    ["KMSA.Localhost", "kmsa.localhost"],
    [" elssc.localhost ", "elssc.localhost"],
    ["[::1]:3000", "[::1]"],
  ])("reads %s as %s", (header, expected) => {
    expect(hostName(header)).toBe(expected);
  });

  it.each([null, undefined, "", "  ", ":3000"])("has no host for %j", (header) => {
    expect(hostName(header)).toBeUndefined();
  });
});

describe("fetchSite", () => {
  it("sends the host without the port as X-Forwarded-Host and returns the site", async () => {
    const { client, requests } = clientAnswering(() => Response.json(kmsa));

    const result = await fetchSite(client, "kmsa.localhost:3000");

    expect(result).toEqual({ kind: "found", site: kmsa });
    expect(requests).toHaveLength(1);
    expect(requests[0]!.url).toBe("http://localhost:5080/api/public/v1/site");
    expect(requests[0]!.headers.get("X-Forwarded-Host")).toBe("kmsa.localhost");
  });

  it("is not found for TENANT_NOT_FOUND", async () => {
    const { client } = clientAnswering(() => problem(404, "TENANT_NOT_FOUND"));

    expect(await fetchSite(client, "unknown.localhost:3000")).toEqual({ kind: "not-found" });
  });

  it("is unavailable for TENANT_UNAVAILABLE", async () => {
    const { client } = clientAnswering(() => problem(403, "TENANT_UNAVAILABLE"));

    expect(await fetchSite(client, "elssc.localhost")).toEqual({ kind: "unavailable" });
  });

  it("doesn't call the API without a host", async () => {
    const { client, requests } = clientAnswering(() => Response.json(kmsa));

    expect(await fetchSite(client, null)).toEqual({ kind: "not-found" });
    expect(requests).toHaveLength(0);
  });

  it.each([
    [500, "PLATFORM_UNEXPECTED_ERROR"],
    // A 404 with another code is a wrong route or API version, not an unknown host.
    [404, "PLATFORM_ROUTE_NOT_FOUND"],
  ])("is an error with the correlation ID for %i %s", async (status, code) => {
    const { client } = clientAnswering(() => problem(status, code));

    expect(await fetchSite(client, "kmsa.localhost")).toEqual({ kind: "error", correlationId: "kmsa-public.42" });
  });

  it("is an error without a correlation ID when the body isn't Problem Details", async () => {
    const { client } = clientAnswering(() => new Response("Bad gateway", { status: 502 }));

    expect(await fetchSite(client, "kmsa.localhost")).toEqual({ kind: "error", correlationId: undefined });
  });

  it("is an error when the API can't be reached", async () => {
    const { client } = clientAnswering(() => {
      throw new TypeError("fetch failed");
    });

    expect(await fetchSite(client, "kmsa.localhost")).toEqual({ kind: "error" });
  });
});
