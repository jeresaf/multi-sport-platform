// Registry → Venues → a venue → Edit details (S07 T6a-2): the PUT replaces every detail from the version the page opened
// on, with the venue's country and its web address; only a changed name, city or coordinates runs the warning, which
// leaves the venue itself out; a conflict says "This record changed since you opened it" and offers Reload; a slug
// already taken shows under the web address; a viewer sees no Edit details.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import { bukoto, candidate, kisaasi, venueAnswers } from "@/test/venues";
import TenantWorkspaceLayout from "../../../layout";
import VenuePage from "../page";
import EditVenuePage from "./page";

const navigation = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", venueId: "0199a3f0-0000-7000-8000-0000000000b1" }),
  usePathname: () => "/t/kmsa/venues/0199a3f0-0000-7000-8000-0000000000b1/edit",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: navigation.push }),
}));

const reads = ["venue.read", "team.read", "sport.read", "tenant.settings.read"];
const registrar = [...reads, "venue.update", "venue.duplicate_check"];
const venuePath = `/api/admin/v1/venues/${bukoto.id}`;

function open(answers: Answers = {}, capabilities: string[] = registrar, page = <EditVenuePage />) {
  const requests = fakeApi({
    ...venueAnswers(capabilities),
    [`GET ${venuePath}`]: { status: 200, body: bukoto },
    [`GET ${venuePath}/status-changes`]: { status: 200, body: { items: [] } },
    "POST /api/admin/v1/venues/duplicate-check": { status: 200, body: { candidates: [] } },
    [`PUT ${venuePath}`]: (request) => ({ status: 200, body: { ...bukoto, ...(request.body as object), version: 5 } }),
    ...answers,
  });
  renderWithQuery(<TenantWorkspaceLayout>{page}</TenantWorkspaceLayout>);
  return requests;
}

const field = (label: string) => screen.findByLabelText(label);
const type = async (label: string, value: string) => fireEvent.change(await field(label), { target: { value } });
const save = () => fireEvent.click(screen.getByRole("button", { name: "Save" }));
const puts = (requests: RecordedRequest[]) => requests.filter((request) => request.method === "PUT");
const checks = (requests: RecordedRequest[]) =>
  requests.filter((request) => request.path === "/api/admin/v1/venues/duplicate-check");

afterEach(() => {
  navigation.push.mockReset();
  cleanup();
  vi.unstubAllGlobals();
});

describe("Edit venue details", () => {
  it("saves every detail from the version it opened on, with the venue's country and web address", async () => {
    const requests = open();
    await type("Capacity (optional)", "3500");
    save();

    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    const sent = puts(requests)[0]!;
    expect(sent.headers.get("If-Match")).toBe('"4"');
    expect(sent.body).toEqual({
      name: "Bukoto Community Stadium",
      shortName: "Bukoto Stadium",
      type: "STADIUM",
      addressLine: "Plot 12, Bukoto Street",
      city: "Kampala",
      region: "Central",
      country: "UG",
      timeZone: "Africa/Kampala",
      latitude: 0.3476,
      longitude: 32.5925,
      capacity: 3500,
      surfaces: ["NATURAL_GRASS"],
      supportedVariants: ["RINGBALL_SEVENS"],
      slug: "bukoto-community-stadium",
      duplicatesReviewed: false,
      duplicateReason: null,
    });
    // Nothing the warning compares changed: it never asked.
    expect(checks(requests)).toHaveLength(0);
    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/t/kmsa/venues/${bukoto.id}`));
  });

  it("warns about other venues once the name changes, never about the venue itself", async () => {
    const requests = open({
      "POST /api/admin/v1/venues/duplicate-check": {
        status: 200,
        body: { candidates: [candidate(bukoto), candidate(kisaasi, "POSSIBLE")] },
      },
    });
    await type("Name", "Kisaasi Community Stadium");
    const warning = await screen.findByRole("region", { name: "Is this a venue you already have?" });
    expect(within(warning).getByText(kisaasi.name)).toBeTruthy();
    expect(within(warning).queryByText(bukoto.name)).toBeNull();
    expect(checks(requests).at(-1)!.body).toMatchObject({ name: "Kisaasi Community Stadium", city: "Kampala" });

    fireEvent.click(within(warning).getByRole("button", { name: "This is a different one" }));
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(1));
    expect(puts(requests)[0]!.body).toMatchObject({ name: "Kisaasi Community Stadium", duplicatesReviewed: true });
  });

  it("says the venue changed since it was opened; Reload starts over from the venue as it is now", async () => {
    // The first read is version 4; after the conflict, someone else's change (version 5) is read.
    let current = bukoto;
    let conflict = true;
    const requests = open({
      [`GET ${venuePath}`]: () => ({ status: 200, body: current }),
      [`PUT ${venuePath}`]: (request) =>
        conflict
          ? { status: 409, body: problem("VENUE_CONCURRENCY_CONFLICT", 409) }
          : { status: 200, body: { ...current, ...(request.body as object), version: 6 } },
    });
    await type("Region (optional)", "Central Region");
    save();
    expect(await screen.findByText("This record changed since you opened it")).toBeTruthy();

    current = { ...bukoto, region: "Kampala Central", version: 5 };
    conflict = false;
    fireEvent.click(screen.getByRole("button", { name: "Reload" }));
    // The form shows the venue as read again, not what was typed before.
    await waitFor(async () => expect(await field("Region (optional)")).toHaveProperty("value", "Kampala Central"));
    await type("Capacity (optional)", "3200");
    save();
    await waitFor(() => expect(puts(requests)).toHaveLength(2));
    const second = puts(requests)[1]!;
    expect(second.headers.get("If-Match")).toBe('"5"');
    expect(second.body).toMatchObject({ region: "Kampala Central", capacity: 3200 });
  });


  it("checks the web address, and shows a taken one under it", async () => {
    const requests = open({
      [`PUT ${venuePath}`]: {
        status: 422,
        body: problem("VENUE_SLUG_TAKEN", 422, {
          detail: "Another venue has this web address.",
          errors: [{ field: "slug", code: "VENUE_SLUG_TAKEN" }],
        }),
      },
    });
    await type("Web address", "Bukoto Stadium");
    save();
    expect(await screen.findByText("Use lower-case letters and digits, with single hyphens between words.")).toBeTruthy();
    await type("Web address", "0199a3f0-0000-7000-8000-0000000000b2");
    save();
    expect(await screen.findByText("A web address can't look like a record's ID.")).toBeTruthy();
    expect(puts(requests)).toHaveLength(0);

    await type("Web address", "kisaasi-recreation-ground");
    save();
    expect(await screen.findByText("Another venue has this web address.")).toBeTruthy();
    expect((await field("Web address")).getAttribute("aria-invalid")).toBe("true");
  });

  it("isn't offered to a viewer: no Edit details in the header, and the page refuses them", async () => {
    open({}, reads, <VenuePage />);
    await screen.findByRole("heading", { level: 1, name: bukoto.name });
    expect(screen.queryByRole("link", { name: "Edit details" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Edit details" })).toBeNull();
    cleanup();

    open({}, reads);
    expect(await screen.findByText("You can't open this page")).toBeTruthy();
  });

  it("offers Edit details in the header to whoever may change the venue", async () => {
    open({}, registrar, <VenuePage />);
    const link = await screen.findByRole("link", { name: "Edit details" });
    expect(link.getAttribute("href")).toBe(`/t/kmsa/venues/${bukoto.id}/edit`);
  });
});
