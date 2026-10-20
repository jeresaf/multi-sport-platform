"use client";

// Reads the `token` from a password-reset or invitation link once, then removes it from the address bar, so it isn't
// kept in history, shown on screen or sent onwards (#44 M3). The page also sends `Referrer-Policy: no-referrer`
// (next.config.ts).
import { useEffect, useRef, useState } from "react";

export type LinkToken = { ready: false } | { ready: true; token: string | null };

function takeToken(): string | null {
  const url = new URL(window.location.href);
  const token = url.searchParams.get("token");
  if (token !== null) {
    url.searchParams.delete("token");
    window.history.replaceState(window.history.state, "", url.pathname + url.search + url.hash);
  }

  return token ? token : null;
}

export function useLinkToken(): LinkToken {
  // A ref keeps the token when React runs the effect twice (Strict Mode): the second run finds the URL already clean.
  const taken = useRef<string | null | undefined>(undefined);
  const [state, setState] = useState<LinkToken>({ ready: false });

  useEffect(() => {
    if (taken.current === undefined) {
      taken.current = takeToken();
    }
    setState({ ready: true, token: taken.current });
  }, []);

  return state;
}
