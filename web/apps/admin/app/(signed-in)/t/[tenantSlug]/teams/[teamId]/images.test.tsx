// Registry → Teams → a team → the logo and the badge (S07 T6c-2b, D15): two controls in the header, each adding,
// changing and removing its own image at its own path from the version its dialog opened on; the badge shown on
// Overview; a file over 2 MB refused before sending and the API's refusal of a small image under the file; a conflict
// offers Reload; a disbanded or archived team gets no new image but its images can be removed; a viewer sees the images
// without a control.
import { cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readOnlyCapabilities } from "@/test/capabilities";
import { created, meteors, team, teamAnswers } from "@/test/teams";
import { fakeApi, problem, renderWithQuery, type Answers, type RecordedRequest } from "@/test/render";
import TenantWorkspaceLayout from "../../layout";
import type { Team } from "../teams-data";
import TeamPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "kmsa", teamId: "0199a3f0-0000-7000-8000-0000000000f1" }),
  usePathname: () => "/t/kmsa/teams/0199a3f0-0000-7000-8000-0000000000f1",
  useSearchParams: () => new URLSearchParams(),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const reads = ["team.read", "organisation.read", "venue.read", "sport.read", "tenant.settings.read"];
const changes = ["team.logo.set", "team.logo.remove", "team.badge.set", "team.badge.remove"];
const registrar = [...reads, ...changes];
const teamPath = `/api/admin/v1/teams/${meteors.id}`;

const image = (asset: string) => ({
  thumbnailUrl: `/media/${asset}/thumbnail?token=signed`,
  mediumUrl: `/media/${asset}/medium?token=signed`,
  expiresAt: "2026-11-27T12:00:00Z",
});
const logo = image("0199a3f0-0000-7000-8000-0000000000a1");
const badge = image("0199a3f0-0000-7000-8000-0000000000a2");
const withBoth = team({ logo, badge });

function open(shown: Team = meteors, capabilities: Parameters<typeof teamAnswers>[0] = registrar, answers: Answers = {}) {
  const requests = fakeApi({
    ...teamAnswers(capabilities),
    [`GET ${teamPath}`]: { status: 200, body: shown },
    [`GET ${teamPath}/status-changes`]: { status: 200, body: { items: [created] } },
    ...answers,
  });
  renderWithQuery(
    <TenantWorkspaceLayout>
      <TeamPage />
    </TenantWorkspaceLayout>,
  );
  return requests;
}

const sent = async (requests: RecordedRequest[], method: string, path: string) => {
  let found: RecordedRequest | undefined;
  await waitFor(() => {
    found = requests.find((request) => request.method === method && request.path === path);
    expect(found).toBeTruthy();
  });
  return found!;
};
const dialog = () => screen.getByRole("dialog");
const press = (name: string) => fireEvent.click(within(dialog()).getByRole("button", { name }));
const chooseFile = (label: string, file: File) =>
  fireEvent.change(within(dialog()).getByLabelText(label), { target: { files: [file] } });
const crest = () => new File(["\x89PNG crest"], "crest.png", { type: "image/png" });
const closed = () => waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
const announced = (text: string) =>
  waitFor(() => expect(screen.getAllByRole("status").map((item) => item.textContent)).toContain(text));

