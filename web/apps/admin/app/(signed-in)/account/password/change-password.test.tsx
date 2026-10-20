// Changing one's own password: the API's answer to a wrong current password goes under that field.
import { cleanup, fireEvent, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fakeApi, problem, renderWithQuery } from "@/test/render";
import ChangePasswordPage from "./page";

vi.mock("next/navigation", () => ({
  useParams: () => ({}),
  usePathname: () => "/account/password",
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const me = {
  status: 200,
  body: { id: "0199a3f0-0000-7000-8000-000000000001", email: "grace.nakato@kmsa.example", platformRoles: [], tenants: [] },
};

function fill(current: string, next: string) {
  fireEvent.change(screen.getByLabelText("Current password"), { target: { value: current } });
  fireEvent.change(screen.getByLabelText("New password"), { target: { value: next } });
  fireEvent.change(screen.getByLabelText("Type it again"), { target: { value: next } });
  fireEvent.click(screen.getByRole("button", { name: "Change password" }));
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("change password", () => {
  it("sends the current and new password, and says when it worked", async () => {
    const requests = fakeApi({
      "GET /api/account/v1/me": me,
      "POST /api/account/v1/password/change": { status: 204 },
    });
    renderWithQuery(<ChangePasswordPage />);

    await waitFor(() => expect(screen.getByRole("button", { name: "Change password" }).hasAttribute("disabled")).toBe(false));
    fill("mango tree by the lake", "seven boda bodas at dawn");

    expect(await screen.findByText("Your password has been changed.")).toBeTruthy();
    const change = requests.find((request) => request.method === "POST")!;
    expect(change.body).toEqual({ currentPassword: "mango tree by the lake", newPassword: "seven boda bodas at dawn" });
    expect(change.headers.get("X-CSRF")).toBe("1");
  });

  it("puts a wrong current password under that field", async () => {
    fakeApi({
      "GET /api/account/v1/me": me,
      "POST /api/account/v1/password/change": { status: 400, body: problem("AUTH_CURRENT_PASSWORD_INVALID", 400) },
    });
    renderWithQuery(<ChangePasswordPage />);

    await waitFor(() => expect(screen.getByRole("button", { name: "Change password" }).hasAttribute("disabled")).toBe(false));
    fill("not my password at all", "seven boda bodas at dawn");

    await waitFor(() => expect(screen.getByLabelText("Current password").getAttribute("aria-invalid")).toBe("true"));
    expect(screen.queryByText("That didn't work")).toBeNull();
  });
});
