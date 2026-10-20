/** A full page load of `path` (on this site): everything held in memory is dropped. Its own module so tests replace it. */
export function leaveTo(path: string): void {
  window.location.assign(path);
}
