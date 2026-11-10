// The SQL page's "read anything, change nothing" rule (server side).
// Not a security measure (the dashboard has none, on purpose): it stops a
// teammate from changing or deleting exam data by accident.
//   1. one statement only, starting with SELECT, WITH, SHOW, DESCRIBE/DESC,
//      EXPLAIN, TABLE or VALUES (so no DROP, ALTER, TRUNCATE...), and no
//      SELECT ... INTO (files, variables);
//   2. no MariaDB "executable comments" (/*! ... */, which the server runs as
//      SQL) and no locking reads (FOR UPDATE, LOCK IN SHARE MODE, GET_LOCK),
//      which would make the log server wait to insert new events;
//   3. it runs inside START TRANSACTION READ ONLY, which MySQL and MariaDB
//      use to refuse any change to a table, and is always rolled back;
//   4. the server stops it after max_statement_time seconds.
import type { FieldPacket, PoolConnection as CorePoolConnection } from "mysql2";
import { getPool } from "./db";

export class ReadOnlyError extends Error {}

// (A WITH ... UPDATE/DELETE, or EXPLAIN ANALYZE of one, passes these checks
// but is refused by the READ ONLY transaction.)
const ALLOWED_FIRST_WORDS = ["SELECT", "WITH", "SHOW", "DESCRIBE", "DESC", "EXPLAIN", "TABLE", "VALUES"];

// The statement with comments and string literals blanked out, so keyword
// checks only look at real SQL.
function stripCommentsAndStrings(sql: string): string {
  let out = "";
  let i = 0;
  while (i < sql.length) {
    const c = sql[i];
    const next = sql[i + 1];
    // "--" starts a comment only when a space or the end of the line follows
    // (as on the server): "1--1" is one minus minus one.
    if (c === "-" && next === "-" && (i + 2 >= sql.length || /[\s\x00-\x1f]/.test(sql[i + 2]))) {
      while (i < sql.length && sql[i] !== "\n") i++;
      out += " ";
    } else if (c === "#") {
      while (i < sql.length && sql[i] !== "\n") i++;
      out += " ";
    } else if (c === "/" && next === "*") {
      if (sql[i + 2] === "!" || sql.slice(i + 2, i + 4) === "M!") {
        throw new ReadOnlyError("Executable comments (/*! ... */) are not allowed: the server would run them as SQL.");
      }
      i += 2;
      while (i < sql.length && !(sql[i] === "*" && sql[i + 1] === "/")) i++;
      i += 2;
      out += " ";
    } else if (c === "'" || c === '"' || c === "`") {
      const quote = c;
      i++;
      while (i < sql.length) {
        // (inside `backticks` only a doubled backtick escapes)
        if (sql[i] === "\\" && quote !== "`") {
          i += 2;
          continue;
        }
        if (sql[i] === quote) {
          if (sql[i + 1] === quote) {
            i += 2;
            continue;
          }
          break;
        }
        i++;
      }
      i++;
      out += quote === "`" ? " x " : " '' ";
    } else {
      out += c;
      i++;
    }
  }
  return out;
}

