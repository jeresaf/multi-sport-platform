import { cn } from "@msp/ui/lib/utils";

/** "Brenda Akello" → "BA": the first letters of the first and last words, for a person without a photo. */
export function initials(displayName: string): string {
  const words = displayName.trim().split(/\s+/).filter(Boolean);
  const letters = words.length > 1 ? [words[0]!, words.at(-1)!] : words;
  return letters.map((word) => Array.from(word)[0]?.toUpperCase() ?? "").join("");
}

/**
 * A person's photo (a signed URL from the person read, S06 D17), or their initials in a circle when they have none.
 * The image is the person's likeness, so its text alternative names them.
 */
export function PersonPhoto({ displayName, url }: Readonly<{ displayName: string; url: string | null | undefined }>) {
  const box = "size-24 text-2xl";
  if (url) {
    // A signed URL on the API's host, changing as it expires: not something Next's image optimiser can cache.
    // eslint-disable-next-line @next/next/no-img-element
    return <img src={url} alt={`Photo of ${displayName}`} className={cn(box, "shrink-0 rounded-full object-cover")} />;
  }
  return (
    <span
      role="img"
      aria-label={`No photo of ${displayName}`}
      className={cn(box, "grid shrink-0 place-items-center rounded-full bg-muted font-semibold text-muted-foreground")}
    >
      <span aria-hidden="true">{initials(displayName)}</span>
    </span>
  );
}
