import { Alert, AlertDescription, AlertTitle } from "@msp/ui/components/alert";
import { AlarmClock, Lock } from "lucide-react";
import { accessNotice, type CapabilityAccess } from "@/lib/access";

/**
 * The tenant shell's banner (S04 D19): every member sees it while the subscription is ending or in grace, and while the
 * workspace is read-only. Nothing while access is full.
 */
export function AccessBanner({ access }: Readonly<{ access: CapabilityAccess }>) {
  const notice = accessNotice(access);
  if (!notice) {
    return null;
  }

  const Icon = notice.tone === "readOnly" ? Lock : AlarmClock;
  // A region, not an alert: it is there when the page opens, not news to interrupt with.
  return (
    <Alert role="region" aria-label="Subscription" className="border-warning [&>svg]:text-warning">
      <Icon aria-hidden="true" />
      <AlertTitle>{notice.title}</AlertTitle>
      <AlertDescription>
        <p>{notice.message}</p>
      </AlertDescription>
    </Alert>
  );
}
