---
name: license-server-purchases
description: "Licence server sells PURCHASES with a serial (TXS-2026-0001), keys issued under it; run the panel with `npm run dev:db`."
metadata: 
  node_type: memory
  type: project
  originSessionId: 69d8b2a2-ed7d-447f-9ed1-ba585383f6f5
  modified: 2026-09-13T12:55:57.285Z
---

`server/` is the Next.js licence admin panel + activation API (MongoDB, Ed25519).

**A key is never issued alone.** Every sale is a **purchase** with a serial —
`TXS-2026-0001` — and its keys are numbered 1..N under it. Selling 40 keys makes
one serial with 40 keys. `orders` collection; `licenseKeys` carries `orderId`,
`serial`, `seq`. **The serial belongs to the sale, not the person** — a repeat
order gets its own; the customer's history is found by searching their email.

Serials come from an atomic `$inc` on a `counters` doc (`serial-<year>`), never
from counting orders, which would race. Verified with 8 concurrent creates.

`POST /api/admin/keys` is **closed (410)** — use `POST /api/admin/orders`.
`/api/admin/orders/:idOrSerial` accepts either the Mongo id or the serial itself.
PATCH actions act on the whole purchase: `extendAll` (adds to the run-to date, or
today if lapsed), `setExpiryAll`, `revokeAll`, `unrevokeAll`, `updateCustomer`
(which also refreshes the copy denormalised onto each key).

**This machine has no MongoDB and no Docker.** `npm run dev:db` starts a
throwaway in-memory mongod, seeds the admin from `.env.local`, and runs the panel
against it (`npm run dev:db -- 3100` for another port). Set
`MONGOMS_DISABLE_POSTINSTALL=1` if npm blocks the install script.

**Gotcha when running it from here:** do not pipe the dev server through
`Select-Object -First N` — closing the pipe breaks the child's stdout, logging
stops and requests start failing with 500s. Redirect to a file instead.

Admin: `admin@textsoft.local` / `change-me-now` from `.env.local`.
Related: [[licensing-plan]], [[textsoft-brand]].
