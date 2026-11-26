// Registry → Venues → Add venue (S07 T6a-2): the live duplicate warning lists the venues that may match, each with
// Open; the save waits for "This is a different one" and then sends `duplicatesReviewed` with the optional reason; a
// save the server sends back for review shows its list; the API's field errors land under their fields; Add sends no
// slug and no country unless one was chosen.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { bukoto, candidate, kisaasi, venue, venueAnswers } from "@/test/venues";
import TenantWorkspaceLayout from "../../layout";
import AddVenuePage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa" }),
  usePathname: () => "/t/kmsa/venues/new",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["venue.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "venue.create", "venue.duplicate_check"];
const added = venue({ id: "0199a3f0-0000-7000-8000-0000000000b9", name: "Kiwatule Meadow Ground", city: "Kampala" });

function open(answers: Answers = {}, capabilities: Parameters<typeof venueAnswers>[0] = registrar) {
  const requests = fakeApi({
    ...venueAnswers(capabilities),
    "POST /api/admin/v1/venues/duplicate-check": { status: 200, body: { candidates: [] } },
    "POST /api/admin/v1/venues": { status: 201, body: added },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <AddVenuePage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const type = (label: string | RegExp, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

async function fill(name = "Kiwatule Meadow Ground") {
  fireEvent.change(await screen.findByLabelText("Name"), { target: { value: name } });
  fireEvent.change(await screen.findByLabelText("Type"), { target: { value: "SPORTS_GROUND" } });
  type("City or town", "Kampala");
}

const creates = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.method === "POST" && request.path === "/api/admin/v1/venues");
const checks = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.path === "/api/admin/v1/venues/duplicate-check");
const save = () => fireEvent.click(screen.getByRole("button", { name: "Add venue" }));
const panel = () => screen.getByRole("region", { name: "Is this a venue you already have?" });

afterEach(() => {
  navigation.push.mockReset();
  cleanup();
  vi.unstubAllGlobals();
});

describe("Add venue", () => {
  it("adds a venue without a slug, in the organisation's country and time zone, and opens it", async () => {
    const requests = open();
    await fill();
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    expect(checks(requests).at(-1)!.body).toEqual({
      name: "Kiwatule Meadow Ground",
      city: "Kampala",
      latitude: null,
      longitude: null,
    });
    fireEvent.click(screen.getByRole("checkbox", { name: "Natural grass" }));
    fireEvent.click(await screen.findByRole("checkbox", { name: "Ringball sevens" }));
    type("Capacity (optional)", "1200");
    save();

    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toEqual({
      name: "Kiwatule Meadow Ground",
      shortName: null,
      type: "SPORTS_GROUND",
      addressLine: null,
      city: "Kampala",
      region: null,
      country: null,
      timeZone: "Africa/Kampala",
      latitude: null,
      longitude: null,
      capacity: 1200,
      surfaces: ["NATURAL_GRASS"],
      supportedVariants: ["RINGBALL_SEVENS"],
      duplicatesReviewed: false,
      duplicateReason: null,
    });
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/venues/${added.id}`));
  });

  it("warns of a venue that may be the same, opens it, or saves once told it is a different one", async () => {
    const requests = open({
      "POST /api/admin/v1/venues/duplicate-check": {
        status: 200,
        body: {
          candidates: [
            candidate(kisaasi, "POSSIBLE", [
              { code: "NAME_SIMILARITY", value: "0.86" },
              { code: "SAME_CITY", value: null },
            ]),
          ],
        },
      },
    });
    await fill("Kisaasi Recreational Ground");

    const warning = await screen.findByRole("region", { name: "Is this a venue you already have?" });
    expect(within(warning).getByText("Possibly the same")).toBeTruthy();
    expect(within(warning).getByText("Names 86% alike")).toBeTruthy();
    expect(within(warning).getByText("Same city")).toBeTruthy();
    expect(within(warning).getByRole("link", { name: `Open ${kisaasi.name}` }).getAttribute("href")).toBe(
      `/t/kmsa/venues/${kisaasi.id}`,
    );

    // Not yet decided: the save waits, and says why.
    save();
    expect(await within(panel()).findByRole("alert")).toBeTruthy();
    expect(within(panel()).getByRole("alert").textContent).toBe(
      "Open the existing venue, or say this is a different one.",
    );
    expect(creates(requests)).toHaveLength(0);

    fireEvent.click(within(panel()).getByRole("button", { name: "This is a different one" }));
    fireEvent.change(within(panel()).getByLabelText("Why is it different? (optional)"), {
      target: { value: "The recreational ground is across the road." },
    });
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(1));
    expect(creates(requests)[0]!.body).toMatchObject({
      duplicatesReviewed: true,
      duplicateReason: "The recreational ground is across the road.",
    });
  });

  it("asks again when a new venue may match after the decision", async () => {
    let answer = [candidate(kisaasi)];
    open({
      "POST /api/admin/v1/venues/duplicate-check": () => ({ status: 200, body: { candidates: answer } }),
    });
    await fill("Kisaasi Recreation Ground");
    const warning = await screen.findByRole("region", { name: /already have/ });
    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    expect(within(panel()).getByText("You said this is a different venue")).toBeTruthy();

    answer = [candidate(kisaasi), candidate(bukoto)];
    type("City or town", "Kampala Central");
    expect(await within(panel()).findByText(bukoto.name)).toBeTruthy();
    expect(within(panel()).getByRole("button", { name: "This is a different one" })).toBeTruthy();
  });

  it("shows the list the server sent back when someone added a matching venue just before the save", async () => {
    const requests = open({
      "POST /api/admin/v1/venues": (request) =>
        (request.body as { duplicatesReviewed: boolean }).duplicatesReviewed
          ? { status: 201, body: added }
          : {
              status: 422,
              body: problem("VENUE_DUPLICATE_REVIEW_REQUIRED", 422, {
                errors: [{ field: "duplicatesReviewed", code: "VENUE_DUPLICATE_REVIEW_REQUIRED" }],
                candidates: [candidate(bukoto)],
              }),
            },
    });
    await fill();
    await waitFor(() => expect(checks(requests).length).toBeGreaterThan(0));
    save();

    const warning = await screen.findByRole("region", { name: "Is this a venue you already have?" });
    expect(within(warning).getByText(bukoto.name)).toBeTruthy();
    expect(within(warning).getByText("Likely the same")).toBeTruthy();
    expect(within(warning).getByRole("alert").textContent).toContain("Someone added a venue that may match");
    await waitFor(() => expect(document.activeElement).toBe(warning));
    // The refusal isn't repeated in an alert above the form.
    expect(screen.queryByText("VENUE_DUPLICATE_REVIEW_REQUIRED")).toBeNull();

    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    save();
    await waitFor(() => expect(creates(requests)).toHaveLength(2));
    expect(creates(requests)[1]!.body).toMatchObject({ duplicatesReviewed: true, duplicateReason: null });
    await waitFor(() => expect(navigation.push).toHaveBeenCalled());
  });

  it("checks the coordinates in pairs and in range before asking", async () => {
    const requests = open();
    await fill();
    type("Latitude (optional)", "0.3476");
    save();
    expect(await screen.findByText("Enter the longitude too, or neither.")).toBeTruthy();
    type("Longitude (optional)", "200");
    save();
    expect(await screen.findByText("Enter the longitude from -180 to 180, with at most six decimals.")).toBeTruthy();
    expect(creates(requests)).toHaveLength(0);
  });

  it("puts the API's refusal of a sport under the sports", async () => {
    open({
      "POST /api/admin/v1/venues": {
        status: 422,
        body: problem("VENUE_SPORT_UNKNOWN", 422, {
          detail: "No published sport has this code.",
          errors: [{ field: "supportedVariants[0]", code: "VENUE_SPORT_UNKNOWN" }],
        }),
      },
    });
    await fill();
    fireEvent.click(await screen.findByRole("checkbox", { name: "Ringball sevens" }));
    save();
    const sports = screen.getByRole("group", { name: "Sports it can host (optional)" });
    expect(await within(sports).findByText("No published sport has this code.")).toBeTruthy();
  });

  it("is refused to whoever can't add venues, and disabled with the reason on a read-only tenant", async () => {
    open({}, reads);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
    cleanup();

    open({}, readOnlyCapabilities(reads, ["venue.create", "venue.duplicate_check"]));
    await screen.findByRole("heading", { level: 1, name: "Add venue" });
    expect((await screen.findAllByText(/read-only/i)).length).toBeGreaterThan(0);
    expect(screen.queryByLabelText("Name")).toBeNull();
  });
});
