// GET /api/export - every row matching the Explorer's filters, streamed as a
// CSV download (UTF-8 with BOM). Same parameters as /api/rows (offset and
// limit are ignored); rows come in id order. Requests: detail=1 adds the
// headers and body columns.
import { errorResponse } from "@/lib/db";
import { BadFilterError, exportCsv } from "@/lib/explorerServer";

export async function GET(request: Request) {
  try {
    const { fileName, stream } = await exportCsv(new URL(request.url).searchParams);
    return new Response(stream, {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": 'attachment; filename="' + fileName + '"',
        "Cache-Control": "no-store",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch (error) {
    return errorResponse(error, error instanceof BadFilterError ? 400 : 500);
  }
}
