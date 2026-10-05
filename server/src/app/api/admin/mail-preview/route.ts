import { getSession } from "@/lib/auth";
import { renderPurchaseEmail } from "@/lib/mail";
import { getEffectiveSmtp } from "@/lib/settings";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// GET /api/admin/mail-preview — the purchase email rendered with sample data, so
// the template can be checked in a browser without creating a purchase.
export async function GET() {
  if (!(await getSession())) return new Response("unauthorized", { status: 401 });
  const smtp = await getEffectiveSmtp();
  const { html } = renderPurchaseEmail(
    {
      to: "customer@example.com",
      customerName: "Nishant Patel",
      company: "Patel Printers",
      phoneE164: "+919825012345",
      serial: "TXS-2026-0042",
      edition: "full",
      expiryUtc: new Date(Date.now() + 365 * 86_400_000),
      quantity: 3,
      keys: [
        { seq: 1, key: "TXSF-9F2K-7QH4-M3TP" },
        { seq: 2, key: "TXSF-K9F7-RJAT-2E98" },
        { seq: 3, key: "TXSF-H48Z-R7TG-TG9N" },
      ],
      note: "Invoice INV-1042",
    },
    { helpEmail: smtp.replyTo || smtp.fromEmail },
  );
  return new Response(html, { headers: { "content-type": "text/html; charset=utf-8" } });
}
