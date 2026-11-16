"use client";

// Platform console → Rules templates data (S05 D16): the published ruleset templates and a template, through the
// platform reads (`platform.sport.read`). Templates change only by publishing a new version (D4), so a read is kept
// for the session.
import { useQuery } from "@tanstack/react-query";
import { api, call } from "@/lib/api";
import { usePlatformCan } from "@/lib/platform-workspace";

/** Not read without `platform.sport.read` (`enabled`): the request would only be refused. */
export function usePlatformRulesetTemplates() {
  const can = usePlatformCan();
  return useQuery({
    queryKey: ["platform", "ruleset-templates", "list"],
    queryFn: () => call(api.GET("/api/platform/v1/ruleset-templates")),
    enabled: can("platform.sport.read"),
    staleTime: Infinity,
  });
}

/** A template: the latest version, or `version` when the address names one; `enabled: false` skips it. */
export function usePlatformRulesetTemplate(code: string, version?: number, enabled = true) {
  const can = usePlatformCan();
  return useQuery({
    queryKey: ["platform", "ruleset-templates", "template", code, version ?? "latest"],
    queryFn: () =>
      call(api.GET("/api/platform/v1/ruleset-templates/{code}", { params: { path: { code }, query: { version } } })),
    enabled: enabled && can("platform.sport.read"),
    staleTime: Infinity,
  });
}
