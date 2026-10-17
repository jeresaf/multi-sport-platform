// Fails when a CSS file outside @msp/tokens defines a colour value (hex or a colour function).
// Usage: node check-css-colours.mjs <dir>... (run by each app's lint script). Colour values belong in tokens.css.
import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";

const colourValue = /#[0-9a-fA-F]{3,8}\b|\b(rgba?|hsla?|oklch|oklab|lab|lch)\(/;
const skip = new Set(["node_modules", ".next", ".turbo"]);

function* cssFiles(dir) {
  for (const name of readdirSync(dir)) {
    if (skip.has(name)) continue;
    const full = path.join(dir, name);
    if (statSync(full).isDirectory()) yield* cssFiles(full);
    else if (name.endsWith(".css")) yield full;
  }
}

let failures = 0;
for (const dir of process.argv.slice(2)) {
  for (const file of cssFiles(dir)) {
    readFileSync(file, "utf8")
      .split("\n")
      .forEach((line, i) => {
        if (colourValue.test(line)) {
          console.error(`${file}:${i + 1}: colour value outside @msp/tokens: ${line.trim()}`);
          failures++;
        }
      });
  }
}
process.exit(failures > 0 ? 1 : 0);
