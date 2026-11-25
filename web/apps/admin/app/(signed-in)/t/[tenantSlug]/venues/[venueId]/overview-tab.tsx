"use client";

import { ExternalLink } from "lucide-react";
import { Facts, ViewerSection, orNone } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { useCountries } from "@/lib/reference-data";
import { useSportVariants } from "../../sports/sports-data";
import type { Venue } from "../venues-data";

const notRecorded = "Not recorded";

/** A map of the point (OpenStreetMap: no key, no tracking script; the link opens in a new tab). */
export function mapUrl(latitude: number, longitude: number): string {
  return `https://www.openstreetmap.org/?mlat=${latitude}&mlon=${longitude}#map=17/${latitude}/${longitude}`;
}

/**
 * Overview (S07 D23): where the venue is (address, coordinates with a map link, time zone), what it is (type,
 * capacity, surfaces) and the sports it supports, named from their definitions (AR-29).
 */
export function OverviewTab({ venue }: Readonly<{ venue: Venue }>) {
  const countries = useCountries();
  const sports = useSportVariants();
  const country = countries.data?.items.find((item) => item.code === venue.country)?.name ?? venue.country;
  const sportName = (code: string) => sports.data?.items.find((item) => item.variant === code)?.name ?? humanizeCode(code);
  const location =
    venue.latitude !== null && venue.longitude !== null ? (
      <span className="flex flex-wrap items-center gap-x-3">
        <span>
          {venue.latitude}, {venue.longitude}
        </span>
        <a
          href={mapUrl(venue.latitude, venue.longitude)}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex items-center gap-1 text-primary underline-offset-4 hover:underline"
        >
          Open in a map
          <ExternalLink aria-hidden="true" className="size-4" />
          <span className="sr-only">(opens in a new tab)</span>
        </a>
      </span>
    ) : (
      notRecorded
    );

  return (
    <>
      <ViewerSection title="Venue">
        <Facts
          items={[
            ["Name", venue.name],
            ["Short name", venue.shortName ?? "None"],
            ["Type", humanizeCode(venue.type)],
            ["Capacity", venue.capacity === null ? notRecorded : `${venue.capacity.toLocaleString("en-GB")} seats`],
            ["Surfaces", orNone(venue.surfaces.map(humanizeCode))],
            ["Sports", orNone(venue.supportedVariants.map(sportName))],
          ]}
        />
      </ViewerSection>
      <ViewerSection title="Location">
        <Facts
          items={[
            ["Address", venue.addressLine ?? notRecorded],
            ["City", venue.city],
            ["Region", venue.region ?? notRecorded],
            ["Country", country],
            ["Coordinates", location],
            ["Time zone", venue.timeZone],
          ]}
        />
      </ViewerSection>
    </>
  );
}
