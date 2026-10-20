// Where to go after signing in. Only a path on this site is accepted, so a crafted sign-in link can't send someone to
// another site (open redirect).
const fallback = "/";

export function safeReturnTo(value: string | null | undefined): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) {
    return fallback;
  }

  // Resolve against a placeholder origin: anything that leaves it (e.g. "/\t/evil.example") is refused.
  const url = new URL(value, "http://admin.invalid");
  if (url.origin !== "http://admin.invalid" || url.pathname === "/sign-in") {
    return fallback;
  }

  return url.pathname + url.search + url.hash;
}

export function signInPath(returnTo?: string): string {
  const target = safeReturnTo(returnTo);
  return target === fallback ? "/sign-in" : `/sign-in?returnTo=${encodeURIComponent(target)}`;
}
