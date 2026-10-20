import { NotFoundView } from "@/components/status-page";

/** Any address the app has no page for (signed in or not). */
export default function NotFound() {
  return (
    <main className="px-4">
      <NotFoundView />
    </main>
  );
}
