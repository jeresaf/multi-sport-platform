// TypeScript view of the design tokens. Colour values live only in tokens.css; these are references to its CSS
// variables so code (e.g. charts, Flutter export later) uses the same tokens and follows light/dark.
const cssVar = (name: string) => `var(--msp-${name})`;

export const colors = {
  background: cssVar("color-background"),
  surface: cssVar("color-surface"),
  surfaceRaised: cssVar("color-surface-raised"),
  border: cssVar("color-border"),
  text: cssVar("color-text"),
  textMuted: cssVar("color-text-muted"),
  focus: cssVar("color-focus"),
  brand: {
    primary: cssVar("color-brand-primary"),
    primaryHover: cssVar("color-brand-primary-hover"),
    primaryContrast: cssVar("color-brand-primary-contrast"),
    secondary: cssVar("color-brand-secondary"),
  },
  semantic: {
    live: cssVar("color-live"),
    success: cssVar("color-success"),
    warning: cssVar("color-warning"),
    danger: cssVar("color-danger"),
    info: cssVar("color-info"),
  },
} as const;

/** 4 px base spacing scale, in px. */
export const spacing = [4, 8, 12, 16, 20, 24, 32, 40, 48, 64] as const;

/** Type scale, in px. */
export const fontSizes = [12, 14, 16, 18, 20, 24, 30, 36] as const;

export const radius = {
  sm: cssVar("radius-sm"),
  md: cssVar("radius-md"),
  lg: cssVar("radius-lg"),
  full: cssVar("radius-full"),
} as const;

/** Breakpoints (min-width, px): phone < 768, tablet 768–1199, desktop ≥ 1200. */
export const breakpoints = { tablet: 768, desktop: 1200 } as const;

export const motion = {
  fast: cssVar("duration-fast"),
  standard: cssVar("duration-standard"),
} as const;
