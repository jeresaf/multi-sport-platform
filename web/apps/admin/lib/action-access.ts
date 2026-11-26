// Whether a screen that needs several actions is open to the user, as Edit details' access (S06-T7c-2b) says it.

/**
 * `allowed` when every action is held; otherwise, when each is held or only unavailable now (a read-only tenant, say),
 * the reason to show next to a disabled control; with neither, the control isn't offered.
 */
export function actionAccess(
  actions: readonly string[],
  can: (action: string) => boolean,
  unavailable: (action: string) => string | null,
): { allowed: boolean; reason: string | null } {
  if (actions.every((action) => can(action))) {
    return { allowed: true, reason: null };
  }
  const offered = actions.every((action) => can(action) || unavailable(action) !== null);
  const reason = offered ? actions.map((action) => unavailable(action)).find((item) => item !== null) : null;
  return { allowed: false, reason: reason ?? null };
}
