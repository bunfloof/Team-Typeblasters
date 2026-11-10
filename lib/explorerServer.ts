// Server side of the Explorer page (used by /api/rows and /api/export):
// one page of rows, one row in full detail, or every matching row streamed
// as CSV. SELECT only: nothing here ever writes to the database.
import {
  DATA_PREFIX,
  EVENT_COLUMN_SET,
  FIELD_NAME,
  jsonConditionProblem,
  parseView,
  type EventDetailResponse,
  type ExplorerView,
  type RequestDetailResponse,
  type RowsResponse,
} from "@/components/explorer/explorerShared";
import { ENVELOPE_FIELDS, fieldsOf } from "./catalog";
import { csvLine } from "./csv";
import { parseJson, query, type Row } from "./db";
import { toEventRow } from "./events";
import { buildEventWhere, buildRequestWhere, parseEventFilter, parseRequestFilter, type EventFilter, type SqlWhere } from "./filters";
import { EVENT_TABLE_COLUMNS, REQUEST_COLUMNS, type EventRow } from "./types";

const EVENT_SELECT = "SELECT " + EVENT_TABLE_COLUMNS.map((c) => "e." + c).join(", ") + ", e.data FROM events e";
const REQUEST_SELECT = "SELECT " + REQUEST_COLUMNS.map((c) => "r." + c).join(", ") + " FROM requests r";
// Export with detail=1: also the query string, all headers (JSON) and the raw body.
const REQUEST_DETAIL_COLUMNS = [...REQUEST_COLUMNS, "query_string", "headers", "body"];
const REQUEST_SELECT_WITH_BODY = "SELECT " + REQUEST_DETAIL_COLUMNS.map((c) => "r." + c).join(", ") + " FROM requests r";

const PAGE_TIMEOUT_MS = 60000;
const COUNT_TIMEOUT_MS = 5000; // a slower count is skipped (total = null, shown as "many")
const EXPORT_BATCH = 5000; // rows per database round trip while streaming CSV
const BODY_SHOW_LIMIT = 1_000_000; // characters of a request body sent to the detail drawer

// A batch the game sent twice (the reply got lost, it retried) is stored
// twice with the same event_id: keep only the first stored copy. Rows with
// no event_id always pass (the subquery finds nothing). Uses the event_id index.
const FIRST_COPY_ONLY = "NOT EXISTS (SELECT 1 FROM events dup WHERE dup.event_id = e.event_id AND dup.id < e.id)";

// where + one more condition.
function andWhere(where: SqlWhere, clause: string, params: unknown[] = []): SqlWhere {
  return { sql: (where.sql ? where.sql + " AND " : " WHERE ") + clause, params: [...where.params, ...params] };
}

// A filter the database cannot run (answered with 400 and this message).
export class BadFilterError extends Error {}

function eventWhere(filter: EventFilter, view: ExplorerView): SqlWhere {
  for (const condition of filter.json) {
    const problem = jsonConditionProblem(condition);
    if (problem) {
      throw new BadFilterError(problem);
    }
  }
  let where = buildEventWhere(filter, "e");
  if (view.requestId !== null) {
    where = andWhere(where, "e.request_id = ?", [view.requestId]);
  }
  if (view.dedupe) {
    where = andWhere(where, FIRST_COPY_ONLY);
  }
  return where;
}

function sqlDir(dir: "asc" | "desc"): string {
  return dir === "asc" ? "ASC" : "DESC";
}

// ORDER BY for the events page. A data field sorts numbers as numbers and
// text as text (CAST turns text into 0, the second key orders it).
function eventOrder(view: ExplorerView): SqlWhere {
  const dir = sqlDir(view.dir);
  if (view.sort.startsWith(DATA_PREFIX)) {
    const path = "$." + view.sort.slice(DATA_PREFIX.length);
    const value = "JSON_UNQUOTE(JSON_EXTRACT(e.data, ?))";
    return {
      sql: " ORDER BY CAST(" + value + " AS DECIMAL(30,6)) " + dir + ", " + value + " " + dir + ", e.id " + dir,
      params: [path, path],
    };
  }
  // view.sort is a known column name here (parseView only lets those through)
  return { sql: " ORDER BY e." + view.sort + " " + dir + (view.sort === "id" ? "" : ", e.id " + dir), params: [] };
}

