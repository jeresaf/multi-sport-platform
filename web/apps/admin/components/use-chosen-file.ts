"use client";

import { useEffect, useRef, useState } from "react";

/**
 * The chosen file and a blob: URL to preview it (a photo, a logo). Each URL is released when another file is chosen
 * and when the dialog closes.
 */
export function useChosenFile() {
  const [chosen, setChosen] = useState<{ file: File; preview: string } | undefined>();
  const current = useRef<string | undefined>(undefined);
  useEffect(
    () => () => {
      if (current.current) {
        URL.revokeObjectURL(current.current);
      }
    },
    [],
  );
  const choose = (file: File | undefined) => {
    if (current.current) {
      URL.revokeObjectURL(current.current);
    }
    current.current = file ? URL.createObjectURL(file) : undefined;
    setChosen(file && current.current ? { file, preview: current.current } : undefined);
  };
  return { file: chosen?.file, preview: chosen?.preview, choose };
}
