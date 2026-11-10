// What the Explorer page and its API routes (/api/rows, /api/export) agree
// on: the URL parameters beyond the shared event / request filters, and the
// response shapes. Safe to import anywhere (no server code, no hooks).
import type { JsonCondition, RequestFilter } from "@/lib/filters";
import { EVENT_TABLE_COLUMNS, REQUEST_COLUMNS } from "@/lib/types";

export type ExplorerTable = "events" | "requests";

export const PAGE_SIZES = [100, 500, 1000, 5000] as const;
export const DEFAULT_LIMIT = 100;
export const MAX_LIMIT = 5000;

// A data field flattened into a column is named "data.<key>" (the grid has
// dot notation switched off, so the dot is just part of the name).
export const DATA_PREFIX = "data.";
// fields=* : let the server pick the data fields (see autoFields there).
export const AUTO_FIELDS = "*";

export const FIELD_NAME = /^[A-Za-z0-9_]+$/;
export const EVENT_COLUMN_SET = new Set<string>(EVENT_TABLE_COLUMNS);
export const REQUEST_COLUMN_SET = new Set<string>(REQUEST_COLUMNS);

// The Explorer's own parameters (the filters themselves are parsed by
// parseEventFilter / parseRequestFilter from lib/filters.ts).
//   table=events|requests   sort=<column or data.key>   dir=asc|desc
//   offset, limit           fields=a,b (or *)           raw=1|0 (data JSON column)
//   dedupe=0 (show every stored copy)                    requestId=<requests.id>
//   id=<row id> (one row with all its details)
export type ExplorerView = {
  table: ExplorerTable;
  sort: string;
  dir: "asc" | "desc";
  offset: number;
  limit: number;
  fields: string[] | "auto"; // "auto" when the URL has no fields param or fields=*
  raw: boolean | null; // null = not given (the page picks a default)
  dedupe: boolean;
  requestId: number | null;
  id: number | null;
};

function intOrNull(value: string | null): number | null {
  if (value === null || value.trim() === "") {
    return null;
  }
  const n = Number(value);
  return Number.isInteger(n) && n >= 0 ? n : null;
}

export function parseView(params: URLSearchParams): ExplorerView {
  const table: ExplorerTable = params.get("table") === "requests" ? "requests" : "events";
  const columns = table === "events" ? EVENT_COLUMN_SET : REQUEST_COLUMN_SET;
  const rawSort = (params.get("sort") || "").trim();
  const sortIsData = table === "events" && rawSort.startsWith(DATA_PREFIX) && FIELD_NAME.test(rawSort.slice(DATA_PREFIX.length));
  const limit = intOrNull(params.get("limit")) ?? DEFAULT_LIMIT;
  const fieldsParam = params.get("fields");
  const rawParam = params.get("raw");
  return {
    table,
    sort: columns.has(rawSort) || sortIsData ? rawSort : "id",
    dir: params.get("dir") === "asc" ? "asc" : "desc",
    offset: intOrNull(params.get("offset")) ?? 0,
    limit: Math.min(MAX_LIMIT, Math.max(1, limit)),
    fields:
      fieldsParam === null || fieldsParam.trim() === AUTO_FIELDS
        ? "auto"
        : fieldsParam
            .split(",")
            .map((field) => field.trim())
            .filter((field) => FIELD_NAME.test(field)),
    raw: rawParam === "1" ? true : rawParam === "0" ? false : null,
    dedupe: params.get("dedupe") !== "0",
    requestId: intOrNull(params.get("requestId")),
    id: intOrNull(params.get("id")),
  };
}

// RequestFilter -> URL query (lib/filters.ts only has the parsing side).
export function requestFilterToParams(filter: Partial<RequestFilter>): URLSearchParams {
  const params = new URLSearchParams();
  if (filter.includeTest) params.set("includeTest", "1");
  if (filter.clientIp) params.set("clientIp", filter.clientIp);
  if (filter.method) params.set("method", filter.method);
  if (filter.path) params.set("path", filter.path);
  if (filter.status) params.set("status", filter.status);
  if (filter.from) params.set("from", filter.from);
  if (filter.to) params.set("to", filter.to);
  if (filter.search) params.set("search", filter.search);
  return params;
}

// ---- Responses ----

// GET /api/rows?table=...&<filters>&sort&dir&offset&limit&fields
export type RowsResponse = {
  table: ExplorerTable;
  columns: string[]; // every column of `rows`, in display order
  fields: string[]; // the data keys flattened into "data.<key>" columns
  rows: Record<string, unknown>[];
  total: number | null; // null = counting took too long ("many")
  offset: number;
  limit: number;
  ms: number;
};

// GET /api/rows?table=events&id=N
export type EventDetailResponse = {
  table: "events";
  row: Record<string, unknown> & { data: Record<string, unknown> };
  copies: number; // rows stored with the same event_id (2+ = a retried batch)
  firstCopyId: number | null; // the copy "hide duplicates" keeps
};

// GET /api/rows?table=requests&id=N
export type RequestDetailResponse = {
  table: "requests";
  row: Record<string, unknown>;
  headers: Record<string, unknown>;
  body: string | null;
  bodyTruncated: boolean;
  storedEvents: number; // events rows that came in with this request
};

// ---- Time inputs ----
// The database stores received_at in UTC ("YYYY-MM-DD HH:MM:SS"); the date
// inputs show the viewer's local time.

function pad(n: number): string {
  return String(n).padStart(2, "0");
}

// "2026-10-13 02:40:00" (UTC) -> "2026-10-12T19:40:00" (local, for <input type="datetime-local">)
export function utcToLocalInput(utc: string): string {
  if (!utc) {
    return "";
  }
  const date = new Date(utc.replace(" ", "T") + (utc.endsWith("Z") ? "" : "Z"));
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return (
    date.getFullYear() +
    "-" +
    pad(date.getMonth() + 1) +
    "-" +
    pad(date.getDate()) +
    "T" +
    pad(date.getHours()) +
    ":" +
    pad(date.getMinutes()) +
    ":" +
    pad(date.getSeconds())
  );
}

// The inverse: local input value -> UTC "YYYY-MM-DD HH:MM:SS" ("" when empty or invalid).
export function localInputToUtc(local: string): string {
  if (!local) {
    return "";
  }
  const date = new Date(local);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return date.toISOString().slice(0, 19).replace("T", " ");
}

// ---- Data conditions ----

// Why a data condition cannot run (null when it can): >, >=, <, <= compare
// numbers, so they need a number to compare with.
export function jsonConditionProblem(condition: JsonCondition): string | null {
  const numeric = condition.op !== "=" && condition.op !== "!=" && condition.op !== "contains";
  if (numeric && (condition.value.trim() === "" || !Number.isFinite(Number(condition.value)))) {
    return "data." + condition.field + " " + condition.op + " needs a number (got “" + condition.value + "”).";
  }
  return null;
}
