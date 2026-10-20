// TypeScript view of the design tokens. Colour values live only in tokens.css; these are references to its CSS
// variables so code (e.g. charts, Flutter export later) uses the same tokens and follows light/dark.
const cssVar = (name: string) => `var(--msp-${name})`;

export const colors = {
  background: cssVar("color-background"),
  surface: cssVar("color-surface"),
  surfaceRaised: cssVar("color-surface-raised"),
  surfaceMuted: cssVar("color-surface-muted"),
  border: cssVar("color-border"),
  text: cssVar("color-text"),
  textMuted: cssVar("color-text-muted"),
  focus: cssVar("color-focus"),
  overlay: cssVar("color-overlay"),
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
    dangerContrast: cssVar("color-danger-contrast"),
    info: cssVar("color-info"),
  },
} as const;

/** 4 px base spacing scale; keys are the step in 4 px units (space[4] is 16 px). */
export const space = {
  1: cssVar("space-1"),
  2: cssVar("space-2"),
  3: cssVar("space-3"),
  4: cssVar("space-4"),
  5: cssVar("space-5"),
  6: cssVar("space-6"),
  8: cssVar("space-8"),
  10: cssVar("space-10"),
  12: cssVar("space-12"),
  16: cssVar("space-16"),
} as const;

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

/** Elevation 1–3 (0 is no shadow), tuned for both themes. */
export const elevation = {
  1: cssVar("elevation-1"),
  2: cssVar("elevation-2"),
  3: cssVar("elevation-3"),
} as const;

export const motion = {
  fast: cssVar("duration-fast"),
  standard: cssVar("duration-standard"),
} as const;
