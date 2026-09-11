// Create/update the bootstrap admin from ADMIN_EMAIL / ADMIN_PASSWORD.
//   node --env-file=.env.local scripts/seed-admin.mjs
// (Node 20.6+ supports --env-file. Otherwise export the vars first.)

import { MongoClient } from "mongodb";
import bcrypt from "bcryptjs";

const uri = process.env.MONGODB_URI;
const dbName = process.env.MONGODB_DB || "textsoft_licenses";
const email = (process.env.ADMIN_EMAIL || "admin@textsoft.local").toLowerCase().trim();
const password = process.env.ADMIN_PASSWORD;

if (!uri) {
  console.error("MONGODB_URI not set. Run with: node --env-file=.env.local scripts/seed-admin.mjs");
  process.exit(1);
}
if (!password) {
  console.error("ADMIN_PASSWORD not set.");
  process.exit(1);
}

const client = new MongoClient(uri);
await client.connect();
const db = client.db(dbName);

const passwordHash = bcrypt.hashSync(password, 10);
await db.collection("admins").createIndex({ email: 1 }, { unique: true });
await db.collection("admins").updateOne(
  { email },
  { $set: { email, passwordHash, role: "owner" }, $setOnInsert: { createdUtc: new Date() } },
  { upsert: true },
);

console.log(`Admin ready: ${email}`);
await client.close();
