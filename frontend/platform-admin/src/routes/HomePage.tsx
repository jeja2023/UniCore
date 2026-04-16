import React from "react";
import { useI18n } from "../i18n/I18nProvider";
import { DetailPageTemplate } from "../components/patterns/DetailPageTemplate";
import { useTheme } from "../design/theme/ThemeProvider";

export function HomePage() {
  const { t } = useI18n();
  const { tokens } = useTheme();
  return (
    <div
      style={{
        display: "grid",
        gap: tokens.space.lg,
      }}
    >
      <section
        style={{
          border: `1px solid ${tokens.colors.border}`,
          borderRadius: tokens.radius.md,
          background: tokens.colors.bg,
          padding: tokens.space.lg,
        }}
      >
        <h2 style={{ margin: 0, fontSize: 24 }}>{t("welcome")}</h2>
        <div style={{ color: tokens.colors.textSecondary, marginTop: tokens.space.sm }}>{t("welcomeDesc")}</div>
      </section>
      <DetailPageTemplate
        title="基础能力状态"
        rows={[
          { label: "Auth", value: "已接入" },
          { label: "RBAC", value: "已接入" },
          { label: "Audit", value: "已接入" },
          { label: "Design System", value: "已接入" },
        ]}
      />
    </div>
  );
}

