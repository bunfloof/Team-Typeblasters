// POST /api/sql/export   body: { "sql": "SELECT ..." }
// Re-checks the read-only rule, then streams EVERY row of the result as a
// CSV download (UTF-8 BOM, header row; no row limit). Errors before the first
// row answer JSON like POST /api/sql; an error mid-way breaks the download.
import { sqlErrorResponse, streamCsvExport } from "@/lib/sqlServer";
import type { SqlErrorBody } from "@/components/sql/types";

// "typeblasters-sql-2026-10-12_20-15-03.csv" (UTC)
function fileName(): string {
  const stamp = new Date().toISOString().slice(0, 19).replace("T", "_").replace(/:/g, "-");
  return "typeblasters-sql-" + stamp + ".csv";
}

export async function POST(request: Request) {
  let body: { sql?: unknown };
  try {
    body = await request.json();
  } catch {
    const error: SqlErrorBody = { error: 'Send JSON like {"sql": "SELECT 1"}.' };
    return Response.json(error, { status: 400 });
  }
  const sql = typeof body.sql === "string" ? body.sql : "";

  try {
    const csv = await streamCsvExport(sql, request.signal);
    return new Response(csv, {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": 'attachment; filename="' + fileName() + '"',
        "Cache-Control": "no-store",
      },
    });
  } catch (error) {
    return sqlErrorResponse(error);
  }
}
