"use client";

// A person's profiles hold the definitions' codes (S06 D12); the screen names them in the definition's words (AR-29):
// each variant's latest definition, read once per session and shared with Sports (the same query keys). Until a
// definition is read, or for a code it no longer has, the code is written as words.
import { useQueries } from "@tanstack/react-query";
import { call } from "@/lib/api";
import { codeLabel, type SportDefinition } from "@/lib/sport-definition";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useSportVariants } from "../../sports/sports-data";

export type SportLabels = {
  /** The variant's latest definition once read (its positions and roles for a form's choices). */
  definition: (variant: string) => SportDefinition | undefined;
  variant: (variant: string) => string;
  position: (variant: string, code: string) => string;
  staffRole: (variant: string, code: string) => string;
  officialRole: (variant: string, code: string) => string;
};

export function useSportLabels(variants: readonly string[]): SportLabels {
  const { client, can } = useTenantWorkspace();
  const list = useSportVariants();
  const distinct = [...new Set(variants)];
  const definitions = useQueries({
    queries: distinct.map((variant) => ({
      queryKey: ["sports", "definition", variant, "latest"],
      queryFn: () => call(client.GET("/api/admin/v1/sports/{variant}", { params: { path: { variant } } })),
      enabled: can("sport.read"),
      staleTime: Infinity,
    })),
  });

  const definition = (variant: string): SportDefinition | undefined =>
    definitions[distinct.indexOf(variant)]?.data;
  const label = (items: ReadonlyArray<{ code: string; label: string }> | undefined, code: string) =>
    items?.find((item) => item.code === code)?.label ?? codeLabel(code);

  return {
    definition,
    variant: (variant) =>
      definition(variant)?.name ?? list.data?.items.find((item) => item.variant === variant)?.name ?? codeLabel(variant),
    position: (variant, code) => label(definition(variant)?.positions, code),
    staffRole: (variant, code) => label(definition(variant)?.staffRoles, code),
    officialRole: (variant, code) => label(definition(variant)?.officialRoles, code),
  };
}
