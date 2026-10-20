// The signed-in user (`GET /api/account/v1/me`) and signing out, shared by every screen.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, call, isUnauthenticated } from "./api";
import { leaveTo } from "./leave";
import { forgetWorkspace } from "./workspace";

export const meQueryKey = ["account", "me"] as const;

export function useMe() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: () => call(api.GET("/api/account/v1/me")),
  });
}

/** Ends the session. A 401 means it had already ended (idle or expired), which is the outcome asked for (#43 N2). */
async function signOut() {
  try {
    await call(api.POST("/api/account/v1/logout"));
  } catch (error) {
    if (!isUnauthenticated(error)) {
      throw error;
    }
  }
}

/** Signs out and stays on the page (accepting an invitation as someone else). */
export function useSignOut() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: signOut,
    // Empties every cached query; the ones on screen ask again (as signed out), so a page can carry on, e.g. accepting
    // an invitation after signing out of another account.
    onSuccess: () => queryClient.resetQueries(),
  });
}

/**
 * Signs out of the shell: a fresh page load of the sign-in page, so nothing the user saw stays in memory, and the
 * signed-in guard never sees the 401 that refetching would bring (it would add a return path to the sign-in link).
 */
export function useSignOutAndLeave() {
  return useMutation({
    mutationFn: signOut,
    onSuccess: () => {
      forgetWorkspace();
      leaveTo("/sign-in");
    },
  });
}
