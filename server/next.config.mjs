/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // The client posts from a desktop app, not a browser origin; keep responses lean.
  poweredByHeader: false,
};

export default nextConfig;
