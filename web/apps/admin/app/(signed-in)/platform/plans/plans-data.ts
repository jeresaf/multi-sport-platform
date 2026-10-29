"use client";

// Platform console → Plans data: the active plans in rank order, each with every entitlement's definition and value.
// Plans change only by a reviewed seed (S04 D2), so the list is read once per session.
import type { components } from "@msp/api-client";
import { useQuery } from "@tanstack/react-query";
import { api, call } from "@/lib/api";

export type Plan = components["schemas"]["PlanResponse"];

export const plansQueryKey = ["platform", "plans"] as const;

/** Not read without `plan.read` (`enabled`): the request would only be refused. */
export function usePlans(enabled: boolean) {
  return useQuery({
    enabled,
    queryKey: plansQueryKey,
    queryFn: () => call(api.GET("/api/platform/v1/plans")),
    staleTime: Infinity,
  });
}
