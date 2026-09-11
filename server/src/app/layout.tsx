import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "TextSoft Licenses",
  description: "TextSoft license administration — Beyond Typing.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
