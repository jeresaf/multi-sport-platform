import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Button } from "./button";

afterEach(cleanup);

describe("Button", () => {
  it("renders a button with token-based primary classes by default", () => {
    render(<Button>Save fixture</Button>);

    const button = screen.getByRole("button", { name: "Save fixture" });
    expect(button.getAttribute("type")).toBe("button");
    expect(button.className).toContain("bg-primary");
    expect(button.className).toContain("text-primary-contrast");
  });

  it("uses surface tokens for the secondary variant", () => {
    render(<Button variant="secondary">Cancel</Button>);

    const button = screen.getByRole("button", { name: "Cancel" });
    expect(button.className).toContain("bg-surface");
    expect(button.className).not.toContain("bg-primary");
  });

  it("calls onClick and respects disabled", () => {
    const onClick = vi.fn();
    render(
      <>
        <Button onClick={onClick}>Publish</Button>
        <Button onClick={onClick} disabled>
          Archive
        </Button>
      </>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Publish" }));
    fireEvent.click(screen.getByRole("button", { name: "Archive" }));

    expect(onClick).toHaveBeenCalledTimes(1);
  });
});