function requestOrder(view: ExplorerView): string {
  const dir = sqlDir(view.dir);
  return " ORDER BY r." + view.sort + " " + dir + (view.sort === "id" ? "" : ", r.id " + dir);
}

function limitSql(view: ExplorerView): string {
  // integers checked by parseView, so they can be written into the SQL
  return " LIMIT " + Math.floor(view.limit) + " OFFSET " + Math.floor(view.offset);
}

// MariaDB and MySQL stop a slow statement with different syntax, so ask the
// server which one it is (once per server start).
let mariaDbCheck: Promise<boolean> | null = null;
function isMariaDb(): Promise<boolean> {
  mariaDbCheck ??= query<{ v: string }>("SELECT VERSION() AS v")
    .then(([row]) => /mariadb/i.test(row?.v ?? ""))
    .catch((error: unknown) => {
      mariaDbCheck = null; // ask again next time
      throw error;
    });
  return mariaDbCheck;
}

// COUNT(*) for the total, given up after COUNT_TIMEOUT_MS: the server stops
// the statement (max_statement_time on MariaDB, MAX_EXECUTION_TIME on MySQL),
// and the client-side timeout is a backstop.
async function countRows(from: string, where: SqlWhere): Promise<number | null> {
  try {
    const count = "COUNT(*) AS n FROM " + from + where.sql;
    const sql = (await isMariaDb())
      ? "SET STATEMENT max_statement_time=" + COUNT_TIMEOUT_MS / 1000 + " FOR SELECT " + count
      : "SELECT /*+ MAX_EXECUTION_TIME(" + COUNT_TIMEOUT_MS + ") */ " + count;
    const [row] = await query<{ n: number }>(sql, where.params, COUNT_TIMEOUT_MS + 1000);
    return Number(row?.n ?? 0);
  } catch {
    return null; // too slow (or failed): the page query reports real errors
  }
}

// The data keys shown as columns when the URL does not choose them (fields=*):
//   one event type   -> its catalog fields, then any other key seen in these rows;
//   several types    -> the catalog fields they all share, plus state and phase;
//   no type chosen   -> none (the raw data column is shown instead).
// Keys that repeat a table column (event, seq, wave...) are left out.
function autoFields(types: string[], events: EventRow[]): string[] {
  if (types.length === 0) {
    return [];
  }
  if (types.length === 1) {
    const fields = Object.keys(fieldsOf(types[0])).filter((key) => !EVENT_COLUMN_SET.has(key));
    const seen = new Set(fields);
    for (const event of events) {
      for (const key of Object.keys(event.data)) {
        if (!seen.has(key) && !EVENT_COLUMN_SET.has(key) && FIELD_NAME.test(key)) {
          seen.add(key);
          fields.push(key);
        }
      }
    }
    return fields;
  }
  const lists = types.map((type) => Object.keys(fieldsOf(type)));
  const shared = lists[0].filter((key) => !EVENT_COLUMN_SET.has(key) && lists.every((list) => list.includes(key)));
  return [...shared, ...Object.keys(ENVELOPE_FIELDS).filter((key) => !shared.includes(key))];
}

// One event as a flat row: table columns, then "data.<key>" columns, then
// (raw) the whole data object.
function flattenEvent(event: EventRow, fields: string[], raw: boolean): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const column of EVENT_TABLE_COLUMNS) {
    out[column] = event[column];
  }
  for (const field of fields) {
    out[DATA_PREFIX + field] = event.data[field] ?? null;
  }
  if (raw) {
    out.data = event.data;
  }
  return out;
}

