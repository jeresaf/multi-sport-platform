// Reads the emails the API sends in Development (Mailpit, docker compose): https://mailpit.axllent.org/docs/api-v1/
import { expect, type APIRequestContext } from "@playwright/test";

const mailpitUrl = process.env.MAILPIT_URL ?? "http://localhost:8025";

type MessageSummary = { ID: string; To: { Address: string }[] };

// Links already handed out (per worker): a person's second email (a second invitation) is waited for, not mistaken
// for the first one, which is still the newest until the outbox sends the next.
const returned = new Set<string>();

/**
 * The first link to `path` (e.g. "/reset-password") in the newest email to `to` that this worker hasn't returned yet,
 * waiting for the outbox to send it.
 */
export async function linkFromEmail(request: APIRequestContext, to: string, path: string): Promise<string> {
  const pattern = new RegExp(`https?://[^\\s"<>]+${path.replace("/", "\\/")}\\?token=[^\\s"<>]+`);
  let link: string | undefined;

  await expect
    .poll(
      async () => {
        const search = await request.get(`${mailpitUrl}/api/v1/search`, { params: { query: `to:"${to}"` } });
        expect(search.ok()).toBe(true);
        const { messages } = (await search.json()) as { messages: MessageSummary[] };
        const newest = messages[0];
        if (!newest) {
          return undefined;
        }

        const message = await request.get(`${mailpitUrl}/api/v1/message/${newest.ID}`);
        const { Text } = (await message.json()) as { Text: string };
        const found = Text.match(pattern)?.[0];
        link = found && !returned.has(found) ? found : undefined;
        return link;
      },
      { message: `an email to ${to} with a ${path} link`, timeout: 30_000 },
    )
    .toBeDefined();

  returned.add(link!);
  return link!;
}
