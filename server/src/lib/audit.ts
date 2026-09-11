import { getDb } from "./mongo";
import type { AuditDoc } from "./models";

export async function audit(
  adminEmail: string,
  action: string,
  target?: string,
  detail?: Record<string, unknown>,
): Promise<void> {
  const db = await getDb();
  const doc: AuditDoc = { adminEmail, action, target, detail, createdUtc: new Date() };
  await db.collection<AuditDoc>("auditLog").insertOne(doc);
}