// Returns the statement to run (trailing semicolons removed) or throws ReadOnlyError.
export function checkReadOnly(sql: string): string {
  const trimmed = sql.trim().replace(/;+\s*$/, "");
  // Semicolons at the end are fine, even with a comment after them ("...; -- note").
  const bare = stripCommentsAndStrings(trimmed).trim().replace(/[;\s]+$/, "");
  if (!bare) {
    throw new ReadOnlyError("Write a query first.");
  }
  if (bare.includes(";")) {
    throw new ReadOnlyError("Only one statement at a time.");
  }
  const firstWord = (bare.match(/^\(*\s*([A-Za-z]+)/)?.[1] || "").toUpperCase();
  if (!ALLOWED_FIRST_WORDS.includes(firstWord)) {
    throw new ReadOnlyError("Read-only: start with SELECT, WITH, SHOW, DESCRIBE, EXPLAIN, TABLE or VALUES.");
  }
  if (/\bINTO\b/i.test(bare)) {
    throw new ReadOnlyError("Read-only: SELECT ... INTO is not allowed.");
  }
  if (/\bLOCK\s+IN\s+SHARE\s+MODE\b|\bFOR\s+(UPDATE|SHARE)\b|\bGET_LOCK\s*\(/i.test(bare)) {
    throw new ReadOnlyError("Read-only: locking reads (FOR UPDATE, LOCK IN SHARE MODE, GET_LOCK) are not allowed.");
  }
  return trimmed;
}

export type SqlResult = {
  columns: { name: string; type: number | null }[];
  rows: Record<string, unknown>[];
  rowCount: number; // rows returned (truncated: there were more)
  truncated: boolean;
  ms: number;
};

// Column names made unique: SELECT e.id, r.id gives "id" and "id (2)".
function uniqueNames(fields: FieldPacket[]): string[] {
  const seen = new Map<string, number>();
  return fields.map((field) => {
    const count = (seen.get(field.name) || 0) + 1;
    seen.set(field.name, count);
    return count === 1 ? field.name : field.name + " (" + count + ")";
  });
}

// Binary values (BLOB, UNHEX()...) arrive as Buffers: show them as text.
function plain(value: unknown): unknown {
  return Buffer.isBuffer(value) ? value.toString("utf8") : value;
}

type Pooled = Awaited<ReturnType<ReturnType<typeof getPool>["getConnection"]>>;

// A pooled connection inside START TRANSACTION READ ONLY, on which the server
// stops any statement that runs longer than limitMs.
export async function readOnlyConnection(limitMs: number): Promise<Pooled> {
  const pooled = await getPool().getConnection();
  try {
    await pooled.query("SET SESSION max_statement_time = ?", [Math.ceil(limitMs / 1000)]);
    await pooled.query("START TRANSACTION READ ONLY");
  } catch (error) {
    pooled.destroy();
    throw error;
  }
  return pooled;
}

// Ends the read-only transaction and gives the connection back to the pool.
export function releaseReadOnly(pooled: Pooled): void {
  pooled
    .query("ROLLBACK")
    .then(() => pooled.query("SET SESSION max_statement_time = DEFAULT"))
    .then(
      () => pooled.release(),
      () => pooled.destroy(),
    );
}

// Runs a read-only statement and returns at most maxRows rows. Rows are read
// one by one; once there are more than maxRows the query is stopped on the
// server (the connection is closed), so a huge result never fills memory.
// signal (the browser's request): Stop, or a newer Run, closes the connection
// at once, so an abandoned query never keeps one of the pool's connections.
export async function runReadOnly(sql: string, maxRows = 50000, timeoutMs = 60000, signal?: AbortSignal): Promise<SqlResult> {
  const statement = checkReadOnly(sql);
  const pooled = await readOnlyConnection(timeoutMs);
  const started = Date.now();
  if (signal?.aborted) {
    releaseReadOnly(pooled);
    throw new Error("Cancelled.");
  }

  // The promise pool's connection wraps a callback-style connection whose
  // query() reports rows one at a time.
  const core = pooled.connection as unknown as CorePoolConnection & { stream?: { destroy(): void } };
  return new Promise<SqlResult>((resolve, reject) => {
    let fields: FieldPacket[] = [];
    const arrays: unknown[][] = [];
    let truncated = false;
    let settled = false;

    // completed: end the transaction and give the connection back. Stopped
    // early (too many rows, error): close the socket, so the server stops.
    const finish = (completed: boolean) => {
      signal?.removeEventListener("abort", onAbort);
      if (completed) {
        releaseReadOnly(pooled);
      } else {
        pooled.destroy();
        core.stream?.destroy();
      }
    };
    const onAbort = () => {
      if (settled) {
        return;
      }
      settled = true;
      finish(false);
      reject(new Error("Cancelled."));
    };
    signal?.addEventListener("abort", onAbort, { once: true });
    const answer = () => {
      const names = uniqueNames(fields);
      resolve({
        columns: fields.map((field, i) => ({ name: names[i], type: field.columnType ?? null })),
        rows: arrays.map((values) => {
          const row: Record<string, unknown> = {};
          names.forEach((name, i) => {
            row[name] = plain(values[i]);
          });
          return row;
        }),
        rowCount: arrays.length,
        truncated,
        ms: Date.now() - started,
      });
    };

    // The server's own limit (max_statement_time) normally ends it first and
    // keeps the connection usable; this one covers a server that went quiet.
    const running = core.query({ sql: statement, rowsAsArray: true, timeout: timeoutMs + 5000 });
    running.on("fields", (received: FieldPacket[]) => {
      fields = received || [];
    });
    running.on("result", (row: unknown) => {
      if (settled || !Array.isArray(row)) {
        return;
      }
      if (arrays.length < maxRows) {
        arrays.push(row);
        return;
      }
      truncated = true;
      settled = true;
      finish(false);
      answer();
    });
    running.on("error", (error: Error) => {
      if (settled) {
        return; // the error caused by closing the connection on purpose
      }
      settled = true;
      // MySQL refused or stopped the statement (syntax error, time limit...):
      // the connection is fine and goes back to the pool. Otherwise close it.
      const mysqlError = error as Error & { sqlState?: string; fatal?: boolean };
      finish(mysqlError.sqlState !== undefined && mysqlError.fatal !== true);
      reject(error);
    });
    running.on("end", () => {
      if (settled) {
        return;
      }
      settled = true;
      finish(true);
      answer();
    });
  });
}
