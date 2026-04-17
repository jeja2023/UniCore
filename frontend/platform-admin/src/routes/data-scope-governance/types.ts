export type ApiEnvelope<T> = { data: T };

export type DataScopeMetadata = {
  fields: Array<{ code: string; name: string; type: string }>;
  operators: string[];
  joiners: string[];
};

export type DataScopeTemplate = {
  code: string;
  name: string;
  scope: string;
  expression: string;
};

export type DataScopeRule = {
  field: string;
  operator: string;
  value: string;
  joinWithPrevious?: string;
  openGroupCount: number;
  closeGroupCount: number;
};

export type ParsedToken = { value: string; kind: string };

export type ParseResult = {
  isValid: boolean;
  errorMessage?: string | null;
  tokens: ParsedToken[];
};

export type ScopeDetail = {
  roleCode: string;
  scope: string;
  customExpression?: string | null;
  revision: number;
  updatedAt?: string | null;
};

export type ScopeHistoryItem = {
  historyId: string;
  version: number;
  scope: string;
  customExpression?: string | null;
  changedBy: string;
  changedAt: string;
};

export type ScopeDiff = {
  roleCode: string;
  fromVersion: number;
  toVersion: number;
  fromScope: string;
  toScope: string;
  fromExpression?: string | null;
  toExpression?: string | null;
  addedTokens: string[];
  removedTokens: string[];
  isSemanticallySame: boolean;
  summary: string;
};

export type GovernanceStatusBarProps = {
  loading: boolean;
  status: string | null;
  error: string | null;
};
