/**
 * An item page's `?version=` as a version number (S05 D17): undefined for the latest (no parameter), null for one that
 * isn't a version number, which can't exist, so the page is "not found" without asking the API.
 */
export function versionParam(value: string | null): number | undefined | null {
  if (value === null) return undefined;
  return /^[1-9][0-9]{0,8}$/.test(value) ? Number(value) : null;
}
