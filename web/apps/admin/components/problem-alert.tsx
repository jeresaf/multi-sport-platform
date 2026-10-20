import { describeProblem, genericErrorMessage } from "@msp/api-client/errors";
import { Alert, AlertDescription, AlertTitle } from "@msp/ui/components/alert";
import { CircleAlert } from "lucide-react";
import type { ReactNode } from "react";
import { ApiProblem } from "@/lib/api";

/** The message for a failed request (admin-app.md): from the error code, or the server's message and the reference. */
export function ProblemAlert({
  error,
  title = "That didn't work",
  children,
}: Readonly<{ error: unknown; title?: string; children?: ReactNode }>) {
  const { message, correlationId } =
    error instanceof ApiProblem ? describeProblem(error.problem) : { message: genericErrorMessage, correlationId: undefined };

  return (
    <Alert variant="destructive">
      <CircleAlert aria-hidden="true" />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <p>{message}</p>
        {correlationId ? <p>Reference: {correlationId}</p> : null}
        {children}
      </AlertDescription>
    </Alert>
  );
}
