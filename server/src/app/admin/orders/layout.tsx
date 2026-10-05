// The page is a client component, so its browser-tab title is set here.
// An object, so the purchase pages under it keep the " · TextSoft Licences" suffix.
export const metadata = { title: { default: "Purchases", template: "%s · TextSoft Licences" } };

export default function Layout({ children }: { children: React.ReactNode }) {
  return children;
}
