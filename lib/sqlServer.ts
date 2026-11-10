// Server side of the SQL console (/sql): the schema listing, the error
// answers, and the streamed "full result as CSV" export. Server only: never
// import this from a "use client" file.
//
// Every statement goes through checkReadOnly() and runs inside
// START TRANSACTION READ ONLY, which is always rolled back (lib/readonly.ts).
import type { FieldPacket, PoolConnection as CorePoolConnection } from "mysql2";
import { DbConfigError, errorResponse, query } from "./db";
import { ReadOnlyError, checkReadOnly, readOnlyConnection, releaseReadOnly } from "./readonly";
import { csvLine } from "./csv";
import type { SchemaResponse, SchemaTable, SqlErrorBody } from "@/components/sql/types";

// ---------------------------------------------------------------------------
// Errors
// ---------------------------------------------------------------------------

type MysqlError = { message: string; code?: unknown; errno?: unknown; sqlState?: unknown };

function isMysqlError(error: unknown): error is MysqlError {
  return error instanceof Error && typeof (error as MysqlError).code === "string";
}

// 400 for the read-only rule, 503 when .env.local is incomplete, 500 for the
// rest (MySQL's own message, verbatim, plus its error code).
export function sqlErrorResponse(error: unknown): Response {
  if (error instanceof ReadOnlyError) {
    const body: SqlErrorBody = { error: error.message, readOnly: true };
    return Response.json(body, { status: 400 });
  }
  if (error instanceof DbConfigError || !isMysqlError(error)) {
    return errorResponse(error);
  }
  const body: SqlErrorBody = {
    error: error.message,
    code: String(error.code),
    errno: typeof error.errno === "number" ? error.errno : undefined,
    sqlState: typeof error.sqlState === "string" ? error.sqlState : undefined,
  };
  return Response.json(body, { status: 500 });
}

// ---------------------------------------------------------------------------
// Values
// ---------------------------------------------------------------------------

// Binary values (BINARY/BLOB columns, UNHEX(), ...) arrive as Buffers; show
// them as text instead of {"type":"Buffer","data":[...]}.
function plainValue(value: unknown): unknown {
  return Buffer.isBuffer(value) ? value.toString("utf8") : value;
}

export function plainRows(rows: Record<string, unknown>[]): Record<string, unknown>[] {
  return rows.map((row) => {
    let copy: Record<string, unknown> | null = null;
    for (const [name, value] of Object.entries(row)) {
      if (Buffer.isBuffer(value)) {
        copy = copy || { ...row };
        copy[name] = plainValue(value);
      }
    }
    return copy || row;
  });
}

// ---------------------------------------------------------------------------
// Schema (GET /api/sql/schema)
// ---------------------------------------------------------------------------

