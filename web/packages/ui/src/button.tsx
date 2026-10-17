import type { ButtonHTMLAttributes } from "react";

export type ButtonVariant = "primary" | "secondary";

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
};

const base =
  "inline-flex min-h-11 items-center justify-center gap-2 rounded-md px-4 text-sm font-medium " +
  "transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus " +
  "disabled:pointer-events-none disabled:opacity-50";

const variants: Record<ButtonVariant, string> = {
  primary: "bg-primary text-primary-contrast hover:bg-primary-hover",
  secondary: "border border-border bg-surface text-text hover:bg-surface-raised",
};

export function Button({ variant = "primary", className, type = "button", ...props }: ButtonProps) {
  const classes = [base, variants[variant], className].filter(Boolean).join(" ");
  return <button type={type} className={classes} {...props} />;
}
