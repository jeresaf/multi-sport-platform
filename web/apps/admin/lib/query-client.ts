// The admin app's query client (providers.tsx; tests use it too, so they see the same behaviour).
import { hashKey, MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";
import { meQueryKey } from "./account";
import { isUnauthenticated } from "./api";

export function createQueryClient() {
  const queryClient: QueryClient = new QueryClient({
    defaultOptions: {
      // An API refusal is an answer, not a glitch: show it instead of asking again.
      queries: { retry: false, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
    // A 401 on any call means the session ended: ask `me` again, so the signed-in guard sends the user to sign in
    // (and back) instead of the screen showing the refusal.
    queryCache: new QueryCache({ onError: (error, query) => onUnauthenticated(error, query.queryKey) }),
    mutationCache: new MutationCache({ onError: (error) => onUnauthenticated(error) }),
  });

  function onUnauthenticated(error: unknown, queryKey?: readonly unknown[]) {
    if (isUnauthenticated(error) && (queryKey === undefined || hashKey(queryKey) !== hashKey(meQueryKey))) {
      void queryClient.invalidateQueries({ queryKey: meQueryKey });
    }
  }

  return queryClient;
}
