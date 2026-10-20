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

  // Inside a form, a button without a type would submit it (a Cancel in a dialog, a show-password toggle).
  it("is a plain button unless a type is given", () => {
    render(
      <form>
        <Button>Cancel</Button>
        <Button type="submit">Save</Button>
      </form>,
    );

    expect(screen.getByRole("button", { name: "Cancel" }).getAttribute("type")).toBe("button");
    expect(screen.getByRole("button", { name: "Save" }).getAttribute("type")).toBe("submit");
  });

  // WCAG 2.2 AA (design-system.md): a solid focus ring, and the danger colour at full strength in dark mode too.
  it("has a solid focus ring and an opaque destructive background", () => {
    render(<Button variant="destructive">Suspend tenant</Button>);

    const classes = screen.getByRole("button", { name: "Suspend tenant" }).className.split(" ");
    expect(classes).toContain("focus-visible:ring-ring");
    expect(classes.filter((c) => /ring-(ring|destructive)\/\d+/.test(c))).toEqual([]);
    expect(classes.filter((c) => /bg-destructive\/\d+/.test(c) && !c.startsWith("hover:"))).toEqual([]);
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

    const link = screen.getByRole("link", { name: "Sign in" });
    expect(link.className).toContain("bg-primary");
    expect(link.hasAttribute("type")).toBe(false);
  });
});
