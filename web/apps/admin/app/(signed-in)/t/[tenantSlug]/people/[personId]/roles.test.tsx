// Registry → People → a person → Roles (S06 D12, T7c): adding, changing and moving role profiles. Each control shows
// only with its action; a profile's sport, positions and roles come from the tenant's enabled sports and the test
// definition (no sport word is ours); every change sends the person's version and shows the person the API answers.
import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { denis, john, person } from "@/test/people";
import { personPageAnswers, tenantId } from "@/test/person-page";
import { ringball } from "@/test/sport-definitions";
import { fakeApi, problem, renderWithQuery, type Answer, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import { personQueryKey, type Person } from "../people-data";
import PersonPage from "./page";

const navigation = vi.hoisted(() => ({ personId: "" }));

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", personId: navigation.personId }),
  usePathname: () => `/t/kmsa/people/${navigation.personId}`,
  useSearchParams: () => new URLSearchParams("tab=roles"),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

// VIEWER's reads (roles.md), the tenant's sports included: the tab still doesn't ask for them.
const viewer = ["person.read", "sport.read", "tenant.settings.read", "tenant.sport.read"];
const profileWrites = [
  "athlete_profile.create",
  "athlete_profile.update",
  "athlete_profile.status.change",
  "staff_profile.create",
  "staff_profile.update",
  "staff_profile.status.change",
  "official_profile.create",
  "official_profile.update",
  "official_profile.status.change",
];
const registrar = [...viewer, "person.read_private", ...profileWrites];

const tenantSport = (variant: string, name: string, enabled = true) => ({
  variant,
  sport: "RINGBALL",
  name,
  scoringFamily: "ADDITIVE",
  enabled,
  enabledSince: enabled ? "2026-10-01T07:00:00Z" : null,
});
const enabledSports: Answer = {
  status: 200,
  body: {
    variants: [
      tenantSport("RINGBALL_SEVENS", "Ringball sevens"),
      tenantSport("RINGBALL_FIVES", "Ringball fives"),
      tenantSport("RINGBALL_TENS", "Ringball tens", false),
    ],
    sportQuota: null,
  },
};

/** Ivan Mukasa, archived (the slice demo): his profiles can be corrected or closed, never added to. */
const ivan = person({
  id: "0199a3f0-0000-7000-8000-0000000000e9",
  firstName: "Ivan",
  lastName: "Mukasa",
  displayName: "Ivan Mukasa",
  status: "ARCHIVED",
  officialProfiles: denis.officialProfiles,
});

let rendered: ReturnType<typeof renderWithQuery>;

function open(shown: Person, capabilities: Parameters<typeof personPageAnswers>[1] = registrar, answers: Answers = {}) {
  navigation.personId = shown.id;
  const requests = fakeApi({
    ...personPageAnswers(shown, capabilities),
    "GET /api/admin/v1/tenant/sports": enabledSports,
    ...answers,
  });
  rendered = renderWithQuery(
    <TenantWorkspaceLayout>
      <PersonPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const sent = async (requests: RecordedRequest[], method: string, path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === method && request.path === path && request.body !== undefined);
    expect(found).toBeTruthy();
  });
  return found!;
};
const dialog = () => screen.getByRole("dialog");
/** Clicks an Add button once it is enabled (it waits for the tenant's sports). */
const add = async (name: string) => {
  const button = await screen.findByRole("button", { name });
  await waitFor(() => expect(button).toHaveProperty("disabled", false));
  fireEvent.click(button);
  // The form's choices come from the definition, read ahead.
  await within(dialog()).findAllByRole("option");
};
const choose = (label: string, value: string) =>
  fireEvent.change(within(dialog()).getByLabelText(label), { target: { value } });

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("roles", () => {
  it("shows a viewer the profiles without a control, and doesn't read the tenant's sports", async () => {
    const requests = open(denis, viewer);

    expect(await screen.findByText("Wing ringer")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^Add .* profile$/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Edit/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Retire/ })).toBeNull();
    expect(requests.some((request) => request.path === "/api/admin/v1/tenant/sports")).toBe(false);
  });

  it("adds an athlete profile for an enabled sport with the definition's positions", async () => {
    const added = person({
      athleteProfiles: [
        {
          id: "0199a3f0-0000-7000-8000-0000000000b9",
          variant: "RINGBALL_SEVENS",
          primaryPosition: "WING_RINGER",
          otherPositions: ["HOOP_GUARD"],
          dominantSide: "LEFT",
          publicName: "J. Ssekandi",
          status: "ACTIVE",
        },
      ],
      version: 8,
    });
    const requests = open(john, registrar, {
      [`POST /api/admin/v1/people/${john.id}/athlete-profiles`]: { status: 201, body: added },
    });

    expect(await screen.findByText("No roles yet")).toBeTruthy();
    await add("Add athlete profile");
    expect(within(dialog()).getByRole("heading", { name: "Add an athlete profile for John Ssekandi" })).toBeTruthy();
    // Only the enabled sports.
    const sports = within(within(dialog()).getByLabelText("Sport")).getAllByRole("option").map((option) => option.textContent);
    expect(sports).toEqual(["Ringball sevens", "Ringball fives"]);

    await within(dialog()).findByRole("option", { name: "Wing ringer" });
    choose("Primary position (optional)", "WING_RINGER");
    // The primary position isn't offered again among the others.
    expect(within(dialog()).queryByRole("checkbox", { name: "Wing ringer" })).toBeNull();
    fireEvent.click(within(dialog()).getByRole("checkbox", { name: "Hoop guard" }));
    choose("Dominant side (optional)", "LEFT");
    fireEvent.change(within(dialog()).getByLabelText("Public name (optional)"), { target: { value: "  J. Ssekandi " } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Add profile" }));

    const request = await sent(requests, "POST", `/api/admin/v1/people/${john.id}/athlete-profiles`);
    expect(request.headers.get("If-Match")).toBe('"7"');
    expect(request.body).toEqual({
      variant: "RINGBALL_SEVENS",
      primaryPosition: "WING_RINGER",
      otherPositions: ["HOOP_GUARD"],
      dominantSide: "LEFT",
      publicName: "J. Ssekandi",
    });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(within(screen.getByRole("tabpanel")).getByRole("status").textContent).toBe("Ringball sevens profile added.");
    expect(screen.getByText("J. Ssekandi")).toBeTruthy();
    // Focus goes to the new profile's heading.
    await waitFor(() => expect(document.activeElement?.id).toBe("profile-0199a3f0-0000-7000-8000-0000000000b9"));
    expect(screen.queryByText("No roles yet")).toBeNull();
  });

  it("shows a refused position under its field, and keeps the dialog open", async () => {
    open(john, registrar, {
      [`POST /api/admin/v1/people/${john.id}/athlete-profiles`]: {
        status: 422,
        body: problem("PERSON_POSITION_UNKNOWN", 422, { errors: [{ field: "otherPositions[0]", code: "PERSON_POSITION_UNKNOWN" }] }),
      },
    });

    await add("Add athlete profile");
    fireEvent.click(await within(dialog()).findByRole("checkbox", { name: "Hoop guard" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Add profile" }));

    const fieldset = within(dialog()).getByRole("group", { name: /Other positions/ });
    expect(await within(fieldset).findByText("The sport variant's definition has no such position.")).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("offers only the profiles a person can still take", async () => {
    // Two sports are enabled and Denis plays one, so the other is still free; his staff profile is open and his
    // official one is inactive (still open), so neither kind is offered again.
    open(denis);

    await add("Add athlete profile");
    expect(screen.queryByRole("button", { name: "Add staff profile" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Add official profile" })).toBeNull();
    const sports = within(within(dialog()).getByLabelText("Sport")).getAllByRole("option").map((option) => option.textContent);
    expect(sports).toEqual(["Ringball fives"]);
  });

  it("changes a staff profile's roles as a whole, refusing the same sport and role twice", async () => {
    const staff = denis.staffProfiles[0]!;
    const requests = open(denis, registrar, {
      [`PUT /api/admin/v1/people/${denis.id}/staff-profiles/${staff.id}`]: { status: 200, body: { ...denis, version: 8 } },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Edit: Staff profile" }));
    expect(within(dialog()).getByLabelText("Qualification (optional)")).toHaveProperty("value", "Level 1 coaching certificate");
    fireEvent.click(within(dialog()).getByRole("button", { name: "Add another role" }));
    const role2 = within(dialog()).getAllByLabelText("Role")[1]!;
    fireEvent.change(role2, { target: { value: "HEAD_COACH" } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" }));
    expect(await within(dialog()).findByText("This sport and role is already listed.")).toBeTruthy();
    expect(requests.some((request) => request.method === "PUT")).toBe(false);

    fireEvent.change(role2, { target: { value: "KIT_KEEPER" } });
    fireEvent.change(within(dialog()).getAllByLabelText("Qualification (optional)")[1]!, { target: { value: " Club kit store " } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" }));

    const request = await sent(requests, "PUT", `/api/admin/v1/people/${denis.id}/staff-profiles/${staff.id}`);
    expect(request.headers.get("If-Match")).toBe('"7"');
    expect(request.body).toEqual({
      capabilities: [
        { variant: "RINGBALL_SEVENS", role: "HEAD_COACH", qualification: "Level 1 coaching certificate" },
        { variant: "RINGBALL_SEVENS", role: "KIT_KEEPER", qualification: "Club kit store" },
      ],
    });
    await waitFor(() => expect(within(screen.getByRole("tabpanel")).getByRole("status").textContent).toBe("The staff profile is saved."));
  });

  it("adds an official profile with a licence", async () => {
    const requests = open(john, registrar, {
      [`POST /api/admin/v1/people/${john.id}/official-profiles`]: { status: 201, body: { ...john, version: 8 } },
    });

    await add("Add official profile");
    await within(dialog()).findByRole("option", { name: "Timekeeper" });
    choose("Role", "TIMEKEEPER");
    fireEvent.change(within(dialog()).getByLabelText("Grade (optional)"), { target: { value: "Class 3" } });
    fireEvent.change(within(dialog()).getByLabelText("Licence number (optional)"), { target: { value: "TK-0042" } });
    fireEvent.change(within(dialog()).getByLabelText("Licence valid until (optional)"), { target: { value: "2027-06-30" } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Add profile" }));

    const request = await sent(requests, "POST", `/api/admin/v1/people/${john.id}/official-profiles`);
    expect(request.body).toEqual({
      capabilities: [
        { variant: "RINGBALL_SEVENS", role: "TIMEKEEPER", grade: "Class 3", licenceNumber: "TK-0042", licenceValidUntil: "2027-06-30" },
      ],
    });
  });

  it("shows a refused role under its row", async () => {
    open(john, registrar, {
      [`POST /api/admin/v1/people/${john.id}/staff-profiles`]: {
        status: 422,
        body: problem("PERSON_ROLE_UNKNOWN", 422, { errors: [{ field: "capabilities[0].role", code: "PERSON_ROLE_UNKNOWN" }] }),
      },
    });

    await add("Add staff profile");
    await within(dialog()).findByRole("option", { name: "Head coach" });
    choose("Role", "HEAD_COACH");
    fireEvent.click(within(dialog()).getByRole("button", { name: "Add profile" }));

    expect(await within(dialog()).findByText("The sport variant's definition has no such role.")).toBeTruthy();
    expect(within(dialog()).getByLabelText("Role").getAttribute("aria-invalid")).toBe("true");
    // Shown under the field, so the alert doesn't repeat it.
    expect(within(dialog()).queryByRole("alert")).toBeNull();
  });

  it("offers each profile only its allowed moves, and asks a reason before moving", async () => {
    const athlete = denis.athleteProfiles[0]!;
    const requests = open(denis, registrar, {
      [`POST /api/admin/v1/people/${denis.id}/athlete-profiles/${athlete.id}/status`]: {
        status: 200,
        body: { ...denis, athleteProfiles: [{ ...athlete, status: "RETIRED" }], version: 8 },
      },
    });

    const card = (name: string) => screen.getByRole("article", { name });
    await screen.findByText("Wing ringer");
    const moves = (name: string) =>
      within(card(name))
        .getAllByRole("button")
        .map((button) => button.getAttribute("aria-label"))
        .filter((name) => !name?.startsWith("Edit"));
    expect(moves("Ringball sevens")).toEqual([
      "Make inactive: Ringball sevens athlete profile",
      "Retire: Ringball sevens athlete profile",
      "Archive: Ringball sevens athlete profile",
    ]);
    expect(moves("Official profile")).toEqual([
      "Make active: Official profile",
      "Retire: Official profile",
      "Archive: Official profile",
    ]);

    fireEvent.click(within(card("Ringball sevens")).getByRole("button", { name: "Retire: Ringball sevens athlete profile" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Retire" }));
    expect(await within(dialog()).findByText("Give a reason: it is kept in the audit trail.")).toBeTruthy();
    fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Stopped playing this season" } });
    fireEvent.click(within(dialog()).getByRole("button", { name: "Retire" }));

    const request = await sent(requests, "POST", `/api/admin/v1/people/${denis.id}/athlete-profiles/${athlete.id}/status`);
    expect(request.body).toEqual({ status: "RETIRED", reason: "Stopped playing this season" });
    await waitFor(() => expect(within(card("Ringball sevens")).getByText("Retired")).toBeTruthy());
    // The Retire button is gone: focus goes to the profile's heading.
    await waitFor(() => expect(document.activeElement?.id).toBe(`profile-${athlete.id}`));
    // Retired: it can be made active again or archived.
    expect(moves("Ringball sevens")).toEqual([
      "Make active: Ringball sevens athlete profile",
      "Archive: Ringball sevens athlete profile",
    ]);
  });

  it("adds nothing to an archived person, and never makes a profile active again", async () => {
    open(ivan);

    expect(await screen.findByText("Ivan Mukasa is archived: no new role can be added.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^Add .* profile$/ })).toBeNull();
    const official = screen.getByRole("article", { name: "Official profile" });
    expect(within(official).queryByRole("button", { name: /^Make active/ })).toBeNull();
    expect(within(official).getByRole("button", { name: "Retire: Official profile" })).toBeTruthy();

    // Its roles can be corrected but not added to.
    fireEvent.click(within(official).getByRole("button", { name: "Edit: Official profile" }));
    expect(within(dialog()).getByText(/the roles held can be kept, changed in their details or removed, but none added/)).toBeTruthy();
    expect(within(dialog()).queryByRole("button", { name: "Add another role" })).toBeNull();
    expect(within(dialog()).queryByLabelText("Role")).toBeNull();
    expect(within(dialog()).getByLabelText("Grade (optional)")).toHaveProperty("value", "Class 2");
  });

  it("marks a position the latest definition dropped, and asks for another before saving", async () => {
    const athlete = denis.athleteProfiles[0]!;
    const requests = open(denis, registrar, {
      "GET /api/admin/v1/sports/RINGBALL_SEVENS": {
        status: 200,
        body: { ...ringball, positions: ringball.positions.filter((position) => position.code !== "WING_RINGER") },
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Edit: Ringball sevens athlete profile" }));
    const primary = within(dialog()).getByLabelText("Primary position (optional)");
    expect(within(primary).getByRole("option", { name: "Wing ringer (no longer in the sport's definition)" })).toBeTruthy();
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" }));
    expect(
      await within(dialog()).findByText("Wing ringer is no longer in the sport's definition: choose another position or none."),
    ).toBeTruthy();
    expect(requests.some((request) => request.method === "PUT")).toBe(false);

    choose("Primary position (optional)", "CENTRE_RINGER");
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" }));
    const request = await sent(requests, "PUT", `/api/admin/v1/people/${denis.id}/athlete-profiles/${athlete.id}`);
    expect(request.body).toMatchObject({ primaryPosition: "CENTRE_RINGER", otherPositions: ["HOOP_GUARD"] });
  });

  it("on an archived person, shows a dropped or refused role under its locked row", async () => {
    const requests = open(ivan, registrar, {
      "GET /api/admin/v1/sports/RINGBALL_SEVENS": {
        status: 200,
        body: { ...ringball, officialRoles: ringball.officialRoles.filter((role) => role.code !== "RING_JUDGE") },
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Edit: Official profile" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" }));
    // The profile's only role can't be removed, so the page says what can be done instead.
    const message = await within(dialog()).findByText(
      "Ring judge is no longer in the sport's definition: this profile can't be corrected; retire or archive it instead.",
    );
    expect(message.getAttribute("role")).toBe("alert");
    expect(within(dialog()).getByRole("button", { name: "Remove role 1" }).getAttribute("aria-describedby")).toBe(message.id);
    expect(requests.some((request) => request.method === "PUT")).toBe(false);
    cleanup();

    // A refusal the page couldn't foresee is shown there too, not lost on a field the row doesn't show.
    open(ivan, registrar, {
      [`PUT /api/admin/v1/people/${ivan.id}/official-profiles/${ivan.officialProfiles[0]!.id}`]: {
        status: 422,
        body: problem("PERSON_ROLE_UNKNOWN", 422, { errors: [{ field: "capabilities[0].role", code: "PERSON_ROLE_UNKNOWN" }] }),
      },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Edit: Official profile" }));
    await waitFor(() => expect(within(dialog()).getByLabelText("Grade (optional)")).toBeTruthy());
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" }));
    expect(await within(dialog()).findByText("The sport variant's definition has no such role.")).toBeTruthy();
  });

  it("keeps a held role on a sport since disabled, but offers only enabled sports to a new row", async () => {
    const staff = {
      ...denis.staffProfiles[0]!,
      capabilities: [{ variant: "RINGBALL_TENS", role: "HEAD_COACH", qualification: null }],
    };
    open({ ...denis, staffProfiles: [staff] });

    fireEvent.click(await screen.findByRole("button", { name: "Edit: Staff profile" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Add another role" }));
    const [held, added] = within(dialog()).getAllByLabelText("Sport");
    const names = (select: HTMLElement) => within(select).getAllByRole("option").map((option) => option.textContent);
    expect(names(held!)).toEqual(["Ringball sevens", "Ringball fives", "Ringball tens"]);
    expect(names(added!)).toEqual(["Ringball sevens", "Ringball fives"]);
  });

  // Every dialog keeps the version it opened on (#110 R1, R7): the official and athlete editors and a status move.
  const official = denis.officialProfiles[0]!;
  const athlete = denis.athleteProfiles[0]!;
  it.each([
    {
      dialogName: "the official profile editor",
      opener: "Edit: Official profile",
      method: "PUT",
      path: `/api/admin/v1/people/${denis.id}/official-profiles/${official.id}`,
      submit: () => fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" })),
    },
    {
      dialogName: "the athlete profile editor",
      opener: "Edit: Ringball sevens athlete profile",
      method: "PUT",
      path: `/api/admin/v1/people/${denis.id}/athlete-profiles/${athlete.id}`,
      submit: () => fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" })),
    },
    {
      dialogName: "a status move",
      opener: "Retire: Ringball sevens athlete profile",
      method: "POST",
      path: `/api/admin/v1/people/${denis.id}/athlete-profiles/${athlete.id}/status`,
      submit: () => {
        fireEvent.change(within(dialog()).getByLabelText("Reason"), { target: { value: "Stopped playing this season" } });
        fireEvent.click(within(dialog()).getByRole("button", { name: "Retire" }));
      },
    },
  ])("$dialogName sends the version it opened on, though the person is read again while it is open", async ({ opener, method, path, submit }) => {
    const requests = open(denis, registrar, { [`${method} ${path}`]: { status: 200, body: { ...denis, version: 9 } } });

    fireEvent.click(await screen.findByRole("button", { name: opener }));
    // Someone else's change arrives with a later read (the photo's URLs are read again before they expire).
    await act(async () => {
      rendered.queryClient.setQueryData(personQueryKey(tenantId, denis.id), { ...denis, version: 8 });
      // The cache tells its observers on the next tick.
      await new Promise((resolve) => setTimeout(resolve, 0));
    });
    submit();

    const request = await sent(requests, method, path);
    expect(request.headers.get("If-Match")).toBe('"7"');
  });

  it("keeps a held position as a choice when the sport's definition can't be read, and says so", async () => {
    open(denis, registrar, {
      "GET /api/admin/v1/sports/RINGBALL_SEVENS": { status: 500, body: problem("PLATFORM_UNEXPECTED_ERROR", 500) },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Edit: Ringball sevens athlete profile" }));
    expect(await within(dialog()).findByText("The sport's positions couldn't be read. Close this and try again.")).toBeTruthy();
    const primary = within(dialog()).getByLabelText("Primary position (optional)") as HTMLSelectElement;
    expect(primary.value).toBe("WING_RINGER");
    expect(within(primary).getByRole("option", { name: "Wing ringer" })).toBeTruthy();
    expect(within(dialog()).getByRole("checkbox", { name: "Hoop guard", checked: true })).toBeTruthy();
  });

  it("on a read-only tenant, shows the controls disabled with the reason", async () => {
    open(denis, readOnlyCapabilities(viewer, profileWrites));

    const add = await screen.findByRole("button", { name: "Add athlete profile" });
    expect(add).toHaveProperty("disabled", true);
    expect(screen.getByText("Read-only: the subscription was cancelled")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Edit: Staff profile" })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "Retire: Official profile" }).getAttribute("aria-describedby")).toBe(
      "roles-unavailable",
    );
  });

  it("says a sport must be enabled first when none is", async () => {
    open(john, registrar, {
      "GET /api/admin/v1/tenant/sports": { status: 200, body: { variants: [tenantSport("RINGBALL_TENS", "Ringball tens", false)], sportQuota: null } },
    });

    const note = await screen.findByText(/No sport is enabled for your organisation yet/);
    const add = screen.getByRole("button", { name: "Add athlete profile" });
    expect(add).toHaveProperty("disabled", true);
    expect(add.getAttribute("aria-describedby")).toBe(note.id);
  });

  it("after a concurrency conflict, offers to reload the person instead of saving", async () => {
    let reads = 0;
    open(denis, registrar, {
      [`GET /api/admin/v1/people/${denis.id}`]: () => {
        reads += 1;
        return { status: 200, body: denis };
      },
      [`PUT /api/admin/v1/people/${denis.id}/official-profiles/${denis.officialProfiles[0]!.id}`]: {
        status: 409,
        body: problem("PERSON_CONCURRENCY_CONFLICT", 409),
      },
    });

    fireEvent.click(await screen.findByRole("button", { name: "Edit: Official profile" }));
    fireEvent.click(within(dialog()).getByRole("button", { name: "Save profile" }));
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    expect(within(dialog()).getByRole("button", { name: "Save profile" })).toHaveProperty("disabled", true);

    fireEvent.click(within(dialog()).getByRole("button", { name: "Reload" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    await waitFor(() => expect(reads).toBe(2));
  });
});
