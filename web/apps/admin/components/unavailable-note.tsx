import { Lock } from "lucide-react";

/**
 * Why the controls next to it are disabled (S04 D19): "Read-only: the subscription has ended", "Your plan allows 5 staff
 * users". Shown as text, not a tooltip, so it reads on a phone; the disabled controls point to it with
 * `aria-describedby={id}`.
 */
export function UnavailableNote({ id, reason }: Readonly<{ id: string; reason: string }>) {
  return (
    <p id={id} className="flex items-start gap-2 rounded-md border bg-muted p-3 text-sm">
      <Lock aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
      <span>{reason}</span>
    </p>
  );
}
