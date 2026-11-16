// Lint config for this package itself: the shared rules, except the rule tests, whose samples are colour (and sport)
// literals on purpose.
import base from "./eslint.config.js";

export default [
  ...base,
  { files: ["test/colour-rules.test.js", "test/sport-rules.test.js"], rules: { "no-restricted-syntax": "off" } },
];
