import type { Metadata } from "next";

// The page is a client component, so its browser-tab title is set here: the serial,
// which is what the URL carries (/admin/orders/TXS-2026-0001).
export function generateMetadata({ params }: { params: { id: string } }): Metadata {
  let id = params.id;
  try {
    id = decodeURIComponent(id);
  } catch {
    /* keep it as it came */
  }
  return { title: /^TXS-/i.test(id) ? id.toUpperCase() : "Purchase" };
}

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
