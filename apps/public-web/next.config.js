const browserApiUrl = process.env.NEXT_PUBLIC_API_URL || '';
const apiProxyTarget = process.env.API_PROXY_TARGET || 'http://localhost:8080';

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  env: {
    NEXT_PUBLIC_API_URL: browserApiUrl,
  },
  async rewrites() {
    return [
      { source: '/api/:path*', destination: `${apiProxyTarget}/api/:path*` },
      { source: '/hubs/:path*', destination: `${apiProxyTarget}/hubs/:path*` },
    ];
  },
  async headers() {
    return securityHeaders();
  },
};

function securityHeaders() {
  const externalConnections = browserApiUrl
    ? ` ${browserApiUrl} ${process.env.NEXT_PUBLIC_WS_URL || browserApiUrl.replace(/^http/, 'ws')}`
    : '';
  return [{
    source: '/(.*)',
    headers: [
      {
        key: 'Content-Security-Policy',
        value: [
          "default-src 'self'",
          "base-uri 'self'",
          "object-src 'none'",
          "frame-ancestors 'none'",
          "form-action 'self'",
          "img-src 'self' data: blob:",
          "font-src 'self' data:",
          "style-src 'self' 'unsafe-inline'",
          "script-src 'self' 'unsafe-inline'",
          `connect-src 'self'${externalConnections}`,
        ].join('; '),
      },
      { key: 'X-Frame-Options', value: 'DENY' },
      { key: 'X-Content-Type-Options', value: 'nosniff' },
      { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
      { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
      { key: 'Cross-Origin-Opener-Policy', value: 'same-origin' },
      { key: 'X-XSS-Protection', value: '0' },
    ],
  }];
}

module.exports = nextConfig;
