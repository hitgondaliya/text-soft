import { MongoClient, Db } from "mongodb";
import { env } from "./env";

// Single shared MongoClient across hot-reloads (dev) and across requests (prod).
// Next.js re-imports modules on every edit in dev, so we stash the client on the
// global object to avoid exhausting the connection pool.

declare global {
  // eslint-disable-next-line no-var
  var __textsoftMongo: { client: MongoClient; promise: Promise<MongoClient> } | undefined;
}

function makeClient(): Promise<MongoClient> {
  if (!global.__textsoftMongo) {
    const client = new MongoClient(env.mongoUri(), {
      // Keep a small pool; this is a low-traffic license server.
      maxPoolSize: 10,
    });
    global.__textsoftMongo = { client, promise: client.connect() };
  }
  return global.__textsoftMongo.promise;
}

let indexesReady = false;

export async function getDb(): Promise<Db> {
  const client = await makeClient();
  const db = client.db(env.mongoDb());
  if (!indexesReady) {
    await ensureIndexes(db);
    indexesReady = true;
  }
  return db;
}

// Indexes are created lazily on first DB access. Idempotent — safe to call often.
async function ensureIndexes(db: Db): Promise<void> {
  await Promise.all([
    db.collection("admins").createIndex({ email: 1 }, { unique: true }),
    db.collection("licenseKeys").createIndex({ keyHash: 1 }, { unique: true }),
    db.collection("licenseKeys").createIndex({ status: 1 }),
    db.collection("licenseKeys").createIndex({ createdUtc: -1 }),
    // A purchase's keys are always read together and in order.
    db.collection("licenseKeys").createIndex({ orderId: 1, seq: 1 }),
    db.collection("licenseKeys").createIndex({ serial: 1 }),
    db.collection("orders").createIndex({ serial: 1 }, { unique: true }),
    db.collection("orders").createIndex({ createdUtc: -1 }),
    db.collection("orders").createIndex({ customerEmail: 1 }),
    db.collection("activations").createIndex({ keyId: 1, deviceFp: 1 }, { unique: true }),
    db.collection("activations").createIndex({ keyId: 1 }),
    db.collection("activations").createIndex({ deviceFp: 1, status: 1 }),
    db.collection("activations").createIndex({ "user.email": 1 }),
    db.collection("activationAttempts").createIndex({ createdUtc: -1 }),
    db.collection("activationAttempts").createIndex({ keyId: 1, createdUtc: -1 }),
    db.collection("activationAttempts").createIndex({ serial: 1 }),
    db.collection("auditLog").createIndex({ createdUtc: -1 }),
  ]);
}
