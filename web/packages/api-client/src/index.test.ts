import { createApiClient, isProblemDetails, type ProblemDetails } from "./index";

const notFound: ProblemDetails = {
  type: "https://errors.sportsplatform.invalid/PLATFORM_ROUTE_NOT_FOUND",
  title: "No endpoint matches this path.",
  status: 404,
  code: "PLATFORM_ROUTE_NOT_FOUND",
  correlationId: "kmsa-admin.7",
};

describe("createApiClient", () => {
  it("sends requests to the base URL and returns Problem Details as the error", async () => {
    const requests: Request[] = [];
    const client = createApiClient({
      baseUrl: "http://localhost:5080",
      fetch: async (request: Request) => {
        requests.push(request);
        return new Response(JSON.stringify(notFound), {
          status: 404,
          headers: { "Content-Type": "application/problem+json" },
        });
      },
    });

    // No endpoints are in the document yet (S01-T7 adds the first); any path is cast for this test.
    const { error, response } = await client.GET("/api/public/v1/countries" as never);

    expect(requests.map((request) => request.url)).toEqual(["http://localhost:5080/api/public/v1/countries"]);
    expect(response.status).toBe(404);
    expect(isProblemDetails(error)).toBe(true);
    expect(error).toEqual(notFound);
  });
});

describe("isProblemDetails", () => {
  it("accepts a body with code and correlationId", () => {
    expect(isProblemDetails(notFound)).toBe(true);
  });

  it.each([null, undefined, "PLATFORM_ROUTE_NOT_FOUND", { title: "Not found" }, { code: "PLATFORM_ROUTE_NOT_FOUND" }])(
    "rejects %j",
    (value) => {
      expect(isProblemDetails(value)).toBe(false);
    },
  );
});
