#!/usr/bin/env node
// Fails when a CSS file outside @msp/tokens defines a colour value. Usage: msp-check-css-colours <dir>...
// Run by each app's lint script; the rules are in colour-rules.js.
import { readdirSync, readFileSync, statSync } from "node:fs";
import path from "node:path";
import { findCssColourIssues } from "./colour-rules.js";

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
    for (const issue of findCssColourIssues(readFileSync(file, "utf8"))) {
      console.error(`${file}:${issue}`);
      failures++;
    }
  }
}
process.exit(failures > 0 ? 1 : 0);
