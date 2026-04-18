export type AppTokens = {
  colors: {
    brand: string;
    brandSoft: string;
    text: string;
    textSecondary: string;
    border: string;
    bg: string;
    bgSubtle: string;
    danger: string;
  };
  glass: {
    surface: string;
    surfaceStrong: string;
    border: string;
    borderHighlight: string;
    shadow: string;
    shadowHover: string;
    blur: string;
  };
  radius: {
    sm: number;
    md: number;
    lg: number;
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
    brandSoft: "rgba(21, 94, 239, 0.12)",
    text: "#101828",
    textSecondary: "#667085",
    border: "#D0D5DD",
    bg: "#FFFFFF",
    bgSubtle: "#F5F7FA",
    danger: "#B42318",
  },
  glass: {
    surface: "rgba(255, 255, 255, 0.9)",
    surfaceStrong: "rgba(255, 255, 255, 0.95)",
    border: "rgba(148, 163, 184, 0.48)",
    borderHighlight: "rgba(21, 94, 239, 0.45)",
    shadow: "0 10px 28px rgba(15, 23, 42, 0.1), 0 2px 10px rgba(15, 23, 42, 0.06)",
    shadowHover: "0 14px 36px rgba(15, 23, 42, 0.14), 0 4px 14px rgba(21, 94, 239, 0.12)",
    blur: "18px",
  },
  radius: {
    sm: 8,
    md: 14,
    lg: 22,
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
      '"PingFang SC","Microsoft YaHei",ui-sans-serif,system-ui,-apple-system,"Segoe UI",Roboto,Arial,"Noto Sans","Apple Color Emoji","Segoe UI Emoji"',
  },
};

