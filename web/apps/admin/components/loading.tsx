/** A screen still loading its data, announced politely to screen readers. */
export function Loading() {
  return (
    <p role="status" className="text-muted-foreground">
      Loading…
    </p>
  );
}
