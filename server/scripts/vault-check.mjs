// Vault canary: do this .env's secrets still open what is already in the database?
//
//   node --env-file=.env scripts/vault-check.mjs        (deploy.sh runs it before restarting)
//
// Licence keys (licenseKeys.keyEnc) and the SMTP password saved in the panel
// (settings "smtp".passEnc) are sealed with KEY_VAULT_SECRET ("v1.k.…") or, when it
// was not set, with COOKIE_SECRET ("v1.c.…"). If the .env lost or changed that secret
// (say an update unzipped a different .env over it), the panel would show every key
// as "not stored", offer to REISSUE keys customers already hold, and stop sending
// mail. So: open the newest value of each kind and check a key really hashes to its
// keyHash. Any failure exits 1 and deploy.sh stops before touching the running app.

import { MongoClient } from "mongodb";
import { open, hashKey, secretBytes, MIN_VAULT_SECRET } from "./vault-lib.mjs";

const uri = process.env.MONGODB_URI;
const dbName = process.env.MONGODB_DB || "textsoft_licenses";
const NAME = { k: "KEY_VAULT_SECRET", c: "COOKIE_SECRET" };

if (!uri) {
  console.error("!! vault-check: MONGODB_URI is not set. Run: node --env-file=.env scripts/vault-check.mjs");
  process.exit(1);
}

const problems = [];
const notes = [];

const client = new MongoClient(uri, { serverSelectionTimeoutMS: 8000 });
try {
  await client.connect();
} catch (e) {
  console.error(`!! vault-check: cannot reach MongoDB at ${uri.replace(/\/\/[^@/]*@/, "//***@")}: ${e.message}`);
  process.exit(1);
}

try {
  const db = client.db(dbName);
  const keys = db.collection("licenseKeys");

  for (const sid of ["k", "c"]) {
    const total = await keys.countDocuments({ keyEnc: { $regex: `^v1\\.${sid}\\.` } });
    if (!total) continue;
    const doc = await keys.findOne(
      { keyEnc: { $regex: `^v1\\.${sid}\\.` } },
      { sort: { createdUtc: -1 }, projection: { keyEnc: 1, keyHash: 1, serial: 1, seq: 1 } },
    );
    const where = `${doc.serial} #${doc.seq}`;
    if (!secretBytes(sid)) {
      problems.push(`${total} key(s) were sealed with ${NAME[sid]}, but ${NAME[sid]} is not set in this .env (e.g. ${where}).`);
      continue;
    }
    const plain = open("keys", doc.keyEnc);
    if (plain === null) problems.push(`${NAME[sid]} in this .env does not open the stored key ${where} — it differs from the one that sealed it.`);
    else if (hashKey(plain) !== doc.keyHash) problems.push(`Key ${where} opens but does not match its stored hash — the database or secret is not the expected one.`);
    else notes.push(`${total} key(s) sealed with ${NAME[sid]}: OK (checked ${where})`);
  }

  const smtp = await db.collection("settings").findOne({ _id: "smtp" }, { projection: { passEnc: 1 } });
  if (smtp?.passEnc) {
    if (open("smtp", smtp.passEnc) === null)
      problems.push("The SMTP password saved in the panel no longer opens with this .env's secrets — mail would stop.");
    else notes.push("Saved SMTP password: OK");
  }

  const vault = (process.env.KEY_VAULT_SECRET ?? "").trim();
  if (vault && vault.length < MIN_VAULT_SECRET) {
    const sealedWithIt = await keys.countDocuments({ keyEnc: { $regex: "^v1\\.k\\." } });
    const msg = `KEY_VAULT_SECRET is only ${vault.length} characters; use at least ${MIN_VAULT_SECRET} (openssl rand -base64 32).`;
    if (sealedWithIt || smtp?.passEnc?.startsWith("v1.k.")) notes.push(`WARNING: ${msg} Data is already sealed with it, so keep it (changing it would orphan that data).`);
    else problems.push(`${msg} Nothing is sealed with it yet, so replace it now.`);
  }
  if (vault) {
    const old = await keys.countDocuments({ keyEnc: { $regex: "^v1\\.c\\." } });
    if (old) notes.push(`${old} key(s) are still sealed with COOKIE_SECRET. Move them under KEY_VAULT_SECRET: node --env-file=.env scripts/vault-reseal.mjs --apply`);
  }
} finally {
  await client.close();
}

for (const n of notes) console.log("   vault: " + n);
if (problems.length) {
  console.error("!! vault-check FAILED — the app was NOT restarted.");
  for (const p of problems) console.error("   - " + p);
  console.error("   KEY_VAULT_SECRET/COOKIE_SECRET differs from the one that sealed existing data.");
  console.error("   Restore the old value in .env from your backup, then run ./deploy.sh again.");
  process.exit(1);
}
console.log("   vault: secrets match the stored data.");
