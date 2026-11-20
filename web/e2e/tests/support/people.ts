// People for the admin tests (S06), created through the Registry's API as a member who may (an owner): the duplicate
// check, then create with its token, then profiles, consent and a photo. Names are the pilot customer's invented
// people (pilot-customer.md); the photo is a generated placeholder, not a photo of anyone.
import { expect, type APIRequestContext } from "@playwright/test";
import { deflateSync } from "node:zlib";

type Member = { tenantId: string };
type Person = { id: string; version: number; displayName: string };

const headers = (member: Member) => ({ "X-CSRF": "1", "X-Tenant-Id": member.tenantId });

export type NewPerson = {
  firstName: string;
  lastName: string;
  middleName?: string;
  otherNames?: string;
  /** Defaults to the first and last names. */
  displayName?: string;
  gender?: "FEMALE" | "MALE";
  dateOfBirth?: string;
  nationality?: string;
  externalIdentifiers?: { authority: string; value: string }[];
};

/** Checks for duplicates, then creates the person with the check's token (a reason only if the check found someone). */
export async function createPerson(request: APIRequestContext, member: Member, person: NewPerson): Promise<Person> {
  // The check takes only what it compares (no gender or display name); the API refuses a member it doesn't know.
  const { firstName, middleName, otherNames, lastName, dateOfBirth, nationality, externalIdentifiers } = person;
  const compared = { firstName, middleName, otherNames, lastName, dateOfBirth, nationality, externalIdentifiers };
  const checked = await request.post("/api/admin/v1/people/duplicate-check", { data: compared, headers: headers(member) });
  expect(checked.status(), `duplicate check: ${await checked.text()}`).toBe(200);
  const { checkToken, candidates } = (await checked.json()) as { checkToken: string; candidates: unknown[] };

  const created = await request.post("/api/admin/v1/people", {
    data: { ...person, checkToken, overrideReason: candidates.length > 0 ? "A different person, checked by the test" : null },
    headers: headers(member),
  });
  expect(created.status(), `create person: ${await created.text()}`).toBe(201);
  return (await created.json()) as Person;
}

/** Posts a change to a person (`…/people/{id}/<path>`) with the person's version; answers the person after it. */
export async function changePerson(
  request: APIRequestContext,
  member: Member,
  person: Person,
  path: string,
  data: Record<string, unknown> = {},
): Promise<Person> {
  const changed = await request.post(`/api/admin/v1/people/${person.id}/${path}`, {
    data: { ...data, version: person.version },
    headers: headers(member),
  });
  expect(changed.status(), `${path}: ${await changed.text()}`).toBeLessThan(300);
  return (await changed.json()) as Person;
}

/** Sets the person's photo: a generated placeholder (a plain disc on a square), never a photo of anyone. */
export async function setPhoto(request: APIRequestContext, member: Member, person: Person): Promise<Person> {
  const set = await request.put(`/api/admin/v1/people/${person.id}/photo`, {
    multipart: { file: { name: "photo.png", mimeType: "image/png", buffer: placeholderPng(240) } },
    headers: { ...headers(member), "If-Match": `"${person.version}"` },
  });
  expect(set.status(), `photo: ${await set.text()}`).toBe(200);
  return (await set.json()) as Person;
}

/** Enables the tenant's first published variant (as the owner) and returns its code and its definition's codes. */
export async function enableFirstSport(request: APIRequestContext, member: Member) {
  const sports = await request.get("/api/admin/v1/tenant/sports", { headers: headers(member) });
  expect(sports.status()).toBe(200);
  const { variants } = (await sports.json()) as { variants: { variant: string; enabled: boolean }[] };
  const variant = variants[0]!.variant;
  if (!variants[0]!.enabled) {
    const enabled = await request.post(`/api/admin/v1/tenant/sports/${variant}/enable`, { headers: headers(member) });
    expect(enabled.status(), "enable sport").toBeLessThan(300);
  }
  const read = await request.get(`/api/admin/v1/sports/${variant}`, { headers: headers(member) });
  expect(read.status()).toBe(200);
  const definition = (await read.json()) as {
    positions: { code: string }[];
    staffRoles: { code: string }[];
    officialRoles: { code: string }[];
  };
  return {
    variant,
    positions: definition.positions.map((position) => position.code),
    staffRole: definition.staffRoles[0]!.code,
    officialRole: definition.officialRoles[0]!.code,
  };
}

/** A PNG of `size` × `size`: a disc in one colour on another, written by hand (no image library). */
function placeholderPng(size: number): Buffer {
  const rows: Buffer[] = [];
  const centre = size / 2;
  for (let y = 0; y < size; y++) {
    const row = Buffer.alloc(1 + size * 3);
    for (let x = 0; x < size; x++) {
      const inside = (x - centre) ** 2 + (y - centre) ** 2 < (size * 0.35) ** 2;
      row.set(inside ? [64, 120, 170] : [214, 226, 236], 1 + x * 3);
    }
    rows.push(row);
  }
  const chunk = (type: string, data: Buffer) => {
    const length = Buffer.alloc(4);
    length.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([length, body, crc]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header.set([8, 2, 0, 0, 0], 8);
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", header),
    chunk("IDAT", deflateSync(Buffer.concat(rows))),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

function crc32(data: Buffer): number {
  let crc = 0xffffffff;
  for (const byte of data) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) {
      crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1;
    }
  }
  return (crc ^ 0xffffffff) >>> 0;
}
