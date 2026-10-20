import type { NextConfig } from "next";

// The API's origin. The app calls `/api/*` on its own origin and Next.js rewrites it here, so the session cookie is
// first-party (S03 D5). `next build` writes the rewrite into the build: set MSP_API_URL for the build, not only at start.
const apiUrl = process.env.MSP_API_URL ?? "http://localhost:5080";

// Pages whose link carries a token (password reset, invitation): no Referer leaves them, so the token can't reach
// another site (#44 M3). The pages also remove the token from the address bar once they have read it.
const tokenPages = ["/reset-password", "/accept-invitation"];

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  transpilePackages: ["@msp/api-client", "@msp/ui", "@msp/tokens"],
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }];
  },
  async headers() {
    return tokenPages.map((source) => ({ source, headers: [{ key: "Referrer-Policy", value: "no-referrer" }] }));
  },
};

export default nextConfig;
