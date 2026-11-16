// Test-only formats (S05 D17): invented codes and words, so a test passes only if the screen shows what the format
// says. One has a parameter of each format type; the other has no parameters and no description.
import type { FormatDefinition } from "@/components/formats/format-view";

export const ladder: FormatDefinition = {
  code: "LADDER_CLIMB",
  version: 3,
  name: "Ladder climb",
  description: "Participants challenge the one just above them; a win swaps their places.",
  schemaVersion: 1,
  contentHash: "sha256:7f3c1e0a9b6d4c2e8f1a5b3d7c9e0f2a4b6c8d0e1f3a5b7c9d1e3f5a7b9c1d3e",
  // 21:30 UTC on 1 Nov is 2 Nov in Kampala.
  publishedAt: "2026-11-01T21:30:00Z",
  compatibleShapes: ["HEAD_TO_HEAD", "FIELD"],
  participants: { min: 4, max: 24 },
  stages: [
    { code: "OPENING_ROUNDS", type: "LEAGUE", generator: "ROUND_ROBIN" },
    { code: "CLIMB", type: "KNOCKOUT", generator: "LADDER" },
  ],
  parameters: {
    challenges: {
      type: "NUMBER",
      label: "Challenges per week",
      description: "How many challenges each participant may make in a week.",
      min: 1,
      max: 3,
      values: [],
      default: 2,
    },
    open_ladder: { type: "FLAG", label: "Open ladder", description: null, min: null, max: null, values: [], default: false },
    starting_order: {
      type: "CHOICE",
      label: "Starting order",
      description: "How the ladder is filled before the first challenge.",
      min: null,
      max: null,
      values: ["RANKED", "RANDOM", "BY_ENTRY_TIME"],
      default: "BY_ENTRY_TIME",
    },
  },
};

export const showdown: FormatDefinition = {
  code: "SHOWDOWN",
  version: 1,
  name: "Showdown",
  description: null,
  schemaVersion: 1,
  contentHash: "sha256:0a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f9",
  publishedAt: "2026-10-20T08:00:00Z",
  compatibleShapes: ["HEAD_TO_HEAD"],
  participants: { min: 2, max: 2 },
  stages: [{ code: "FINAL", type: "SINGLE", generator: "SINGLE" }],
  parameters: {},
};
