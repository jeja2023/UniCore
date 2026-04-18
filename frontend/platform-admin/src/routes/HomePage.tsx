import React from "react";
import { useI18n } from "../i18n/I18nProvider";
import { DetailPageTemplate } from "../components/patterns/DetailPageTemplate";
import { useTheme } from "../design/theme/ThemeProvider";
import { createGlassPanelStyle } from "../styles/glass";

export function HomePage() {
  const { t } = useI18n();
  const { tokens } = useTheme();

  const glassCard: React.CSSProperties = {
    ...createGlassPanelStyle(tokens),
    padding: tokens.space.lg,
  };

  return (
    <div
      style={{
        display: "grid",
        gap: tokens.space.lg,
      }}
    >
      <section style={glassCard}>
        <h2 style={{ margin: 0, fontSize: 22, fontWeight: 800, letterSpacing: "-0.02em" }}>{t("welcome")}</h2>
        <div style={{ color: tokens.colors.textSecondary, marginTop: tokens.space.sm, lineHeight: 1.65 }}>
          {t("welcomeDesc")}
        </div>
      </section>
      <DetailPageTemplate
        title="基础能力"
        rows={[
          { label: "认证能力", value: "已接入" },
          { label: "权限能力", value: "已接入" },
          { label: "审计能力", value: "已接入" },
          { label: "设计体系", value: "已接入" },
        ]}
      />
    </div>
  );
}
