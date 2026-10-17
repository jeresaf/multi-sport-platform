// Shared ESLint flat config for every web package. Next.js apps add eslint-config-next on top.
import tseslint from "typescript-eslint";

// Colours live only in @msp/tokens (docs/product/ux/design-system.md). Catches hex and rgb()/hsl()/oklch() literals,
// and Tailwind arbitrary colours such as "bg-[#fff]" or "text-[red]". Known false positive: strings that look like
// hex, e.g. "#add" — rename or disable the line with a reason.
const colourFunction = String.raw`/#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?|oklch|oklab|lab|lch)\(/`;
const arbitraryColour = String.raw`/\b(bg|text|border|fill|stroke|ring|outline|decoration|shadow|from|via|to|accent|caret|placeholder)-\[(?!var\()[a-z]+\]/`;
const colourMessage = "No colour literals in components; use design tokens from @msp/tokens.";

export const base = tseslint.config(
  { ignores: ["**/.next/**", "**/node_modules/**", "**/next-env.d.ts", "**/playwright-report/**", "**/test-results/**"] },
  ...tseslint.configs.recommended,
  {
    files: ["**/*.{js,mjs,ts,tsx}"],
    rules: {
      "no-restricted-syntax": [
        "error",
        ...[colourFunction, arbitraryColour].flatMap((pattern) => [
          { selector: `Literal[value=${pattern}]`, message: colourMessage },
          { selector: `TemplateElement[value.raw=${pattern}]`, message: colourMessage },
        ]),
      ],
    },
  },
);

export default base;
