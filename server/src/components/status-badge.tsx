import { Badge } from "@/components/ui/badge";

/** One place that decides how a licence's state looks, so every table agrees. */
export function StatusBadge({ status, expired }: { status: string; expired?: boolean }) {
  if (status === "revoked")
    return (
      <Badge variant="destructive">
        <Dot /> Revoked
      </Badge>
    );
  if (expired)
    return (
      <Badge variant="warning">
        <Dot /> Expired
      </Badge>
    );
  return (
    <Badge variant="success">
      <Dot /> Active
    </Badge>
  );
}

export function EditionBadge({ edition }: { edition: string }) {
  return edition === "full" ? <Badge variant="brand">Full</Badge> : <Badge variant="secondary">Demo</Badge>;
}

function Dot() {
  return <span className="h-1.5 w-1.5 rounded-full bg-current" aria-hidden />;
}
