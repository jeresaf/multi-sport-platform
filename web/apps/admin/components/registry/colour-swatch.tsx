// A team's colour as a swatch with its hex beside it (S07 D23): the hex says it to a screen reader and to anyone the
// swatch fails. Only the API's form (#RRGGBB) is painted.

/** The API's colour form (`TeamDetailsRequest`). */
export const hexColour = /^#[0-9A-Fa-f]{6}$/;

export function ColourSwatch({ colour }: Readonly<{ colour: string }>) {
  return (
    <span className="flex items-center gap-2">
      {hexColour.test(colour) ? (
        <span
          aria-hidden="true"
          className="inline-block size-5 shrink-0 rounded-sm border"
          // The team's own colour is data, not a design token.
          style={{ backgroundColor: colour }}
        />
      ) : null}
      <span className="font-mono text-sm">{colour.toUpperCase()}</span>
    </span>
  );
}
