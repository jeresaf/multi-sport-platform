"use client";

// Platform console → Sports data (S05 D16): the published variants and a variant's definition, through the platform
// reads (`platform.sport.read`). Definitions change only by publishing a new version (D4), so a read is kept for the
// session. The keys start with "platform" so they never meet a tenant workspace's.
import { useQuery } from "@tanstack/react-query";
import { api, call } from "@/lib/api";
import { usePlatformCan } from "@/lib/platform-workspace";

/** Not read without `platform.sport.read` (`enabled`): the request would only be refused. */
export function usePlatformSportVariants() {
  const can = usePlatformCan();
  return useQuery({
    queryKey: ["platform", "sports", "variants"],
    queryFn: () => call(api.GET("/api/platform/v1/sports")),
    enabled: can("platform.sport.read"),
    staleTime: Infinity,
  });
}

/** A variant's definition: the latest version, or `version` when the address names one; `enabled: false` skips it. */
export function usePlatformSportDefinition(variant: string, version?: number, enabled = true) {
  const can = usePlatformCan();
  return useQuery({
    queryKey: ["platform", "sports", "definition", variant, version ?? "latest"],
    queryFn: () =>
      call(api.GET("/api/platform/v1/sports/{variant}", { params: { path: { variant }, query: { version } } })),
    enabled: enabled && can("platform.sport.read"),
    staleTime: Infinity,
  });
}
