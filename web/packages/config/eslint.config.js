// Shared ESLint flat config for every web package. Next.js apps add eslint-config-next on top.
import tseslint from "typescript-eslint";

// Colours live only in @msp/tokens (docs/product/ux/design-system.md). Catches hex, rgb()/hsl()/oklch() literals,
// including Tailwind arbitrary values such as "bg-[#fff]".
const colourLiteral = String.raw`/#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?|oklch|oklab|lab|lch)\(/`;
const colourMessage = "No colour literals in components; use design tokens from @msp/tokens.";

export const base = tseslint.config(
  { ignores: ["**/.next/**", "**/node_modules/**", "**/next-env.d.ts", "**/playwright-report/**", "**/test-results/**"] },
  ...tseslint.configs.recommended,
  {
    files: ["**/*.{ts,tsx}"],
    rules: {
      "no-restricted-syntax": [
        "error",
        { selector: `Literal[value=${colourLiteral}]`, message: colourMessage },
        { selector: `TemplateElement[value.raw=${colourLiteral}]`, message: colourMessage },
      ],
    },
  },
);

export default base;
