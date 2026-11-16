"use client";

// Platform console → Formats data (S05 D16): the published formats and a format, through the platform reads
// (`platform.sport.read`). Formats change only by publishing a new version (D4), so a read is kept for the session.
import { useQuery } from "@tanstack/react-query";
import { api, call } from "@/lib/api";
import { usePlatformCan } from "@/lib/platform-workspace";

/** Not read without `platform.sport.read` (`enabled`): the request would only be refused. */
export function usePlatformFormats() {
  const can = usePlatformCan();
  return useQuery({
    queryKey: ["platform", "formats", "list"],
    queryFn: () => call(api.GET("/api/platform/v1/formats")),
    enabled: can("platform.sport.read"),
    staleTime: Infinity,
  });
}

/** A format: the latest version, or `version` when the address names one; `enabled: false` skips it. */
export function usePlatformFormat(code: string, version?: number, enabled = true) {
  const can = usePlatformCan();
  return useQuery({
    queryKey: ["platform", "formats", "format", code, version ?? "latest"],
    queryFn: () => call(api.GET("/api/platform/v1/formats/{code}", { params: { path: { code }, query: { version } } })),
    enabled: enabled && can("platform.sport.read"),
    staleTime: Infinity,
  });
}
