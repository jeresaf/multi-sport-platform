// Lint config for this package itself: the shared rules, except the colour-rule test, whose samples are colour
// literals on purpose.
import base from "./eslint.config.js";

export default [
  ...base,
  { files: ["test/colour-rules.test.js"], rules: { "no-restricted-syntax": "off" } },
];
