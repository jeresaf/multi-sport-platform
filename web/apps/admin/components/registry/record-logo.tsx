import { cn } from "@msp/ui/lib/utils";
import { initials } from "@/components/people/person-photo";

/**
 * An organisation's or a team's logo (a signed URL from the admin read, S07 T4b), or its initials in a square when it
 * has none. A logo keeps its shape and transparency (`object-contain`), so it isn't cropped to a circle as a photo is.
 */
export function RecordLogo({ name, url }: Readonly<{ name: string; url: string | null | undefined }>) {
  const box = "size-20 text-xl";
  if (url) {
    // A signed URL on the API's host, changing as it expires: not something Next's image optimiser can cache.
    // eslint-disable-next-line @next/next/no-img-element
    return <img src={url} alt={`Logo of ${name}`} className={cn(box, "shrink-0 rounded-md border object-contain")} />;
  }
  return (
    <span
      role="img"
      aria-label={`No logo for ${name}`}
      className={cn(box, "grid shrink-0 place-items-center rounded-md bg-muted font-semibold text-muted-foreground")}
    >
      <span aria-hidden="true">{initials(name)}</span>
    </span>
  );
}
