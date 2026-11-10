// Filters shared by the pages (as URL query parameters) and the API routes
// (as SQL WHERE clauses). Safe to import anywhere. Values always go into the
// SQL as ? parameters, never as text.

export type JsonCondition = {
  field: string; // a key inside `data`, e.g. "kind" or "health"
  op: "=" | "!=" | ">" | ">=" | "<" | "<=" | "contains";
  value: string;
};

export type EventFilter = {
  includeTest: boolean; // include install_id 'qa-...' (the team's own test plays)
  events: string[]; // event types; empty = all
  installId: string;
  sessionId: string;
  runId: string;
  level: string; // scene name ("Prototype", "Hard"); empty = all
  build: string;
  waveMin: number | null;
  waveMax: number | null;
  from: string; // received_at >= (UTC "YYYY-MM-DD HH:MM:SS"); empty = no bound
  to: string;
  search: string; // text anywhere in the event's data
  json: JsonCondition[];
};

export const EMPTY_EVENT_FILTER: EventFilter = {
  includeTest: false,
  events: [],
  installId: "",
  sessionId: "",
  runId: "",
  level: "",
  build: "",
  waveMin: null,
  waveMax: null,
  from: "",
  to: "",
  search: "",
  json: [],
};

const JSON_OPS: JsonCondition["op"][] = ["=", "!=", ">", ">=", "<", "<=", "contains"];
const FIELD_NAME = /^[A-Za-z0-9_]+$/;

