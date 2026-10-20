import type { Metadata } from "next";
import { AcceptInvitation } from "./accept-invitation";

export const metadata: Metadata = { title: "Accept invitation · MSP Admin" };

export default function Page() {
  return <AcceptInvitation />;
}
