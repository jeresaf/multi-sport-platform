// The signed-in user (`GET /api/account/v1/me`) and signing out, shared by every screen.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, call, isUnauthenticated } from "./api";

export const meQueryKey = ["account", "me"] as const;

export function useMe() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: () => call(api.GET("/api/account/v1/me")),
  });
}

/** Signs out. A 401 means the session had already ended (idle or expired), which is the outcome asked for (#43 N2). */
export function useSignOut() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      try {
        await call(api.POST("/api/account/v1/logout"));
      } catch (error) {
        if (!isUnauthenticated(error)) {
          throw error;
        }
      }
    },
    // Empties every cached query; the ones on screen ask again (as signed out), so a page can carry on, e.g. accepting
    // an invitation after signing out of another account.
    onSuccess: () => queryClient.resetQueries(),
  });
}
