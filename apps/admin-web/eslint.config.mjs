import nextConfig from 'eslint-config-next/core-web-vitals';

const eslintConfig = [
  ...nextConfig,
  {
    ignores: ['.next/**', 'node_modules/**', 'playwright-report/**', 'test-results/**'],
    rules: {
      // These effects hydrate browser-only auth state and trigger asynchronous
      // API refreshes. Keep their established behavior during this upgrade.
      'react-hooks/set-state-in-effect': 'off',
    },
  },
];

export default eslintConfig;