function eventColumns(fields: string[], raw: boolean): string[] {
  return [...EVENT_TABLE_COLUMNS, ...fields.map((field) => DATA_PREFIX + field), ...(raw ? ["data"] : [])];
}

// ---- One page of rows (GET /api/rows) ----

export async function loadRows(params: URLSearchParams): Promise<RowsResponse> {
  const view = parseView(params);
  const started = Date.now();

  if (view.table === "requests") {
    const where = buildRequestWhere(parseRequestFilter(params), "r");
    const [rows, total] = await Promise.all([
      query(REQUEST_SELECT + where.sql + requestOrder(view) + limitSql(view), where.params, PAGE_TIMEOUT_MS),
      countRows("requests r", where),
    ]);
    return {
      table: "requests",
      columns: [...REQUEST_COLUMNS],
      fields: [],
      rows,
      total,
      offset: view.offset,
      limit: view.limit,
      ms: Date.now() - started,
    };
  }

  const filter = parseEventFilter(params);
  const where = eventWhere(filter, view);
  const order = eventOrder(view);
  const [rows, total] = await Promise.all([
    query(EVENT_SELECT + where.sql + order.sql + limitSql(view), [...where.params, ...order.params], PAGE_TIMEOUT_MS),
    countRows("events e", where),
  ]);
  const events = rows.map(toEventRow);
  const fields = view.fields === "auto" ? autoFields(filter.events, events) : view.fields;
  const raw = view.raw ?? filter.events.length === 0;
  return {
    table: "events",
    columns: eventColumns(fields, raw),
    fields,
    rows: events.map((event) => flattenEvent(event, fields, raw)),
    total,
    offset: view.offset,
    limit: view.limit,
    ms: Date.now() - started,
  };
}

// ---- One row in detail (GET /api/rows?id=N) ----

export async function loadEventDetail(id: number): Promise<EventDetailResponse | null> {
  const rows = await query(EVENT_SELECT + " WHERE e.id = ?", [id]);
  if (!rows[0]) {
    return null;
  }
  const event = toEventRow(rows[0]);
  let copies = 1;
  let firstCopyId: number | null = event.id;
  if (event.event_id) {
    const [stats] = await query<{ n: number; first: number }>(
      "SELECT COUNT(*) AS n, MIN(id) AS first FROM events WHERE event_id = ?",
      [event.event_id],
    );
    copies = Number(stats?.n ?? 1);
    firstCopyId = stats?.first === undefined || stats.first === null ? null : Number(stats.first);
  }
  return { table: "events", row: event, copies, firstCopyId };
}

function textOf(value: unknown): string | null {
  if (value === null || value === undefined) {
    return null;
  }
  return Buffer.isBuffer(value) ? value.toString("utf8") : String(value);
}

export async function loadRequestDetail(id: number): Promise<RequestDetailResponse | null> {
  const rows = await query("SELECT r.* FROM requests r WHERE r.id = ?", [id]);
  if (!rows[0]) {
    return null;
  }
  const { headers, body, ...row } = rows[0];
  const bodyText = textOf(body);
  const [stored] = await query<{ n: number }>("SELECT COUNT(*) AS n FROM events WHERE request_id = ?", [id]);
  return {
    table: "requests",
    row,
    headers: parseJson(headers),
    body: bodyText !== null && bodyText.length > BODY_SHOW_LIMIT ? bodyText.slice(0, BODY_SHOW_LIMIT) : bodyText,
    bodyTruncated: bodyText !== null && bodyText.length > BODY_SHOW_LIMIT,
    storedEvents: Number(stored?.n ?? 0),
  };
}

// ---- Every matching row as CSV (GET /api/export) ----

export type CsvExport = { fileName: string; stream: ReadableStream<Uint8Array> };

