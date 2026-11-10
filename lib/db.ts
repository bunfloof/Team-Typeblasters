// Server-side MySQL access. Import this ONLY from route handlers or other
// server code (never from a "use client" file): it reads the database
// settings from .env.local, which must never reach the browser.
import mysql from "mysql2/promise";

export type Row = Record<string, unknown>;

export class DbConfigError extends Error {}

// One pool for the whole server. In development Next.js re-runs modules on
// every edit, so the pool is kept on globalThis instead of being re-created.
const globalForDb = globalThis as unknown as { typeblastersPool?: mysql.Pool };

export function getPool(): mysql.Pool {
  if (globalForDb.typeblastersPool) {
    return globalForDb.typeblastersPool;
  }
  const missing = ["DB_HOST", "DB_NAME", "DB_USER", "DB_PASSWORD"].filter((name) => !process.env[name]);
  if (missing.length > 0) {
    throw new DbConfigError(
      "Missing in .env.local: " + missing.join(", ") + ". Fill them in and restart the dashboard (`pnpm dev`, or `pnpm start` on the server).",
    );
  }
  const pool = mysql.createPool({
    host: process.env.DB_HOST,
    port: Number(process.env.DB_PORT || 3306),
    user: process.env.DB_USER,
    password: process.env.DB_PASSWORD,
    database: process.env.DB_NAME,
    charset: "utf8mb4",
    connectionLimit: 6,
    connectTimeout: 10000,
    dateStrings: true, // DATETIME stays the exact text MySQL stores (UTC, see the log server)
    jsonStrings: true, // JSON columns arrive as text on MySQL and MariaDB alike; parseJson() reads them
    supportBigNumbers: true,
    timezone: "Z",
  });
  globalForDb.typeblastersPool = pool;
  return pool;
}

// Runs one parameterised SELECT and returns its rows. Values go in `params`
// (the ? placeholders), never pasted into the SQL text. MariaDB itself stops
// the statement after timeoutMs (max_statement_time), so a slow query never
// keeps running on a pooled connection after the page has given up on it.
export async function query<T extends Row = Row>(sql: string, params: unknown[] = [], timeoutMs = 30000): Promise<T[]> {
  const limited = "SET STATEMENT max_statement_time=" + Math.ceil(timeoutMs / 1000) + " FOR " + sql;
  const [rows] = await getPool().query({ sql: limited, timeout: timeoutMs + 5000 }, params);
  return rows as T[];
}

// A JSON column value as an object (the event's `data`, a request's `headers`).
export function parseJson(value: unknown): Record<string, unknown> {
  if (value === null || value === undefined) {
    return {};
  }
  if (typeof value === "object") {
    return value as Record<string, unknown>;
  }
  try {
    const parsed = JSON.parse(String(value));
    return parsed !== null && typeof parsed === "object" ? (parsed as Record<string, unknown>) : { value: parsed };
  } catch {
    return {};
  }
}

// Turns any thrown error into the { error } JSON a route handler returns.
export function errorResponse(error: unknown, status = 500): Response {
  const message = error instanceof Error ? error.message : String(error);
  const isConfig = error instanceof DbConfigError;
  return Response.json({ error: message, config: isConfig }, { status: isConfig ? 503 : status });
}
