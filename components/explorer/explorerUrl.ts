// The Explorer keeps every filter in the page URL (so a view can be shared
// by copying the address). These helpers turn the URL into filter drafts and
// API URLs, and drafts back into URL changes. Safe anywhere (no hooks).
import { EVENTS, describeField, fieldsOf } from "@/lib/catalog";
import { eventFilterToParams, intersectRange, parseEventFilter, parseRequestFilter, type JsonCondition } from "@/lib/filters";
import type { GlobalFilters } from "@/lib/globalFilters";
import type { MetaResponse } from "@/lib/types";
import { AUTO_FIELDS, DATA_PREFIX, EVENT_COLUMN_SET, parseView, requestFilterToParams } from "./explorerShared";
import type { FieldCandidate } from "./FieldPicker";

// A change to the URL: a value sets the parameter (even ""), an array repeats
// it, null removes it.
export type UrlPatch = Record<string, string | string[] | null>;

export function applyPatch(current: URLSearchParams, patch: UrlPatch): URLSearchParams {
  const next = new URLSearchParams(current.toString());
  for (const [name, value] of Object.entries(patch)) {
    next.delete(name);
    if (Array.isArray(value)) {
      value.forEach((item) => next.append(name, item));
    } else if (value !== null) {
      next.set(name, value);
    }
  }
  return next;
}

// ---- Events tab ----

// What the filter row edits (text fields stay text until applied).
export type EventDraft = {
  events: string[];
  installId: string;
  sessionId: string;
  runId: string;
  build: string;
  waveMin: string;
  waveMax: string;
  from: string; // UTC "YYYY-MM-DD HH:MM:SS"
  to: string;
  search: string;
  json: JsonCondition[];
  dedupe: boolean;
  requestId: string;
};

export const EMPTY_EVENT_DRAFT: EventDraft = {
  events: [],
  installId: "",
  sessionId: "",
  runId: "",
  build: "",
  waveMin: "",
  waveMax: "",
  from: "",
  to: "",
  search: "",
  json: [],
  dedupe: true,
  requestId: "",
};

export function eventDraftFromUrl(params: URLSearchParams): EventDraft {
  const filter = parseEventFilter(params);
  const view = parseView(params);
  return {
    events: filter.events,
    installId: filter.installId,
    sessionId: filter.sessionId,
    runId: filter.runId,
    build: filter.build,
    waveMin: filter.waveMin === null ? "" : String(filter.waveMin),
    waveMax: filter.waveMax === null ? "" : String(filter.waveMax),
    from: filter.from,
    to: filter.to,
    search: filter.search,
    json: filter.json,
    dedupe: view.dedupe,
    requestId: view.requestId === null ? "" : String(view.requestId),
  };
}

// "" -> null: an empty filter is left out of the URL.
function orNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
}

// The URL change for an applied draft. Choosing other event types also
// resets the data columns to the automatic choice for those types (and a
// sort on a data field, which the new types may not have).
export function eventDraftPatch(draft: EventDraft, previous: EventDraft, currentSort: string): UrlPatch {
  const typesChanged = draft.events.join(",") !== previous.events.join(",");
  return {
    event: orNull(draft.events.join(",")),
    installId: orNull(draft.installId),
    sessionId: orNull(draft.sessionId),
    runId: orNull(draft.runId),
    build: orNull(draft.build),
    waveMin: orNull(draft.waveMin),
    waveMax: orNull(draft.waveMax),
    from: orNull(draft.from),
    to: orNull(draft.to),
    search: orNull(draft.search),
    json: draft.json.filter((c) => c.field).map((c) => c.field + "|" + c.op + "|" + c.value),
    dedupe: draft.dedupe ? null : "0",
    requestId: orNull(draft.requestId),
    ...(typesChanged ? { fields: null, raw: null } : {}),
    ...(typesChanged && currentSort.startsWith(DATA_PREFIX) ? { sort: null } : {}),
    offset: null,
  };
}

