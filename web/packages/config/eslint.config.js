// Shared ESLint flat config for every web package. Next.js apps add eslint-config-next on top, and use `apps`.
import tseslint from "typescript-eslint";
import { arbitraryColour, colourValue } from "./colour-rules.js";
import { sportLiteralSelectors } from "./sport-rules.js";

// Known false positives: strings that look like hex, e.g. "#add" or "Fixture #101" — reword, or disable the line
// with a reason. Known gap: named colours in inline style objects ({ color: "red" }).
const colourMessage = "No colour literals in components; use design tokens from @msp/tokens.";
const asSelector = (pattern) => `/${pattern.source}/`;
const sourceFiles = ["**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}"];

const colourSelectors = [colourValue, arbitraryColour].flatMap((pattern) => [
  { selector: `Literal[value=${asSelector(pattern)}]`, message: colourMessage },
  { selector: `TemplateElement[value.raw=${asSelector(pattern)}]`, message: colourMessage },
]);

export const base = tseslint.config(
  { ignores: ["**/.next/**", "**/node_modules/**", "**/next-env.d.ts", "**/playwright-report/**", "**/test-results/**"] },
  ...tseslint.configs.recommended,
  { files: sourceFiles, rules: { "no-restricted-syntax": ["error", ...colourSelectors] } },
);

/**
 * The apps' config: `base` plus S05 D6's sport-literal rule. Renderers (the only generic-free sport code) and tests
 * are exempt, as in the backend's SportLiteralTests. The rule lists the colour selectors again: a later
 * `no-restricted-syntax` replaces an earlier one's options instead of adding to them.
 */
export const apps = tseslint.config(...base, {
  files: sourceFiles,
  ignores: ["**/renderers/**", "**/*.test.{ts,tsx,js,jsx}", "**/test/**"],
  rules: { "no-restricted-syntax": ["error", ...colourSelectors, ...sportLiteralSelectors()] },
});

export default base;
