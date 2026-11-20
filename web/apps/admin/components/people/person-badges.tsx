import type { components } from "@msp/api-client";
import { Badge } from "@msp/ui/components/badge";
import { CircleHelp, ShieldAlert } from "lucide-react";

type AgeStatus = components["schemas"]["AgeStatus"];

/**
 * "Minor" or "Age unknown" (S06 D13): a person who isn't known to be an adult is protected as a minor (ADR-0015), so
 * both are marked; an adult has no badge.
 */
export function AgeBadge({ ageStatus }: Readonly<{ ageStatus: AgeStatus }>) {
  if (ageStatus === "ADULT") {
    return null;
  }
  const Icon = ageStatus === "MINOR" ? ShieldAlert : CircleHelp;
  return (
    <Badge variant="outline">
      <Icon aria-hidden="true" />
      {ageStatus === "MINOR" ? "Minor" : "Age unknown"}
    </Badge>
  );
}
