// Colour rules shared by the ESLint config and the CSS checker. Colour values live only in @msp/tokens
// (docs/product/ux/design-system.md).

/** Hex values and colour functions: "#1d4ed8", "rgb(0 0 0)", "oklch(…)". */
export const colourValue = /#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?|oklch|oklab|lab|lch|hwb|color)\(/;

/** Tailwind arbitrary colours: "bg-[red]", "text-[color:red]", "[color:red]" (var(...) is allowed). */
export const arbitraryColour =
  /\b(bg|text|border|fill|stroke|ring|outline|decoration|shadow|from|via|to|accent|caret|placeholder)-\[(color:)?(?!var\()[a-z]+\]|\[(color|background-color|border-color|fill|stroke):(?!var\()/;

// CSS properties whose value must come from a token (var(...)) or a keyword.
const colourProperty = /(^|[\s;{])(color|background|background-color|border(-(top|right|bottom|left))?-color|outline-color|fill|stroke|accent-color|caret-color|text-decoration-color)\s*:\s*([^;}]+)/g;
const allowedValue = /^(var\(|inherit|initial|unset|revert|transparent|currentcolor|none)/i;

const stripComments = (css) => css.replace(/\/\*[\s\S]*?\*\//g, (comment) => comment.replace(/[^\n]/g, " "));

/** Returns "line: message" problems for colour values in a CSS file outside @msp/tokens. */
export function findCssColourIssues(css) {
  const issues = [];
  stripComments(css)
    .split("\n")
    .forEach((line, i) => {
      if (colourValue.test(line) || arbitraryColour.test(line)) {
        issues.push(`${i + 1}: colour value outside @msp/tokens: ${line.trim()}`);
        return;
      }
      for (const match of line.matchAll(colourProperty)) {
        if (!allowedValue.test(match[5].trim())) {
          issues.push(`${i + 1}: colour property must use a token (var(--msp-…)): ${line.trim()}`);
        }
      }
    });
  return issues;
}
