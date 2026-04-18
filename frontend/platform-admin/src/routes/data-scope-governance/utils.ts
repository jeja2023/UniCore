export function nowIsoText(input?: string | null) {
  if (!input) return "-";
  const date = new Date(input);
  if (Number.isNaN(date.getTime())) return input;
  return date.toLocaleString("zh-CN");
}

export function formatDataScopeFieldOptionLabel(f: { code: string; name: string; type?: string }) {
  const hasName = Boolean(f.name?.trim()) && f.name.trim() !== f.code;
  const main = hasName ? `${f.name.trim()}（${f.code}）` : f.code;
  return f.type ? `${main} · ${f.type}` : main;
}

export function formatDataScopeTokenKind(kind: string) {
  const k = kind.trim().toLowerCase();
  const map: Record<string, string> = {
    identifier: "标识符",
    literal: "字面量",
    operator: "运算符",
    keyword: "关键字",
    string: "字符串",
    number: "数字",
    punctuation: "标点",
    whitespace: "空白",
  };
  return map[k] ?? kind;
}
