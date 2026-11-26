"use client";

import { RecordImageControl, type RecordImageConfig } from "@/components/registry/image-control";
import { humanizeCode } from "@/lib/codes";
import { acceptsNewChildren, useOrganisationLogoApi, type Organisation } from "../organisations-data";

/**
 * The organisation's logo (S07 D15, T4b): `organisation.logo.set` adds or replaces it, `organisation.logo.remove`
 * takes it down. A dissolved or archived organisation gets no new logo; removing works in any status.
 */
export function LogoControl({ organisation }: Readonly<{ organisation: Organisation }>) {
  const api = useOrganisationLogoApi(organisation.id);
  const config: RecordImageConfig = {
    id: "organisation-logo",
    image: "logo",
    name: organisation.displayName,
    actions: { set: "organisation.logo.set", remove: "organisation.logo.remove" },
    hasImage: organisation.logo !== null,
    closedNote: acceptsNewChildren(organisation.status)
      ? null
      : `${organisation.displayName} is ${humanizeCode(organisation.status).toLowerCase()}: no new logo can be added.`,
  };
  return <RecordImageControl record={organisation} config={config} api={api} />;
}
