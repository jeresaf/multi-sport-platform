// What the user may do in the workspace on screen (docs/architecture/authorization.md, "UI behaviour"): the screens
// hide what the capabilities don't allow; the server still refuses it.
import { useQuery } from "@tanstack/react-query";
import { api, call, tenantApi } from "./api";

export function useTenantCapabilities(tenantId: string, enabled: boolean) {
  return useQuery({
    queryKey: ["capabilities", "tenant", tenantId],
    queryFn: () => call(tenantApi(tenantId).GET("/api/admin/v1/me/capabilities")),
    enabled,
  });
}

export function usePlatformCapabilities(enabled: boolean) {
  return useQuery({
    queryKey: ["capabilities", "platform"],
    queryFn: () => call(api.GET("/api/platform/v1/me/capabilities")),
    enabled,
  });
}

/** A suspended or archived tenant's status and why; the server answers whoever holds `tenant.access.read`. */
export function useTenantAccess(tenantId: string, enabled: boolean) {
  return useQuery({
    queryKey: ["tenant-access", tenantId],
    queryFn: () => call(tenantApi(tenantId).GET("/api/admin/v1/tenant/access")),
    enabled,
  });
}
