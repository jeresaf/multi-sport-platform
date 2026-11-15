// AR-11 and S05 D6: generic web code reads sports as data. The sport and variant codes come from the reference
// definitions (docs/domain/sports), read the same way as the backend's SportLiteralTests: each file's `variant:` and
// `sport:` lines, plus the variants in README.md's table.
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const codeLine = /^(variant|sport):\s*([A-Z][A-Z0-9_]*)\s*(#.*)?$/;
const tableVariant = /^\|[^|]+\|\s*`([A-Z][A-Z0-9_]*)`/;

/** The repository's docs/domain/sports folder (this package lives in web/packages/config). */
export const sportsFolder = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "docs", "domain", "sports");

/** Every sport and variant code in the reference definitions, sorted. */
export function sportCodes(folder = sportsFolder) {
  const codes = new Set();
  for (const file of readdirSync(folder).filter((name) => name.endsWith(".yaml"))) {
    for (const line of readFileSync(join(folder, file), "utf8").split(/\r?\n/)) {
      const match = codeLine.exec(line);
      if (match) codes.add(match[2]);
    }
  }
  for (const line of readFileSync(join(folder, "README.md"), "utf8").split(/\r?\n/)) {
    const match = tableVariant.exec(line);
    if (match) codes.add(match[1]);
  }
  return [...codes].sort();
}

export const sportLiteralMessage =
  "No sport or variant codes in generic code (AR-11): read sports as data; sport words live in definitions and renderers.";

/**
 * `no-restricted-syntax` selectors for a whole string (or template) literal equal to one of the codes, and for an
 * object key naming one (`{ FOOTBALL: … }`, a map by sport).
 */
export function sportLiteralSelectors(codes = sportCodes()) {
  if (codes.length === 0) return [];
  const pattern = `/^(${codes.join("|")})$/`;
  return [
    { selector: `Literal[value=${pattern}]`, message: sportLiteralMessage },
    { selector: `TemplateElement[value.raw=${pattern}]`, message: sportLiteralMessage },
    { selector: `Property > Identifier.key[name=${pattern}]`, message: sportLiteralMessage },
  ];
}
