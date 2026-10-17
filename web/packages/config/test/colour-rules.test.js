import { ESLint } from "eslint";
import { describe, expect, it } from "vitest";
import { findCssColourIssues } from "../colour-rules.js";
import base from "../eslint.config.js";

const eslint = new ESLint({ overrideConfigFile: true, overrideConfig: base });

async function colourErrors(code) {
  const [result] = await eslint.lintText(code, { filePath: "component.tsx" });
  return result.messages.filter((m) => m.ruleId === "no-restricted-syntax").length;
}

describe("ESLint colour-literal rule", () => {
  it.each([
    ['export const a = "bg-[#ff0000]";'],
    ['export const a = "text-[red]";'],
    ['export const a = "text-[color:red]";'],
    ['export const a = "[color:red]";'],
    ["export const a = `color: rgb(1 2 3)`;"],
    ['export const a = "oklch(0.5 0.1 200)";'],
  ])("rejects %s", async (code) => {
    expect(await colourErrors(code)).toBeGreaterThan(0);
  });

  it.each([
    ['export const a = "bg-primary text-primary-contrast";'],
    ['export const a = "bg-[var(--msp-color-live)]";'],
    ['export const a = "text-[length:14px] p-4";'],
  ])("accepts %s", async (code) => {
    expect(await colourErrors(code)).toBe(0);
  });
});

describe("CSS colour checker", () => {
  it.each([
    ["body { color: #123456; }"],
    ["body { color: red; }"],
    ["body { background: rgb(0 0 0); }"],
    [".a { @apply bg-[red]; }"],
    [".a { border-top-color: tomato; }"],
  ])("rejects %s", (css) => {
    expect(findCssColourIssues(css)).not.toHaveLength(0);
  });

  it.each([
    ['@import "tailwindcss";'],
    ["body { color: var(--msp-color-text); background: transparent; }"],
    [".a { @apply bg-background text-text; }"],
    ["/* rgb(0 0 0) is fine in a comment */"],
    ["/*\n color: red;\n*/"],
  ])("accepts %s", (css) => {
    expect(findCssColourIssues(css)).toEqual([]);
  });
});
