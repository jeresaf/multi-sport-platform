import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Button } from "./button";

afterEach(cleanup);

describe("Button", () => {
  it("is a token-coloured button with a 44 px touch target by default", () => {
    render(<Button>Save fixture</Button>);

    const button = screen.getByRole("button", { name: "Save fixture" });
    expect(button.className).toContain("bg-primary");
    expect(button.className).toContain("text-primary-foreground");
    expect(button.className).toContain("h-11");
  });

  it("uses the muted surface for secondary, not the brand secondary colour", () => {
    render(<Button variant="secondary">Cancel</Button>);

    const button = screen.getByRole("button", { name: "Cancel" });
    expect(button.className).toContain("bg-muted");
    expect(button.className).not.toContain("bg-secondary");
  });

  it("uses the danger tokens for destructive", () => {
    render(<Button variant="destructive">Remove member</Button>);

    const button = screen.getByRole("button", { name: "Remove member" });
    expect(button.className).toContain("bg-destructive");
    expect(button.className).toContain("text-destructive-foreground");
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

  it("renders its child as the element with asChild", () => {
    render(
      <Button asChild>
        <a href="/sign-in">Sign in</a>
      </Button>,
    );

    expect(screen.getByRole("link", { name: "Sign in" }).className).toContain("bg-primary");
  });
});
