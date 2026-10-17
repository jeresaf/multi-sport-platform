#!/usr/bin/env node
// PreToolUse guard for Edit/Write. Blocks (exit 2) edits that break project rules:
//  1. Anything under docs/reference/ (frozen source material).
//  2. EF Core migration files that already exist on origin/main (add a new migration instead).
//  3. Accepted ADRs in docs/decisions/, except edits that change only the status line
//     (to "Superseded by ADR-XXXX", or to a plain "Accepted" when the owner confirms).
// Cross-platform (Node only, no jq). On unexpected internal errors it allows the edit (exit 0) and logs to stderr.

import { readFileSync, existsSync } from "node:fs";
import { execFileSync } from "node:child_process";
import path from "node:path";

function readStdin() {
  try {
    return readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

function block(message) {
  process.stderr.write(`Blocked by .claude/hooks/guard-paths.mjs: ${message}\n`);
  process.exit(2);
}

function toPosix(p) {
  return p.replace(/\\/g, "/");
}

function relativeToProject(filePath, projectDir) {
  const abs = path.resolve(projectDir, filePath);
  let rel = toPosix(path.relative(projectDir, abs));
  if (process.platform === "win32") rel = rel.toLowerCase();
  return rel;
}

function existsOnOriginMain(projectDir, rel) {
  try {
    execFileSync("git", ["cat-file", "-e", `origin/main:${rel}`], {
      cwd: projectDir,
      stdio: "ignore",
      timeout: 5000,
    });
    return true;
  } catch {
    return false; // not on main, no origin/main yet, or git unavailable
  }
}

const STATUS_LINE = /^\*\*Status:\*\*/;
const ACCEPTED = /^\*\*Status:\*\*\s*Accepted/m;
// Allowed replacement status lines: confirming an acceptance (e.g. dropping "(owner to confirm …)")
// or superseding by a newer ADR.
const SUPERSEDED = /^\*\*Status:\*\*\s*(Accepted\b|Superseded by ADR-\d{4})/;

function onlyStatusLineChanged(oldText, newText) {
  const a = oldText.split(/\r?\n/);
  const b = newText.split(/\r?\n/);
  if (a.length !== b.length) return false;
  let changed = 0;
  for (let i = 0; i < a.length; i++) {
    if (a[i] === b[i]) continue;
    if (!STATUS_LINE.test(a[i]) || !SUPERSEDED.test(b[i])) return false;
    changed++;
  }
  return changed <= 1;
}

function editTouchesOnlyStatus(oldString, newString) {
  const o = (oldString ?? "").trim();
  const n = (newString ?? "").trim();
  return STATUS_LINE.test(o) && !o.includes("\n") && SUPERSEDED.test(n) && !n.includes("\n");
}

try {
  const raw = readStdin();
  if (!raw) process.exit(0);
  const input = JSON.parse(raw);
  const toolInput = input.tool_input ?? {};
  const filePath = toolInput.file_path;
  if (!filePath) process.exit(0);

  const projectDir = process.env.CLAUDE_PROJECT_DIR || input.cwd || process.cwd();
  const rel = relativeToProject(filePath, projectDir);
  if (rel.startsWith("..")) process.exit(0); // outside the project: not our concern

  // 1. Frozen reference material
  if (rel.startsWith("docs/reference/")) {
    block("docs/reference/ is frozen background material. Record disagreements in docs/build/open-questions.md or propose an ADR instead.");
  }

  // 2. Migrations already merged to main
  const isMigration =
    /(^|\/)migrations\/[^/]+\.cs$/i.test(rel) && !/modelsnapshot\.cs$/i.test(rel);
  if (isMigration && existsOnOriginMain(projectDir, toPosix(path.relative(projectDir, path.resolve(projectDir, filePath))))) {
    block(`${rel} is already on main. Never edit a merged migration — add a new migration.`);
  }

  // 3. Accepted ADRs
  if (/^docs\/decisions\/\d{4}-.+\.md$/i.test(rel)) {
    const abs = path.resolve(projectDir, filePath);
    if (existsSync(abs)) {
      const current = readFileSync(abs, "utf8");
      if (ACCEPTED.test(current)) {
        let allowed = false;
        if (typeof toolInput.content === "string") {
          allowed = onlyStatusLineChanged(current, toolInput.content);
        } else if (Array.isArray(toolInput.edits)) {
          allowed = toolInput.edits.every((e) => editTouchesOnlyStatus(e.old_string, e.new_string));
        } else {
          allowed = editTouchesOnlyStatus(toolInput.old_string, toolInput.new_string);
        }
        if (!allowed) {
          block(`${rel} is an Accepted ADR. Write a new ADR (/new-adr) and only change this file's status line to "**Status:** Superseded by ADR-XXXX".`);
        }
      }
    }
  }

  process.exit(0);
} catch (err) {
  process.stderr.write(`guard-paths.mjs internal error (edit allowed): ${err?.message ?? err}\n`);
  process.exit(0);
}
