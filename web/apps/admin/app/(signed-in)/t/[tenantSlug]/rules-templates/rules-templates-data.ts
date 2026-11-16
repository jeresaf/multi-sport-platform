"use client";

// Tenant workspace → Rules templates data (S05 D16): the published ruleset templates and a template. Templates change
// only by publishing a new version (D4), so a read is kept for the session.
import { useQuery } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { useTenantWorkspace } from "@/lib/tenant-workspace";

/** Not read without `ruleset.read` (`enabled`): the request would only be refused. */
export function useRulesetTemplates() {
  const { client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["ruleset-templates", "list"],
    queryFn: () => call(client.GET("/api/admin/v1/ruleset-templates")),
    enabled: can("ruleset.read"),
    staleTime: Infinity,
  });
}

/** A template: the latest version, or `version` when the address names one; `enabled: false` skips it. */
export function useRulesetTemplate(code: string, version?: number, enabled = true) {
  const { client, can } = useTenantWorkspace();
  return useQuery({
    queryKey: ["ruleset-templates", "template", code, version ?? "latest"],
    queryFn: () =>
      call(client.GET("/api/admin/v1/ruleset-templates/{code}", { params: { path: { code }, query: { version } } })),
    enabled: enabled && can("ruleset.read"),
    staleTime: Infinity,
  });
}

