import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { ESLint } from "eslint";
import { describe, expect, it } from "vitest";
import { apps } from "../eslint.config.js";
import { sportCodes } from "../sport-rules.js";

const eslint = new ESLint({ overrideConfigFile: true, overrideConfig: apps });

async function ruleMessages(code, filePath = "app/page.tsx") {
  const [result] = await eslint.lintText(code, { filePath });
  return result.messages.filter((m) => m.ruleId === "no-restricted-syntax").map((m) => m.message);
}

const sportLiteral = (messages) => messages.filter((message) => message.includes("AR-11")).length;

describe("sport codes", () => {
  it("are read from the reference definitions and README.md's table", () => {
    const codes = sportCodes();
    expect(codes).toContain("FOOTBALL_ASSOCIATION");
    expect(codes).toContain("FOOTBALL");
    expect(codes).toEqual([...codes].sort());
  });

  it("take each file's variant and sport lines (with a comment) and the table's variants", () => {
    const folder = mkdtempSync(join(tmpdir(), "sports-"));
    try {
      writeFileSync(join(folder, "a.yaml"), "schema_version: 1\nvariant: ALPHA_ONE   # never reused\nsport: ALPHA\nlabel: x\n");
      writeFileSync(join(folder, "README.md"), "| Alpha | `ALPHA_TWO` | a.yaml |\n| text | not a code |\n");
      expect(sportCodes(folder)).toEqual(["ALPHA", "ALPHA_ONE", "ALPHA_TWO"]);
    } finally {
      rmSync(folder, { recursive: true, force: true });
    }
  });
});

describe("ESLint sport-literal rule (S05 D6)", () => {
  it.each([['export const a = "FOOTBALL";'], ["export const a = `FOOTBALL_ASSOCIATION`;"], ['if (v === "FOOTBALL") {}'], ["export const byCode = { FOOTBALL: 1 };"]])(
    "rejects %s",
    async (code) => {
      expect(sportLiteral(await ruleMessages(code))).toBe(1);
    },
  );

  it.each([['export const a = "FOOTBALL_PITCH";'], ['export const a = "football";'], ['export const a = "Football";'], ["export const a = { football: 1 }; a.FOOTBALL_PITCH = 2;"]])(
    "accepts %s",
    async (code) => {
      expect(sportLiteral(await ruleMessages(code))).toBe(0);
    },
  );

  it.each([["components/renderers/additive.tsx"], ["app/page.test.tsx"], ["test/definitions.ts"]])(
    "exempts %s",
    async (filePath) => {
      expect(await ruleMessages('export const a = "FOOTBALL";', filePath)).toEqual([]);
    },
  );

  it("keeps the colour rule in the apps' config", async () => {
    expect(await ruleMessages('export const a = "bg-[#ff0000]";')).toHaveLength(1);
    expect(await ruleMessages('export const a = "bg-[#ff0000]";', "app/page.test.tsx")).toHaveLength(1);
  });
});