// /api/rows and /api/export parameters for the events tab: the page URL's
// filters plus the global ones (test plays, level, time range) from the top
// bar; the page's own From / To narrow the top bar's time range.
// `columns` pins the data columns (export uses what the grid shows).
export function eventApiParams(
  page: URLSearchParams,
  global: GlobalFilters,
  columns?: { fields: string[]; raw: boolean },
): URLSearchParams {
  const filter = parseEventFilter(page);
  const view = parseView(page);
  const range = intersectRange(global, filter);
  const params = eventFilterToParams({ ...filter, includeTest: global.includeTest, level: global.level, from: range.from, to: range.to });
  params.set("table", "events");
  params.set("sort", view.sort);
  params.set("dir", view.dir);
  params.set("offset", String(view.offset));
  params.set("limit", String(view.limit));
  if (columns) {
    params.set("fields", columns.fields.join(","));
    params.set("raw", columns.raw ? "1" : "0");
  } else {
    params.set("fields", view.fields === "auto" ? AUTO_FIELDS : view.fields.join(","));
    if (view.raw !== null) params.set("raw", view.raw ? "1" : "0");
  }
  if (!view.dedupe) params.set("dedupe", "0");
  if (view.requestId !== null) params.set("requestId", String(view.requestId));
  return params;
}

// ---- Requests tab ----

export type RequestDraft = {
  clientIp: string;
  method: string;
  path: string;
  status: string;
  from: string;
  to: string;
  search: string;
};

export const EMPTY_REQUEST_DRAFT: RequestDraft = { clientIp: "", method: "", path: "", status: "", from: "", to: "", search: "" };

export function requestDraftFromUrl(params: URLSearchParams): RequestDraft {
  const { clientIp, method, path, status, from, to, search } = parseRequestFilter(params);
  return { clientIp, method, path, status, from, to, search };
}

export function requestDraftPatch(draft: RequestDraft): UrlPatch {
  return {
    clientIp: orNull(draft.clientIp),
    method: orNull(draft.method),
    path: orNull(draft.path),
    status: orNull(draft.status),
    from: orNull(draft.from),
    to: orNull(draft.to),
    search: orNull(draft.search),
    offset: null,
  };
}

export function requestApiParams(page: URLSearchParams, global: GlobalFilters): URLSearchParams {
  const view = parseView(page);
  const filter = parseRequestFilter(page);
  const range = intersectRange(global, filter);
  const params = requestFilterToParams({ ...filter, includeTest: global.includeTest, from: range.from, to: range.to });
  params.set("table", "requests");
  params.set("sort", view.sort);
  params.set("dir", view.dir);
  params.set("offset", String(view.offset));
  params.set("limit", String(view.limit));
  return params;
}

// ---- Data fields ----

// Every data key the chosen event types can have (all types when none is
// chosen): catalog fields first, then keys seen in the stored data.
// Keys that repeat a table column are left out.
export function fieldCandidates(types: string[], meta: MetaResponse | null, keep: string[] = []): FieldCandidate[] {
  const seenFields = meta?.dataFields ?? {};
  const chosen = types.length > 0 ? types : [...new Set([...Object.keys(EVENTS), ...Object.keys(seenFields)])];
  const byKey = new Map<string, FieldCandidate>();
  for (const type of chosen) {
    const seen = new Set(seenFields[type] ?? []);
    const keys = [...Object.keys(fieldsOf(type)), ...(seenFields[type] ?? [])];
    for (const key of keys) {
      if (EVENT_COLUMN_SET.has(key)) {
        continue;
      }
      const existing = byKey.get(key);
      if (existing) {
        existing.seen = existing.seen || seen.has(key);
      } else {
        byKey.set(key, { key, seen: seen.has(key), doc: describeField(key, type) });
      }
    }
  }
  for (const key of keep) {
    if (!byKey.has(key)) {
      byKey.set(key, { key, seen: true, doc: describeField(key) });
    }
  }
  const list = [...byKey.values()];
  // With no type chosen the list is long: alphabetical reads better there.
  return types.length > 0 ? list : list.sort((a, b) => a.key.localeCompare(b.key));
}

// The doc of a data key for the chosen event types (first type that has it).
export function fieldDoc(key: string, types: string[]): string | undefined {
  for (const type of types) {
    if (fieldsOf(type)[key]) {
      return describeField(key, type);
    }
  }
  return describeField(key);
}
