// Run the admin panel against a throwaway MongoDB.
//
// The licence server needs a database, and a developer machine often has none.
// mongodb-memory-server starts a real mongod on a temporary port and throws the
// data away on exit, so the panel can be run and tested without installing
// anything system-wide. Production points MONGODB_URI at the real server.
//
//   npm run dev:db          -- panel on http://localhost:3100 (same port as production), fresh empty database
//   npm run dev:db -- 3200  -- on another port
//
// Mail: .env.local may hold a real SMTP account. To try purchase emails without
// mailing anyone, point Settings → Email at a local SMTP sink (security "none").
//
// The admin user is seeded automatically, because a fresh database has none and
// the login page would be a dead end.

import { spawn } from "node:child_process";
import { MongoMemoryServer } from "mongodb-memory-server";
import { MongoClient } from "mongodb";
import bcrypt from "bcryptjs";

// .env.local holds the signing keys and the bootstrap admin; read it ourselves
// since we start before Next does.
import fs from "node:fs";
import path from "node:path";

const envPath = path.join(process.cwd(), ".env.local");
const fileEnv = {};
if (fs.existsSync(envPath)) {
  for (const line of fs.readFileSync(envPath, "utf8").split(/\r?\n/)) {
    const m = /^([A-Z0-9_]+)=(.*)$/.exec(line.trim());
    if (m) fileEnv[m[1]] = m[2];
  }
}

const port = process.argv[2] ?? "3100";
const dbName = fileEnv.MONGODB_DB || "textsoft_licenses";

console.log("starting a temporary MongoDB…");
const mongo = await MongoMemoryServer.create({ instance: { dbName } });
const uri = mongo.getUri();
console.log("  " + uri);

// seed the admin so there is someone to log in as
const email = fileEnv.ADMIN_EMAIL || "admin@textsoft.local";
const password = fileEnv.ADMIN_PASSWORD || "change-me-now";
const client = new MongoClient(uri);
await client.connect();
await client.db(dbName).collection("admins").insertOne({
  email: email.toLowerCase(),
  passwordHash: await bcrypt.hash(password, 10),
  role: "owner",
  createdUtc: new Date(),
});
await client.close();
console.log(`  admin seeded: ${email} / ${password}`);

const child = spawn(
  process.platform === "win32" ? "npx.cmd" : "npx",
  ["next", "dev", "-p", port],
  {
    stdio: "inherit",
    env: {
      ...process.env,
      ...fileEnv,
      MONGODB_URI: uri,
      MONGODB_DB: dbName,
      PORT: port,
      // own build directory, so a production build cannot clobber this one
      NEXT_DIST_DIR: ".next-dev",
    },
    shell: process.platform === "win32",
  },
);

async function shutdown() {
  child.kill();
  await mongo.stop();
  process.exit(0);
}
process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
child.on("exit", shutdown);