// How one table is exported: the batch query and how a row becomes cells.
type ExportSource = {
  idColumn: string;
  fetchBatch: (afterId: number | null) => Promise<Row[]>;
  columnsFor: (firstBatch: Row[]) => string[];
  cells: (row: Row, columns: string[]) => unknown[];
};

function exportSource(params: URLSearchParams, view: ExplorerView, dir: "asc" | "desc"): ExportSource {
  const order = sqlDir(dir);
  const after = dir === "asc" ? " > ?" : " < ?";

  if (view.table === "requests") {
    // detail=1 adds the query string, the headers JSON and the raw body
    const withBody = params.get("detail") === "1";
    const where = buildRequestWhere(parseRequestFilter(params), "r");
    return {
      idColumn: "id",
      fetchBatch: (afterId) => {
        const batchWhere = afterId === null ? where : andWhere(where, "r.id" + after, [afterId]);
        const select = withBody ? REQUEST_SELECT_WITH_BODY : REQUEST_SELECT;
        return query(select + batchWhere.sql + " ORDER BY r.id " + order + " LIMIT " + EXPORT_BATCH, batchWhere.params, PAGE_TIMEOUT_MS);
      },
      columnsFor: () => (withBody ? REQUEST_DETAIL_COLUMNS : [...REQUEST_COLUMNS]),
      cells: (row, columns) => columns.map((column) => (column === "body" ? textOf(row.body) : row[column])),
    };
  }

  const filter = parseEventFilter(params);
  const where = eventWhere(filter, view);
  let fields: string[] = [];
  let raw = false;
  return {
    idColumn: "id",
    fetchBatch: (afterId) => {
      const batchWhere = afterId === null ? where : andWhere(where, "e.id" + after, [afterId]);
      return query(EVENT_SELECT + batchWhere.sql + " ORDER BY e.id " + order + " LIMIT " + EXPORT_BATCH, batchWhere.params, PAGE_TIMEOUT_MS);
    },
    columnsFor: (firstBatch) => {
      fields = view.fields === "auto" ? autoFields(filter.events, firstBatch.map(toEventRow)) : view.fields;
      raw = view.raw ?? filter.events.length === 0;
      return eventColumns(fields, raw);
    },
    cells: (row, columns) => {
      const flat = flattenEvent(toEventRow(row), fields, raw);
      return columns.map((column) => flat[column]);
    },
  };
}

// Streams the whole result in id order (keyset pagination, EXPORT_BATCH rows
// per query), so even a million rows never sit in memory. The first batch is
// read before the response starts, so a database error is a normal error
// response rather than a broken download.
export async function exportCsv(params: URLSearchParams): Promise<CsvExport> {
  const view = parseView(params);
  // The view's direction when it is sorted by id; otherwise oldest first.
  const dir = view.sort === "id" ? view.dir : "asc";
  const source = exportSource(params, view, dir);
  const firstBatch = await source.fetchBatch(null);
  const columns = source.columnsFor(firstBatch);

  const encoder = new TextEncoder();
  let pending: Row[] | null = firstBatch;
  let lastId: number | null = null;

  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      // The BOM makes Excel read the file as UTF-8.
      controller.enqueue(encoder.encode("﻿" + csvLine(columns)));
    },
    async pull(controller) {
      try {
        const rows = pending ?? (await source.fetchBatch(lastId));
        pending = null;
        if (rows.length === 0) {
          controller.close();
          return;
        }
        lastId = Number(rows[rows.length - 1][source.idColumn]);
        let text = "";
        for (const row of rows) {
          text += csvLine(source.cells(row, columns));
        }
        controller.enqueue(encoder.encode(text));
        if (rows.length < EXPORT_BATCH) {
          controller.close();
        }
      } catch (error) {
        controller.error(error);
      }
    },
  });

  const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, "-");
  return { fileName: "typeblasters-" + view.table + "-" + stamp + ".csv", stream };
}