function numberOrNull(value: string | null): number | null {
  if (value === null || value.trim() === "") {
    return null;
  }
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

// URL query -> filter. Conditions on data fields use repeated "json" params
// written as field|op|value, e.g. json=kind|=|runner.
export function parseEventFilter(params: URLSearchParams): EventFilter {
  const json: JsonCondition[] = [];
  for (const raw of params.getAll("json")) {
    const [field, op, ...rest] = raw.split("|");
    if (field && FIELD_NAME.test(field) && JSON_OPS.includes(op as JsonCondition["op"])) {
      json.push({ field, op: op as JsonCondition["op"], value: rest.join("|") });
    }
  }
  const events = params
    .getAll("event")
    .flatMap((value) => value.split(","))
    .map((value) => value.trim())
    .filter(Boolean);
  return {
    includeTest: params.get("includeTest") === "1",
    events,
    installId: (params.get("installId") || "").trim(),
    sessionId: (params.get("sessionId") || "").trim(),
    runId: (params.get("runId") || "").trim(),
    level: (params.get("level") || "").trim(),
    build: (params.get("build") || "").trim(),
    waveMin: numberOrNull(params.get("waveMin")),
    waveMax: numberOrNull(params.get("waveMax")),
    from: (params.get("from") || "").trim(),
    to: (params.get("to") || "").trim(),
    search: (params.get("search") || "").trim(),
    json,
  };
}

// Filter -> URL query (the inverse of parseEventFilter; empty values left out).
export function eventFilterToParams(filter: Partial<EventFilter>): URLSearchParams {
  const params = new URLSearchParams();
  if (filter.includeTest) params.set("includeTest", "1");
  if (filter.events && filter.events.length > 0) params.set("event", filter.events.join(","));
  if (filter.installId) params.set("installId", filter.installId);
  if (filter.sessionId) params.set("sessionId", filter.sessionId);
  if (filter.runId) params.set("runId", filter.runId);
  if (filter.level) params.set("level", filter.level);
  if (filter.build) params.set("build", filter.build);
  if (filter.waveMin !== null && filter.waveMin !== undefined) params.set("waveMin", String(filter.waveMin));
  if (filter.waveMax !== null && filter.waveMax !== undefined) params.set("waveMax", String(filter.waveMax));
  if (filter.from) params.set("from", filter.from);
  if (filter.to) params.set("to", filter.to);
  if (filter.search) params.set("search", filter.search);
  for (const condition of filter.json || []) {
    params.append("json", condition.field + "|" + condition.op + "|" + condition.value);
  }
  return params;
}

// The top bar's time range combined with a page's own from / to (Explorer,
// Runs): the later start and the earlier end. UTC text sorts like the time it names.
export function intersectRange(global: { from: string; to: string }, own: { from?: string; to?: string }): { from: string; to: string } {
  const starts = [global.from, own.from ?? ""].filter(Boolean).sort();
  const ends = [global.to, own.to ?? ""].filter(Boolean).sort();
  return { from: starts[starts.length - 1] ?? "", to: ends[0] ?? "" };
}

export type SqlWhere = { sql: string; params: unknown[] };

// SQL for "this install_id is test data" (qa-...).
export function testInstallClause(column: string): string {
  return "(" + column + " IS NULL OR " + column + " NOT LIKE 'qa-%')";
}

// Filter -> " WHERE ..." for the events table (alias e by default).
export function buildEventWhere(filter: EventFilter, alias = "e"): SqlWhere {
  const parts: string[] = [];
  const params: unknown[] = [];
  const col = (name: string) => alias + "." + name;

  if (!filter.includeTest) {
    parts.push(testInstallClause(col("install_id")));
  }
  if (filter.events.length > 0) {
    parts.push(col("event") + " IN (" + filter.events.map(() => "?").join(",") + ")");
    params.push(...filter.events);
  }
  for (const [name, value] of [
    ["install_id", filter.installId],
    ["session_id", filter.sessionId],
    ["run_id", filter.runId],
    ["level", filter.level],
    ["build", filter.build],
  ] as const) {
    if (value) {
      parts.push(col(name) + " = ?");
      params.push(value);
    }
  }
  if (filter.waveMin !== null) {
    parts.push(col("wave") + " >= ?");
    params.push(filter.waveMin);
  }
  if (filter.waveMax !== null) {
    parts.push(col("wave") + " <= ?");
    params.push(filter.waveMax);
  }
  if (filter.from) {
    parts.push(col("received_at") + " >= ?");
    params.push(filter.from);
  }
  if (filter.to) {
    parts.push(col("received_at") + " <= ?");
    params.push(filter.to);
  }
  if (filter.search) {
    parts.push("CAST(" + col("data") + " AS CHAR) LIKE ?");
    params.push("%" + filter.search + "%");
  }
  for (const condition of filter.json) {
    if (!FIELD_NAME.test(condition.field)) {
      continue;
    }
    const value = "JSON_UNQUOTE(JSON_EXTRACT(" + col("data") + ", ?))";
    params.push("$." + condition.field);
    if (condition.op === "contains") {
      parts.push(value + " LIKE ?");
      params.push("%" + condition.value + "%");
    } else if (condition.op === "=" || condition.op === "!=") {
      parts.push(value + (condition.op === "=" ? " = ?" : " <> ?"));
      params.push(condition.value);
    } else if (condition.value.trim() !== "" && Number.isFinite(Number(condition.value))) {
      parts.push("CAST(" + value + " AS DECIMAL(30,6)) " + condition.op + " ?");
      params.push(Number(condition.value));
    } else {
      // not a number: compare as text (e.g. word > "m")
      parts.push(value + " " + condition.op + " ?");
      params.push(condition.value);
    }
  }
  return { sql: parts.length > 0 ? " WHERE " + parts.join(" AND ") : "", params };
}

// ---- The requests table (one row per HTTP request to the log server) ----

export type RequestFilter = {
  includeTest: boolean;
  clientIp: string;
  method: string;
  path: string;
  status: string;
  from: string;
  to: string;
  search: string; // text anywhere in the body or headers
};

export function parseRequestFilter(params: URLSearchParams): RequestFilter {
  return {
    includeTest: params.get("includeTest") === "1",
    clientIp: (params.get("clientIp") || "").trim(),
    method: (params.get("method") || "").trim(),
    path: (params.get("path") || "").trim(),
    status: (params.get("status") || "").trim(),
    from: (params.get("from") || "").trim(),
    to: (params.get("to") || "").trim(),
    search: (params.get("search") || "").trim(),
  };
}

export function buildRequestWhere(filter: RequestFilter, alias = "r"): SqlWhere {
  const parts: string[] = [];
  const params: unknown[] = [];
  const col = (name: string) => alias + "." + name;
  if (!filter.includeTest) {
    parts.push(col("body") + " NOT LIKE ?");
    params.push('%"install_id":"qa-%');
  }
  for (const [name, value] of [
    ["client_ip", filter.clientIp],
    ["method", filter.method],
    ["status", filter.status],
  ] as const) {
    if (value) {
      parts.push(col(name) + " = ?");
      params.push(value);
    }
  }
  if (filter.path) {
    parts.push(col("path") + " LIKE ?");
    params.push("%" + filter.path + "%");
  }
  if (filter.from) {
    parts.push(col("received_at") + " >= ?");
    params.push(filter.from);
  }
  if (filter.to) {
    parts.push(col("received_at") + " <= ?");
    params.push(filter.to);
  }
  if (filter.search) {
    parts.push("(" + col("body") + " LIKE ? OR CAST(" + col("headers") + " AS CHAR) LIKE ?)");
    params.push("%" + filter.search + "%", "%" + filter.search + "%");
  }
  return { sql: parts.length > 0 ? " WHERE " + parts.join(" AND ") : "", params };
}
