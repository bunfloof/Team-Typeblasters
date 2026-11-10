// GET /api/runs/[runId]?includeTest=1&level=Prototype
// Everything the run timeline page needs about ONE run: its events
// (deduplicated, seq order), the summary, the page load it belongs to
// (session_start), the HTTP request of its first batch (IP, browser), every
// IP that sent it, and the previous / next run of the same player.
//
// The run itself is always returned (it was asked for by id). The global
// filters only (1) limit prev / next to the selected level and (2) are
// reported back in `hiddenBy` when they would normally hide this run.
// Read-only: SELECT statements only.
import { errorResponse, query } from "@/lib/db";
import { dedupeEvents, loadEvents } from "@/lib/events";
import { summarizeRun } from "@/lib/runSummary";
import { levelName } from "@/lib/catalog";
import { sortRunEvents, type RunDetailResponse, type RunRequestInfo } from "@/lib/timelineData";

const REQUEST_FIELDS = [
  "id",
  "received_at",
  "client_ip",
  "peer_ip",
  "cf_connecting_ip",
  "x_real_ip",
  "x_forwarded_for",
  "cf_ipcountry",
  "user_agent",
  "origin",
  "referer",
  "accept_language",
  "sec_ch_ua",
  "sec_ch_ua_platform",
  "sec_ch_ua_mobile",
] as const;

function textOrNull(value: unknown): string | null {
  return value === null || value === undefined || value === "" ? null : String(value);
}

export async function GET(request: Request, ctx: RouteContext<"/api/runs/[runId]">) {
  try {
    const { runId } = await ctx.params;
    const search = new URL(request.url).searchParams;
    const includeTest = search.get("includeTest") === "1";
    const level = (search.get("level") || "").trim();

    const raw = await loadEvents(" WHERE e.run_id = ?", [runId], 0, "e.seq, e.id");
    if (raw.length === 0) {
      return Response.json({ error: "No events found for run " + runId + "." }, { status: 404 });
    }
    const events = sortRunEvents(dedupeEvents(raw));
    const summary = summarizeRun(events);
    const first = events[0];

    // The page load (session_start has no run_id: it is logged before Start).
    let session: RunDetailResponse["session"] = null;
    if (first.session_id) {
      const [start] = await loadEvents(" WHERE e.session_id = ? AND e.event = ?", [first.session_id, "session_start"], 1);
      if (start) {
        session = { ...start.data, received_at: start.received_at };
      }
    }

    // IP and browser: the request that carried the run's first event.
    const [requestRow] = await query("SELECT " + REQUEST_FIELDS.join(", ") + " FROM requests WHERE id = ?", [first.request_id]);
    let requestInfo: RunRequestInfo | null = null;
    if (requestRow) {
      requestInfo = { id: Number(requestRow.id) } as RunRequestInfo;
      for (const field of REQUEST_FIELDS) {
        if (field !== "id") {
          requestInfo[field] = textOrNull(requestRow[field]);
        }
      }
    }
    const ipRows = await query<{ ip: string | null }>(
      "SELECT DISTINCT r.client_ip AS ip FROM requests r JOIN events e ON e.request_id = r.id WHERE e.run_id = ? ORDER BY r.client_ip",
      [runId],
    );

    // Previous / next run of the same browser, in the order they started.
    const siblings: RunDetailResponse["siblings"] = { prev: null, next: null, position: null, total: 0 };
    if (first.install_id) {
      const params: unknown[] = [first.install_id];
      let levelSql = "";
      if (level) {
        levelSql = " AND e.level = ?";
        params.push(level);
      }
      const runs = await query<{ run_id: string; first_id: number }>(
        "SELECT e.run_id AS run_id, MIN(e.id) AS first_id FROM events e WHERE e.install_id = ? AND e.run_id IS NOT NULL AND e.run_id <> ''" +
          levelSql +
          " GROUP BY e.run_id ORDER BY first_id",
        params,
      );
      const firstId = Math.min(...raw.map((e) => e.id));
      const before = runs.filter((r) => Number(r.first_id) < firstId && r.run_id !== runId);
      const after = runs.filter((r) => Number(r.first_id) > firstId && r.run_id !== runId);
      siblings.prev = before.length > 0 ? before[before.length - 1].run_id : null;
      siblings.next = after.length > 0 ? after[0].run_id : null;
      siblings.total = runs.length;
      const index = runs.findIndex((r) => r.run_id === runId);
      siblings.position = index >= 0 ? index + 1 : null;
    }

    // Data quality: retried batches (duplicates) and missing seq numbers.
    const seqs = events.map((e) => e.seq).filter((s): s is number => s !== null);
    const seqGaps = seqs.length > 0 ? Math.max(...seqs) - Math.min(...seqs) + 1 - new Set(seqs).size : 0;

    const hiddenBy: string[] = [];
    if (summary.test && !includeTest) {
      hiddenBy.push("it is a test play (install id qa-…) and “Include test plays” is off");
    }
    if (level && summary.level !== level) {
      hiddenBy.push("it is on the " + levelName(summary.level) + " level, not " + levelName(level));
    }

    const body: RunDetailResponse = {
      summary,
      events,
      session,
      request: requestInfo,
      ips: ipRows.map((row) => row.ip).filter((ip): ip is string => !!ip),
      siblings,
      quality: {
        rawEvents: raw.length,
        duplicates: raw.length - events.length,
        seqGaps,
        batches: new Set(events.map((e) => e.request_id)).size,
      },
      hiddenBy,
    };
    return Response.json(body);
  } catch (error) {
    return errorResponse(error);
  }
}
