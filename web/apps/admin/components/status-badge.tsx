import { Badge } from "@msp/ui/components/badge";
import { CircleCheck, CircleDashed, CircleMinus, CirclePause, CircleX, Clock, type LucideIcon } from "lucide-react";
import { humanizeCode } from "@/lib/codes";

// Every status has an icon and its label, never colour alone (design-system.md).
const icons: Readonly<Record<string, LucideIcon>> = {
  ACTIVE: CircleCheck,
  ACCEPTED: CircleCheck,
  PENDING: Clock,
  PROVISIONING: CircleDashed,
  SUSPENDED: CirclePause,
  EXPIRED: CircleMinus,
  ARCHIVED: CircleMinus,
  REMOVED: CircleX,
  REVOKED: CircleX,
};

/** A record's status (membership, invitation, tenant) as an icon and its name. */
export function StatusBadge({ status }: Readonly<{ status: string }>) {
  const Icon = icons[status] ?? CircleDashed;
  return (
    <Badge variant={status === "ACTIVE" ? "secondary" : "outline"}>
      <Icon aria-hidden="true" />
      {humanizeCode(status)}
    </Badge>
  );
}
