import { Badge } from "@msp/ui/components/badge";
import {
  AlarmClock,
  CalendarClock,
  CircleCheck,
  CircleDashed,
  CircleMinus,
  CirclePause,
  CircleX,
  Clock,
  Hourglass,
  type LucideIcon,
} from "lucide-react";
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
  SCHEDULED: CalendarClock,
  EXPIRING: AlarmClock,
  GRACE: Hourglass,
  CANCELLED: CircleX,
  INACTIVE: CirclePause,
  DECEASED: CircleMinus,
  RETIRED: CircleMinus,
  WITHDRAWN: CircleX,
  TEMPORARILY_UNAVAILABLE: CirclePause,
  CLOSED: CircleX,
  DISBANDED: CircleMinus,
  DISSOLVED: CircleMinus,
};

/** A record's status (membership, invitation, tenant, subscription period, person, profile, consent, venue, organisation, team) as an icon and its name. */
export function StatusBadge({ status }: Readonly<{ status: string }>) {
  const Icon = icons[status] ?? CircleDashed;
  return (
    <Badge variant={status === "ACTIVE" ? "secondary" : "outline"}>
      <Icon aria-hidden="true" />
      {humanizeCode(status)}
    </Badge>
  );
}
