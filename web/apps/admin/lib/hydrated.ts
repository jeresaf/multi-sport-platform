// Whether React has hydrated this page. A form rendered on the server submits natively until then (a GET reload that
// puts its fields, an email address included, in the URL), so submit buttons stay disabled until it is true.
import { useSyncExternalStore } from "react";

const noChanges = () => () => {};

export function useHydrated(): boolean {
  return useSyncExternalStore(
    noChanges,
    () => true,
    () => false,
  );
}
