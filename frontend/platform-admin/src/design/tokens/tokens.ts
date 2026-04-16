export type AppTokens = {
  colors: {
    brand: string;
    text: string;
    textSecondary: string;
    border: string;
    bg: string;
    bgSubtle: string;
    danger: string;
  };
  radius: {
    sm: number;
    md: number;
  };
  space: {
    xs: number;
    sm: number;
    md: number;
    lg: number;
    xl: number;
  };
  font: {
    size: number;
    family: string;
  };
};

export const tokens: AppTokens = {
  colors: {
    brand: "#155EEF",
    text: "#101828",
    textSecondary: "#667085",
    border: "#EAECF0",
    bg: "#FFFFFF",
    bgSubtle: "#F9FAFB",
    danger: "#B42318",
  },
  radius: {
    sm: 6,
    md: 10,
  },
  space: {
    xs: 4,
    sm: 8,
    md: 12,
    lg: 16,
    xl: 24,
  },
  font: {
    size: 14,
    family:
      'ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, Arial, "Noto Sans", "Apple Color Emoji", "Segoe UI Emoji"',
  },
};

