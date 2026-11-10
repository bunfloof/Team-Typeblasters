// GET /api/rows - the Explorer's rows (read only).
//   table=events (default) | requests
//   events filters:   includeTest, event, installId, sessionId, runId, level, build,
//                     waveMin, waveMax, from, to, search, json=field|op|value (lib/filters.ts)
//                     requestId=<requests.id>, dedupe=0 (show every stored copy; default: first copy only)
//                     fields=a,b (data keys as "data.<key>" columns; * or absent = automatic), raw=1|0
//   requests filters: includeTest, clientIp, method, path, status, from, to, search
//   sort=<column or data.key> dir=asc|desc offset limit (<= 5000)
//     -> RowsResponse { table, columns, fields, rows, total (null = too many to count fast), offset, limit, ms }
//   id=<row id> -> EventDetailResponse (events) or RequestDetailResponse (requests); 404 when missing
// Errors: { error } with 400 (a data condition that cannot run), 503 (.env.local incomplete) or 500.
import { parseView } from "@/components/explorer/explorerShared";
import { errorResponse } from "@/lib/db";
import { BadFilterError, loadEventDetail, loadRequestDetail, loadRows } from "@/lib/explorerServer";

export async function GET(request: Request) {
  try {
    const params = new URL(request.url).searchParams;
    const view = parseView(params);
    if (view.id !== null) {
      const detail = view.table === "events" ? await loadEventDetail(view.id) : await loadRequestDetail(view.id);
      if (!detail) {
        return Response.json({ error: "No " + view.table + " row with id " + view.id + "." }, { status: 404 });
      }
      return Response.json(detail);
    }
    return Response.json(await loadRows(params));
  } catch (error) {
    return errorResponse(error, error instanceof BadFilterError ? 400 : 500);
  }
}
