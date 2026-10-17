// Shared ESLint flat config for every web package. Next.js apps add eslint-config-next on top.
import tseslint from "typescript-eslint";
import { arbitraryColour, colourValue } from "./colour-rules.js";

// Known false positives: strings that look like hex, e.g. "#add" or "Fixture #101" — reword, or disable the line
// with a reason. Known gap: named colours in inline style objects ({ color: "red" }).
const colourMessage = "No colour literals in components; use design tokens from @msp/tokens.";
const asSelector = (pattern) => `/${pattern.source}/`;

export const base = tseslint.config(
  { ignores: ["**/.next/**", "**/node_modules/**", "**/next-env.d.ts", "**/playwright-report/**", "**/test-results/**"] },
  ...tseslint.configs.recommended,
  {
    files: ["**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}"],
    rules: {
      "no-restricted-syntax": [
        "error",
        ...[colourValue, arbitraryColour].flatMap((pattern) => [
          { selector: `Literal[value=${asSelector(pattern)}]`, message: colourMessage },
          { selector: `TemplateElement[value.raw=${asSelector(pattern)}]`, message: colourMessage },
        ]),
      ],
    },
  },
);

export default base;
