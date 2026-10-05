// Create the bootstrap admin from ADMIN_EMAIL / ADMIN_PASSWORD.
//   node --env-file=.env.local scripts/seed-admin.mjs
// (Node 20.6+ supports --env-file. Otherwise export the vars first.)
//
// deploy.sh runs this on every deploy. It creates the owner when the account does not
// exist yet, and otherwise leaves the password alone: a password changed in the panel's
// Team page must survive the next update. To force the .env password back (a lost
// password), run it once with ADMIN_RESET_PASSWORD=1; that also signs out every session
// of that account.

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
const reset = process.env.ADMIN_RESET_PASSWORD === "1";
await db.collection("admins").createIndex({ email: 1 }, { unique: true });
const existing = await db.collection("admins").findOne({ email });
if (!existing) {
  await db.collection("admins").insertOne({ email, passwordHash, role: "owner", status: "active", sessionVersion: 0, createdUtc: new Date() });
  console.log(`Admin created: ${email}`);
} else if (reset) {
  await db.collection("admins").updateOne(
    { email },
    { $set: { passwordHash, role: "owner", status: "active" }, $inc: { sessionVersion: 1 } },
  );
  console.log(`Admin password reset from .env, every session of it signed out: ${email}`);
} else {
  // Keep the owner an active owner, but never overwrite a password set in the panel.
  await db.collection("admins").updateOne({ email }, { $set: { role: "owner", status: "active" } });
  console.log(`Admin ready (password unchanged): ${email}`);
}
await client.close();
