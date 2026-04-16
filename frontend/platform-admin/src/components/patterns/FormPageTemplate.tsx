import React from "react";
import { PageSection } from "./PageSection";

export function FormPageTemplate({
  title,
  onSubmit,
  children,
  actions,
}: {
  title: string;
  onSubmit: (e: React.FormEvent) => void;
  children: React.ReactNode;
  actions?: React.ReactNode;
}) {
  return (
    <PageSection title={title}>
      <form onSubmit={onSubmit}>
        <div style={{ display: "grid", rowGap: 10 }}>{children}</div>
        {actions ? <div style={{ marginTop: 12 }}>{actions}</div> : null}
      </form>
    </PageSection>
  );
}

