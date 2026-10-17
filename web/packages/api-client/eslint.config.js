import base from "@msp/config/eslint";

// src/generated is written by openapi-typescript (pnpm generate); it is checked for staleness, not linted.
export default [{ ignores: ["src/generated/**"] }, ...base];
