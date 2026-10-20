/** A code as people read it: "TENANT_OWNER" → "Tenant owner". */
export function humanizeCode(code: string): string {
  const words = code.toLowerCase().split("_").filter(Boolean).join(" ");
  return words.charAt(0).toUpperCase() + words.slice(1);
}
