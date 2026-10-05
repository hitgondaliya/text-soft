// Move everything sealed with COOKIE_SECRET ("v1.c.…") under KEY_VAULT_SECRET ("v1.k.…").
//
//   node --env-file=.env scripts/vault-reseal.mjs            dry run: counts only
//   node --env-file=.env scripts/vault-reseal.mjs --apply    re-seal
//
// Without KEY_VAULT_SECRET the panel seals licence keys and the saved SMTP password
// with a key derived from COOKIE_SECRET, so rotating COOKIE_SECRET (the normal answer
// to a leaked session secret) would make them unreadable. Once KEY_VAULT_SECRET is in
// .env, run this once and COOKIE_SECRET can be rotated freely afterwards.
// Run it only after ./deploy.sh has restarted the panel with KEY_VAULT_SECRET in .env:
// a panel started without it cannot open "v1.k." values.
//
// Safe to re-run: each key is opened, checked against its keyHash, re-sealed, opened
// again, and written only if its keyEnc did not change in the meantime.

import { MongoClient } from "mongodb";
import { open, seal, hashKey, secretBytes, MIN_VAULT_SECRET } from "./vault-lib.mjs";

const apply = process.argv.includes("--apply");
const uri = process.env.MONGODB_URI;
const dbName = process.env.MONGODB_DB || "textsoft_licenses";

if (!uri) {
  console.error("MONGODB_URI is not set. Run: node --env-file=.env scripts/vault-reseal.mjs [--apply]");
  process.exit(1);
}
if (!secretBytes("k")) {
  console.error("KEY_VAULT_SECRET is not set. Add it to .env first: openssl rand -base64 32");
  process.exit(1);
}
if (process.env.KEY_VAULT_SECRET.trim().length < MIN_VAULT_SECRET) {
  console.error(`KEY_VAULT_SECRET is shorter than ${MIN_VAULT_SECRET} characters. Use: openssl rand -base64 32`);
  process.exit(1);
}
if (!secretBytes("c")) {
  console.error("COOKIE_SECRET is not set, so values sealed with it cannot be opened.");
  process.exit(1);
}

const client = new MongoClient(uri, { serverSelectionTimeoutMS: 8000 });
await client.connect();
const db = client.db(dbName);
const keys = db.collection("licenseKeys");

let moved = 0;
let unreadable = 0;
const cursor = keys.find({ keyEnc: { $regex: "^v1\\.c\\." } }, { projection: { keyEnc: 1, keyHash: 1, serial: 1, seq: 1 } });
for await (const k of cursor) {
  const plain = open("keys", k.keyEnc);
  if (plain === null || hashKey(plain) !== k.keyHash) {
    unreadable++;
    console.warn(`  skip ${k.serial} #${k.seq}: does not open with COOKIE_SECRET (or does not match its hash)`);
    continue;
  }
  const next = seal("keys", plain, "k");
  if (open("keys", next) !== plain) throw new Error("re-seal round-trip failed — nothing more written");
  if (apply) {
    const r = await keys.updateOne({ _id: k._id, keyEnc: k.keyEnc }, { $set: { keyEnc: next } });
    if (r.modifiedCount === 1) moved++;
  } else moved++;
}

let smtpMoved = false;
const settings = db.collection("settings");
const smtp = await settings.findOne({ _id: "smtp" }, { projection: { passEnc: 1 } });
if (smtp?.passEnc?.startsWith("v1.c.")) {
  const plain = open("smtp", smtp.passEnc);
  if (plain === null) console.warn("  skip SMTP password: does not open with COOKIE_SECRET (type it again in Settings → Email)");
  else {
    const next = seal("smtp", plain, "k");
    if (apply) {
      const r = await settings.updateOne({ _id: "smtp", passEnc: smtp.passEnc }, { $set: { passEnc: next } });
      smtpMoved = r.modifiedCount === 1;
    } else smtpMoved = true;
  }
}

await client.close();
const verb = apply ? "Re-sealed" : "Would re-seal";
console.log(`${verb} ${moved} key(s)${smtpMoved ? " and the SMTP password" : ""} under KEY_VAULT_SECRET.${unreadable ? ` ${unreadable} could not be opened and were left as they are.` : ""}`);
if (!apply && (moved || smtpMoved)) console.log("Run again with --apply to write the changes.");
if (apply) console.log("COOKIE_SECRET can now be rotated (that signs everyone out of the panel once).");
