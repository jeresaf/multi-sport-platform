import { Alert, AlertDescription, AlertTitle } from "@msp/ui/components/alert";
import { Button } from "@msp/ui/components/button";
import { RefreshCw } from "lucide-react";

/** A change refused because the record changed since it was read (`*_CONCURRENCY_CONFLICT`, admin-app.md). */
export function ConflictAlert({ onReload }: Readonly<{ onReload: () => void }>) {
  return (
    <Alert variant="destructive">
      <RefreshCw aria-hidden="true" />
      <AlertTitle>This record changed since you opened it</AlertTitle>
      <AlertDescription>
        <p>Someone else saved a change first. Reload to see it, then make your change again.</p>
        <Button variant="outline" onClick={onReload} className="mt-2">
          Reload
        </Button>
      </AlertDescription>
    </Alert>
  );
}
