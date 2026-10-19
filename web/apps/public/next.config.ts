import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  transpilePackages: ["@msp/api-client", "@msp/ui", "@msp/tokens"],
};

export default nextConfig;
