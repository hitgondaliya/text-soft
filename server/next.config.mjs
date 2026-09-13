/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // The client posts from a desktop app, not a browser origin; keep responses lean.
  poweredByHeader: false,
  // Running `next build` while `next dev` is up overwrites the dev server's
  // chunks in .next, and the running panel then 404s its own CSS and JS and
  // renders as unstyled HTML. Giving the dev run its own directory keeps a build
  // from pulling the ground out from under it.
  distDir: process.env.NEXT_DIST_DIR || ".next",
};

export default nextConfig;
