import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@msp/ui/components/card";
import type { ReactNode } from "react";

/** A signed-out page's card: its title (the page's h1), an optional description, then the content. */
export function PageCard({
  title,
  description,
  children,
}: Readonly<{ title: string; description?: ReactNode; children: ReactNode }>) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h1 className="text-xl font-semibold">{title}</h1>
        </CardTitle>
        {description ? <CardDescription>{description}</CardDescription> : null}
      </CardHeader>
      <CardContent className="grid gap-4">{children}</CardContent>
    </Card>
  );
}
