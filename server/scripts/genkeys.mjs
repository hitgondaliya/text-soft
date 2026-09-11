// Generate a fresh Ed25519 keypair for signing license tokens.
//   node scripts/genkeys.mjs
// Prints the env values for the server and the PUBLIC key (raw, base64) to embed
// in the WPF client. The PRIVATE key must go into .env.local / the VPS env only.

import crypto from "node:crypto";

const { publicKey, privateKey } = crypto.generateKeyPairSync("ed25519");

const privPem = privateKey.export({ type: "pkcs8", format: "pem" });
const pubPem = publicKey.export({ type: "spki", format: "pem" });

// Raw 32-byte public key = last 32 bytes of the DER SPKI encoding.
const pubDer = publicKey.export({ type: "spki", format: "der" });
const pubRaw = pubDer.subarray(pubDer.length - 32);

console.log("# ---- server env (.env.local / VPS) ----");
console.log(`LICENSE_PRIVATE_KEY=${Buffer.from(privPem).toString("base64")}`);
console.log(`LICENSE_PUBLIC_KEY=${Buffer.from(pubPem).toString("base64")}`);
console.log();
console.log("# ---- WPF client (embed this PUBLIC key, base64 of 32 raw bytes) ----");
console.log(`PublicKeyBase64 = ${pubRaw.toString("base64")}`);
