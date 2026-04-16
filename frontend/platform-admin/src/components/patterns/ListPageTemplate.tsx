import React from "react";
import { PageSection } from "./PageSection";

export function ListPageTemplate({
  title,
  toolbar,
  children,
}: {
  title: string;
  toolbar?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <PageSection title={title}>
      {toolbar ? <div style={{ marginBottom: 12 }}>{toolbar}</div> : null}
      {children}
    </PageSection>
  );
}

