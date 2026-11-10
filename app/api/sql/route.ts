// POST /api/sql   body: { "sql": "SELECT ...", "maxRows": 50000 }
// Runs ONE read-only statement (SELECT / WITH / SHOW / DESCRIBE / EXPLAIN /
// TABLE / VALUES) inside a read-only transaction that is always rolled back.
// 200 -> SqlRunResponse { columns, rows, rowCount, truncated, ms }
// 400 -> { error, readOnly: true }   refused by the read-only rule
// 503 -> { error, config: true }     .env.local is incomplete
// 500 -> { error, code, errno, sqlState }   MySQL's error, verbatim
import { runReadOnly } from "@/lib/readonly";
import { plainRows, sqlErrorResponse } from "@/lib/sqlServer";
import type { SqlErrorBody, SqlRunResponse } from "@/components/sql/types";

const DEFAULT_MAX_ROWS = 50000;
const MAX_ROWS_LIMIT = 200000;

export async function POST(request: Request) {
  let body: { sql?: unknown; maxRows?: unknown };
  try {
    body = await request.json();
  } catch {
    const error: SqlErrorBody = { error: 'Send JSON like {"sql": "SELECT 1"}.' };
    return Response.json(error, { status: 400 });
  }
  const sql = typeof body.sql === "string" ? body.sql : "";
  const requested = Math.floor(Number(body.maxRows));
  const maxRows = Number.isFinite(requested) && requested > 0 ? Math.min(requested, MAX_ROWS_LIMIT) : DEFAULT_MAX_ROWS;

  try {
    const result = await runReadOnly(sql, maxRows, 60000, request.signal);
    const response: SqlRunResponse = { ...result, rows: plainRows(result.rows) };
    return Response.json(response);
  } catch (error) {
    return sqlErrorResponse(error);
  }
}
