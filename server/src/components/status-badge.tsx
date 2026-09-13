import { Badge } from "@/components/ui/badge";

/** One place that decides how a licence's state looks, so every table agrees. */
export function StatusBadge({ status, expired }: { status: string; expired?: boolean }) {
  if (status === "revoked") return <Badge variant="destructive">Revoked</Badge>;
  if (expired) return <Badge variant="warning">Expired</Badge>;
  return <Badge variant="success">Active</Badge>;
}

export function EditionBadge({ edition }: { edition: string }) {
  return edition === "full" ? <Badge variant="brand">Full</Badge> : <Badge variant="secondary">Demo</Badge>;
}