// MySQL 8 returns information_schema column names in upper case and MariaDB in
// the case written, so every column below has a lower-case alias.
export async function loadSchema(): Promise<SchemaResponse> {
  const [info] = await query<{ db: string; version: string }>("SELECT DATABASE() AS db, VERSION() AS version");
  const tables = await query<{ name: string; kind: string }>(
    "SELECT TABLE_NAME AS name, TABLE_TYPE AS kind FROM information_schema.TABLES " +
      "WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME",
  );
  const columns = await query<{
    table_name: string;
    name: string;
    type: string;
    nullable: string;
    column_key: string;
    comment: string;
  }>(
    "SELECT TABLE_NAME AS table_name, COLUMN_NAME AS name, COLUMN_TYPE AS type, IS_NULLABLE AS nullable, " +
      "COLUMN_KEY AS column_key, COLUMN_COMMENT AS comment FROM information_schema.COLUMNS " +
      "WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME, ORDINAL_POSITION",
  );
  const indexRows = await query<{ table_name: string; index_name: string; non_unique: number; seq: number; column_name: string }>(
    "SELECT TABLE_NAME AS table_name, INDEX_NAME AS index_name, NON_UNIQUE AS non_unique, " +
      "SEQ_IN_INDEX AS seq, COLUMN_NAME AS column_name FROM information_schema.STATISTICS " +
      "WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX",
  );

  const result: SchemaTable[] = tables.map((table) => {
    const indexes = new Map<string, { name: string; unique: boolean; columns: string[] }>();
    for (const row of indexRows) {
      if (row.table_name !== table.name) continue;
      const index = indexes.get(row.index_name) || { name: row.index_name, unique: Number(row.non_unique) === 0, columns: [] };
      index.columns.push(row.column_name);
      indexes.set(row.index_name, index);
    }
    const leading = new Set([...indexes.values()].map((index) => index.columns[0]));
    return {
      name: table.name,
      kind: table.kind,
      rows: null,
      columns: columns
        .filter((column) => column.table_name === table.name)
        .map((column) => ({
          name: column.name,
          type: String(column.type),
          nullable: column.nullable === "YES",
          key: column.column_key || "",
          indexed: leading.has(column.name),
          comment: column.comment || "",
        })),
      indexes: [...indexes.values()],
    };
  });

  // Exact row counts (the information_schema figure is only an estimate).
  // Table names come from information_schema and are quoted as identifiers.
  await Promise.all(
    result
      .filter((table) => table.kind === "BASE TABLE")
      .map(async (table) => {
        try {
          const [row] = await query<{ n: number }>("SELECT COUNT(*) AS n FROM `" + table.name.replace(/`/g, "``") + "`", [], 10000);
          table.rows = Number(row?.n ?? 0);
        } catch {
          table.rows = null;
        }
      }),
  );

  return { database: info?.db ?? "", version: info?.version ?? "", tables: result };
}

// ---------------------------------------------------------------------------
// Streamed CSV export (POST /api/sql/export)
// ---------------------------------------------------------------------------

const EXPORT_LIMIT_MS = 10 * 60 * 1000; // a runaway export is stopped after 10 minutes
const ROWS_PER_CHUNK = 500;
const BOM = String.fromCharCode(0xfeff); // byte order mark: makes Excel open the file as UTF-8

// Runs a read-only statement and returns its whole result as a CSV byte
// stream (UTF-8 BOM, header row, then every row). Rows are read from MySQL
// only as fast as the browser downloads them. A statement MySQL rejects
// before the first row throws here, so the route can answer with JSON.
// If the browser goes away (or 10 minutes pass) the connection is closed
// at once, which makes MySQL stop the query and roll back.
export async function streamCsvExport(sql: string, signal: AbortSignal): Promise<ReadableStream<Uint8Array>> {
  const statement = checkReadOnly(sql);
  const pooled = await readOnlyConnection(EXPORT_LIMIT_MS);

  // The promise wrapper's typings call this the promise Connection, but it is
  // the callback-style connection, whose query() can stream.
  const core = pooled.connection as unknown as CorePoolConnection & { stream?: { destroy(): void } };
  // rowsAsArray keeps two columns with the same name (SELECT e.id, r.id) apart.
  const rows = core.query({ sql: statement, rowsAsArray: true }).stream({ highWaterMark: ROWS_PER_CHUNK });

  let finished = false;
  let streamError: Error | null = null;
  let output: ReadableStreamDefaultController<Uint8Array> | null = null;
  // Always listening, so an error between two reads never goes unhandled.
  rows.on("error", (error: Error) => {
    streamError = streamError || error;
  });

  // completed: end the transaction and give the connection back to the pool.
  // Otherwise (stopped mid-result) close the socket itself: mysql2's own
  // destroy() only half-closes, and MySQL would keep sending the rest.
  const finish = (completed: boolean) => {
    if (finished) return;
    finished = true;
    clearTimeout(timer);
    signal.removeEventListener("abort", onAbort);
    if (completed) {
      releaseReadOnly(pooled);
    } else {
      pooled.destroy(); // leaves the pool, and marks the close as intended
      core.stream?.destroy();
      rows.destroy();
    }
  };
  // Cancel, client gone, or too slow: stop and fail the download.
  const stop = (reason: Error) => {
    streamError = streamError || reason;
    finish(false);
    output?.error(reason);
  };
  const onAbort = () => stop(new Error("Export cancelled."));
  signal.addEventListener("abort", onAbort);
  const timer = setTimeout(() => stop(new Error("The export took longer than 10 minutes and was stopped.")), EXPORT_LIMIT_MS);
  if (signal.aborted) onAbort();

  // Wait for the column list (or MySQL's error) before answering.
  let header: string[];
  try {
    header = await new Promise<string[]>((resolve, reject) => {
      rows.once("fields", (fields: FieldPacket[] | undefined) => resolve((fields || []).map((field) => field.name)));
      rows.once("error", reject);
      rows.once("close", () => reject(streamError || new Error("The query ended before returning any columns.")));
    });
  } catch (error) {
    // MySQL refused the statement (syntax error, unknown column...): the
    // connection is fine and goes back to the pool. Otherwise it is closed.
    finish(isMysqlError(error) && (error as { fatal?: boolean }).fatal !== true);
    throw error;
  }

  const encoder = new TextEncoder();
  const iterator = rows[Symbol.asyncIterator]();
  return new ReadableStream<Uint8Array>({
    start(controller) {
      output = controller;
      controller.enqueue(encoder.encode(BOM + csvLine(header)));
    },
    async pull(controller) {
      try {
        let text = "";
        for (let i = 0; i < ROWS_PER_CHUNK; i++) {
          const next = await iterator.next();
          if (finished) {
            return; // stopped while waiting for the row
          }
          if (next.done) {
            if (streamError) throw streamError;
            if (text) controller.enqueue(encoder.encode(text));
            finish(true);
            controller.close();
            return;
          }
          // Statements without a result set yield an OK packet, not a row.
          if (Array.isArray(next.value)) {
            text += csvLine((next.value as unknown[]).map(plainValue));
          }
        }
        controller.enqueue(encoder.encode(text));
      } catch (error) {
        finish(false);
        controller.error(error);
      }
    },
    cancel() {
      finish(false);
    },
  });
}
