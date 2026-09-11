// KeyGen is retired. License keys are no longer minted in the client/build:
// under the online licensing model the Next.js admin panel generates keys and the
// server signs the license tokens with its private Ed25519 key.
//
// To create keys: run the admin panel (see server/README.md) → License keys →
// Generate keys. See docs/LICENSING-PLAN.md for the full design.

Console.WriteLine("KeyGen is retired.");
Console.WriteLine("Generate license keys in the admin panel: server/  (npm run dev → /admin).");
Console.WriteLine("See server/README.md and docs/LICENSING-PLAN.md.");
return 0;