beforeEach(() => {
  // jsdom has no blob: URLs; the preview only needs one to show.
  URL.createObjectURL = vi.fn(() => "blob:crest");
  URL.revokeObjectURL = vi.fn();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("Team logo and badge", () => {
  it("adds a logo at the logo's path, then shows it in the header", async () => {
    const requests = open(meteors, registrar, {
      [`PUT ${teamPath}/logo`]: { status: 200, body: team({ logo, version: 6 }) },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add logo" }));
    expect(within(dialog()).getByRole("heading", { name: "Add the logo of Mutundwe Meteors FC" })).toBeTruthy();
    chooseFile("Logo", crest());
    press("Add logo");
    const upload = await sent(requests, "PUT", `${teamPath}/logo`);
    expect(upload.headers.get("If-Match")).toBe('"5"');
    await closed();
    expect(screen.getByRole("img", { name: "Logo of Mutundwe Meteors FC" }).getAttribute("src")).toBe(logo.mediumUrl);
    await announced("Logo added.");
    expect(requests.some((request) => request.path === `${teamPath}/badge`)).toBe(false);
  });

  it("adds a badge at the badge's path, then shows it on Overview", async () => {
    const requests = open(meteors, registrar, {
      [`PUT ${teamPath}/badge`]: { status: 200, body: team({ badge, version: 6 }) },
    });
    expect(await screen.findByRole("heading", { name: "Badge" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Add badge" }));
    expect(within(dialog()).getByRole("heading", { name: "Add the badge of Mutundwe Meteors FC" })).toBeTruthy();
    chooseFile("Badge", crest());
    press("Add badge");
    const upload = await sent(requests, "PUT", `${teamPath}/badge`);
    expect(upload.headers.get("If-Match")).toBe('"5"');
    await closed();
    expect(screen.getByRole("img", { name: "Badge of Mutundwe Meteors FC" }).getAttribute("src")).toBe(badge.mediumUrl);
    await announced("Badge added.");
    await waitFor(() => expect(document.activeElement?.textContent).toBe("Change badge"));
  });

  it("removes the badge alone, keeping the logo", async () => {
    const requests = open(withBoth, registrar, {
      [`DELETE ${teamPath}/badge`]: { status: 200, body: team({ logo, badge: null, version: 6 }) },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Remove badge" }));
    expect(within(dialog()).getByRole("heading", { name: "Remove the badge of Mutundwe Meteors FC" })).toBeTruthy();
    press("Remove badge");
    const removal = await sent(requests, "DELETE", `${teamPath}/badge`);
    expect(removal.headers.get("If-Match")).toBe('"5"');
    await closed();
    expect(screen.queryByRole("img", { name: "Badge of Mutundwe Meteors FC" })).toBeNull();
    expect(screen.getByRole("img", { name: "Logo of Mutundwe Meteors FC" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Remove logo" })).toBeTruthy();
    await announced("Badge removed.");
  });

  it("refuses a file over 2 MB before sending, and shows the API's refusal of a small image under the file", async () => {
    const requests = open(meteors, registrar, {
      [`PUT ${teamPath}/badge`]: {
        status: 422,
        body: problem("MEDIA_IMAGE_TOO_SMALL", 422, {
          detail: "The image is too small.",
          errors: [{ field: "file", code: "MEDIA_IMAGE_TOO_SMALL" }],
        }),
      },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add badge" }));
    const large = crest();
    Object.defineProperty(large, "size", { value: 2 * 1024 * 1024 + 1 });
    chooseFile("Badge", large);
    press("Add badge");
    expect(await within(dialog()).findByText(/larger than 2 MB/)).toBeTruthy();
    expect(requests.some((request) => request.method === "PUT")).toBe(false);

    chooseFile("Badge", crest());
    press("Add badge");
    await waitFor(() => expect(within(dialog()).getByLabelText("Badge").getAttribute("aria-invalid")).toBe("true"));
  });

  it("offers Reload when the team changed since the dialog opened", async () => {
    open(meteors, registrar, {
      [`PUT ${teamPath}/logo`]: { status: 409, body: problem("TEAM_CONCURRENCY_CONFLICT", 409) },
    });
    fireEvent.click(await screen.findByRole("button", { name: "Add logo" }));
    chooseFile("Logo", crest());
    press("Add logo");
    expect(await within(dialog()).findByText("This record changed since you opened it")).toBeTruthy();
    expect(within(dialog()).getByRole("button", { name: "Reload" })).toBeTruthy();
  });

  it.each([
    ["DISBANDED", "disbanded"],
    ["ARCHIVED", "archived"],
  ] as const)("adds no image to a %s team, but removes them", async (status, word) => {
    open(team({ logo, badge, status }));
    for (const image of ["logo", "badge"]) {
      const change = await screen.findByRole("button", { name: `Change ${image}` });
      expect(change).toHaveProperty("disabled", true);
      expect(document.getElementById(change.getAttribute("aria-describedby")!)?.textContent).toBe(
        `Mutundwe Meteors FC is ${word}: no new ${image} can be added.`,
      );
      expect(screen.getByRole("button", { name: `Remove ${image}` })).toHaveProperty("disabled", false);
    }
  });

  it("disables both controls with the reason on a read-only tenant", async () => {
    open(withBoth, readOnlyCapabilities(reads, changes));
    for (const name of ["Change logo", "Remove logo", "Change badge", "Remove badge"]) {
      const button = await screen.findByRole("button", { name });
      expect(button).toHaveProperty("disabled", true);
      expect(document.getElementById(button.getAttribute("aria-describedby")!)?.textContent).toMatch(/read-only/i);
    }
  });

  it("shows a viewer the logo and the badge without a control", async () => {
    open(withBoth, reads);
    expect(await screen.findByRole("img", { name: "Logo of Mutundwe Meteors FC" })).toBeTruthy();
    expect(screen.getByRole("img", { name: "Badge of Mutundwe Meteors FC" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /(logo|badge)$/ })).toBeNull();
    expect(screen.queryByRole("group", { name: "Logo" })).toBeNull();
  });

  it("says None on Overview without a badge", async () => {
    open(meteors, reads);
    const heading = await screen.findByRole("heading", { name: "Badge" });
    expect(heading.closest("section")?.textContent).toContain("None");
  });
});
