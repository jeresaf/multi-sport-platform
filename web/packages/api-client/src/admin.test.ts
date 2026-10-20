import { createAdminClient } from "./admin";

function recordingClient(tenantId?: string) {
  const requests: Request[] = [];
  const client = createAdminClient({
    baseUrl: "http://localhost:3001",
    tenantId,
    fetch: async (request: Request) => {
      requests.push(request);
      return new Response("{}", { status: 200, headers: { "Content-Type": "application/json" } });
    },
  });
  return { client, requests };
}

describe("createAdminClient", () => {
  it("sends X-CSRF on a state-changing request and no tenant outside a workspace", async () => {
    const { client, requests } = recordingClient();

    await client.POST("/api/account/v1/login", { body: { email: "grace.nakato@kmsa.example", password: "x" } });

    expect(requests).toHaveLength(1);
    expect(requests[0]!.headers.get("X-CSRF")).toBe("1");
    expect(requests[0]!.headers.has("X-Tenant-Id")).toBe(false);
    expect(requests[0]!.credentials).toBe("same-origin");
  });

  it("sends no X-CSRF on a GET", async () => {
    const { client, requests } = recordingClient();

    await client.GET("/api/account/v1/me");

    expect(requests[0]!.headers.has("X-CSRF")).toBe(false);
  });

  it("sends the workspace's tenant on every request", async () => {
    const tenantId = "0199a3f0-7c1e-7d4a-9b1e-2f3c4d5e6f70";
    const { client, requests } = recordingClient(tenantId);

    await client.GET("/api/admin/v1/me/capabilities");
    await client.PATCH("/api/admin/v1/tenant/settings", { body: {} as never });

    expect(requests.map((request) => request.headers.get("X-Tenant-Id"))).toEqual([tenantId, tenantId]);
    expect(requests.map((request) => request.headers.get("X-CSRF"))).toEqual([null, "1"]);
  });
});
