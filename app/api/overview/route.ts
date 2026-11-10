// GET /api/overview?includeTest=1&level=Prototype -> OverviewResponse
// (components/overview/types.ts): totals, run outcomes, events per minute
// over the last hour, the 20 most recent sessions (with IP and browser),
// event counts, and the log server's data health. Read only.
//   includeTest=1, level=...   the global filters (the data-health numbers
//                              ignore level: they are about the server)
//   from=, to=                 the top bar's time range (received_at, UTC): the
//                              totals, runs, sessions and event counts; the live
//                              numbers (last hour, last 5 minutes) and data
//                              health ignore it
import { errorResponse, query, type Row } from "@/lib/db";
import { EMPTY_EVENT_FILTER, buildEventWhere, buildRequestWhere, parseEventFilter, type SqlWhere } from "@/lib/filters";
import { levelName } from "@/lib/catalog";
import { ABANDON_AFTER_SECONDS } from "@/lib/runSummary";
import { median } from "@/components/insights/stats";
import { browserLabel } from "@/components/overview/userAgent";
import type { OverviewResponse, ProblemRequest, RecentSession } from "@/components/overview/types";

function andWhere(base: SqlWhere, clause: string): SqlWhere {
  return { sql: (base.sql ? base.sql + " AND " : " WHERE ") + clause, params: base.params };
}

function n(value: unknown): number {
  const x = Number(value);
  return Number.isFinite(x) ? x : 0;
}

function utcMillis(text: string): number {
  return Date.parse(text.replace(" ", "T") + "Z");
}

// "YYYY-MM-DD HH:MM" (UTC) of a time, as DATE_FORMAT(..., '%Y-%m-%d %H:%i') writes it.
function minuteKey(ms: number): string {
  return new Date(ms).toISOString().slice(0, 16).replace("T", " ");
}

export async function GET(request: Request) {
  try {
    const filter = parseEventFilter(new URL(request.url).searchParams);
    const where = buildEventWhere({ ...filter, events: [] });
    const healthWhere = buildEventWhere({ ...EMPTY_EVENT_FILTER, includeTest: filter.includeTest });
    const requestWhere = buildRequestWhere({ includeTest: filter.includeTest, clientIp: "", method: "", path: "", status: "", from: "", to: "", search: "" });
    const runWhere = andWhere(where, "e.run_id IS NOT NULL AND e.run_id <> ''");
    const live = buildEventWhere({ ...filter, events: [], from: "", to: "" }); // "right now" ignores the time range
    const lastHour = andWhere(live, "e.received_at >= UTC_TIMESTAMP() - INTERVAL 60 MINUTE");
    const lastFive = andWhere(live, "e.received_at >= UTC_TIMESTAMP() - INTERVAL 5 MINUTE");
    const withSession = andWhere(where, "e.session_id IS NOT NULL");

    const [nowRows, totalRows, runRows, minuteRows, activeRows, sessionRows, countRows, requestRows, statusRows, duplicateRows, testRows, gapRows, problemRows] =
      await Promise.all([
        query<{ now: string }>("SELECT UTC_TIMESTAMP(6) AS now"),
        query(
          "SELECT COUNT(*) AS row_count, COUNT(DISTINCT e.event_id) AS events, COUNT(DISTINCT e.install_id) AS players, " +
            "COUNT(DISTINCT e.session_id) AS sessions, COUNT(DISTINCT CASE WHEN e.run_id <> '' THEN e.run_id END) AS runs FROM events e" +
            where.sql,
          where.params,
        ),
        // One row per run: play seconds, last arrival, and its run_end outcome.
        query(
          "SELECT e.run_id AS run_id, MAX(e.t_play) AS seconds, MAX(e.received_at) AS last_at, " +
            "MAX(CASE WHEN e.event = 'run_end' THEN JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.outcome')) END) AS outcome " +
            "FROM events e" +
            runWhere.sql +
            " GROUP BY e.run_id",
          runWhere.params,
        ),
        // Each event counted once, in the minute it first arrived (a retried
        // batch can land again in the next minute).
        query(
          "SELECT DATE_FORMAT(f.first_at, '%Y-%m-%d %H:%i') AS bucket, COUNT(*) AS events, " +
            "COUNT(DISTINCT f.session_id) AS sessions FROM (SELECT e.event_id, MIN(e.session_id) AS session_id, " +
            "MIN(e.received_at) AS first_at FROM events e" +
            andWhere(lastHour, "e.event_id IS NOT NULL").sql +
            " GROUP BY e.event_id) f GROUP BY bucket ORDER BY bucket",
          andWhere(lastHour, "e.event_id IS NOT NULL").params,
        ),
        query("SELECT COUNT(DISTINCT e.session_id) AS sessions FROM events e" + lastFive.sql, lastFive.params),
        query(
          "SELECT e.session_id AS session_id, MIN(e.install_id) AS install_id, MIN(e.received_at) AS first_seen, " +
            "MAX(e.received_at) AS last_seen, COUNT(DISTINCT CASE WHEN e.run_id <> '' THEN e.run_id END) AS runs, " +
            "COUNT(DISTINCT e.event_id) AS events, MAX(e.request_id) AS last_request_id, " +
            "GROUP_CONCAT(DISTINCT e.level ORDER BY e.level SEPARATOR ',') AS levels, MAX(e.build) AS build, MAX(e.wave) AS best_wave, " +
            "COUNT(DISTINCT CASE WHEN e.event = 'run_end' AND JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.outcome')) = 'won' THEN e.run_id END) AS won, " +
            "COUNT(DISTINCT CASE WHEN e.event = 'run_end' AND JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.outcome')) = 'lost' THEN e.run_id END) AS lost " +
            "FROM events e" +
            withSession.sql +
            " GROUP BY e.session_id ORDER BY last_seen DESC LIMIT 20",
          withSession.params,
        ),
        query(
          "SELECT e.event AS event, COUNT(*) AS row_count, COUNT(DISTINCT e.event_id) AS events FROM events e" +
            where.sql +
            " GROUP BY e.event ORDER BY events DESC",
          where.params,
        ),
        // ---- data health (server side; the level filter does not apply) ----
        query(
          "SELECT COUNT(*) AS requests, " +
            "SUM(CASE WHEN r.parse_ok IS NOT NULL THEN 1 ELSE 0 END) AS batches, " +
            "SUM(CASE WHEN r.parse_ok = 0 THEN 1 ELSE 0 END) AS parse_failed, " +
            "SUM(CASE WHEN r.status <> 200 THEN 1 ELSE 0 END) AS non200, " +
            "SUM(CASE WHEN r.cf_ray IS NULL OR r.cf_ray = '' THEN 1 ELSE 0 END) AS no_cf_ray, " +
            "SUM(CASE WHEN (r.cf_ray IS NULL OR r.cf_ray = '') AND r.parse_ok IS NOT NULL THEN 1 ELSE 0 END) AS batches_no_cf_ray " +
            "FROM requests r" +
            requestWhere.sql,
          requestWhere.params,
        ),
        query(
          "SELECT r.status AS status, r.method AS method, COUNT(*) AS requests FROM requests r" +
            requestWhere.sql +
            " GROUP BY r.status, r.method ORDER BY requests DESC",
          requestWhere.params,
        ),
        query(
          "SELECT COUNT(*) AS ids, COALESCE(SUM(d.copies - 1), 0) AS extra_rows FROM " +
            "(SELECT e.event_id, COUNT(*) AS copies FROM events e" +
            andWhere(healthWhere, "e.event_id IS NOT NULL").sql +
            " GROUP BY e.event_id HAVING COUNT(*) > 1) d",
          healthWhere.params,
        ),
        query("SELECT COUNT(*) AS all_events, SUM(CASE WHEN install_id LIKE 'qa-%' THEN 1 ELSE 0 END) AS test_events FROM events"),
        // seq is 1, 2, 3... per session: numbers missing below the highest one are lost events.
        query(
          "SELECT COUNT(*) AS sessions, COALESCE(SUM(s.missing), 0) AS missing, SUM(CASE WHEN s.missing > 0 THEN 1 ELSE 0 END) AS with_gaps FROM " +
            "(SELECT e.session_id, GREATEST(0, MAX(e.seq) - COUNT(DISTINCT e.seq)) AS missing FROM events e" +
            andWhere(healthWhere, "e.session_id IS NOT NULL AND e.seq IS NOT NULL").sql +
            " GROUP BY e.session_id) s",
          healthWhere.params,
        ),
        query(
          "SELECT r.id, r.received_at, r.status, r.method, LEFT(r.path, 200) AS path, r.client_ip, r.parse_error, r.user_agent FROM requests r" +
            andWhere(requestWhere, "(r.status >= 400 OR r.parse_ok = 0)").sql +
            " ORDER BY r.id DESC LIMIT 25",
          requestWhere.params,
        ),
      ]);

    // The IP and browser of each recent session: from its latest request.
    const requestIds = sessionRows.map((row) => n(row.last_request_id)).filter((id) => id > 0);
    const requestInfo = new Map<number, Row>();
    if (requestIds.length > 0) {
      const rows = await query(
        "SELECT r.id, r.client_ip, r.cf_ipcountry, r.user_agent FROM requests r WHERE r.id IN (" + requestIds.map(() => "?").join(",") + ")",
        requestIds,
      );
      for (const row of rows) requestInfo.set(n(row.id), row);
    }

    const now = String(nowRows[0]?.now ?? "");
    const nowMs = utcMillis(now);

    // Run outcomes; a run without run_end is abandoned unless its last event is recent.
    const outcomes = { won: 0, lost: 0, restart: 0, abandoned: 0, playing: 0 };
    const seconds: number[] = [];
    for (const row of runRows) {
      let outcome = String(row.outcome ?? "");
      if (outcome !== "won" && outcome !== "lost" && outcome !== "restart") {
        const age = nowMs - utcMillis(String(row.last_at ?? ""));
        outcome = Number.isFinite(age) && age < ABANDON_AFTER_SECONDS * 1000 ? "playing" : "abandoned";
      }
      outcomes[outcome as keyof typeof outcomes] += 1;
      if (outcome !== "playing" && row.seconds !== null) seconds.push(n(row.seconds));
    }

    // Every minute of the last hour (empty minutes as 0), oldest first.
    const perMinute = new Map(minuteRows.map((row) => [String(row.bucket), row]));
    const lastMinute = Number.isFinite(nowMs) ? Math.floor(nowMs / 60000) * 60000 : 0;
    const minutes = Array.from({ length: 60 }, (_, i) => {
      const key = minuteKey(lastMinute - (59 - i) * 60000);
      const row = perMinute.get(key);
      return { minute: key, events: n(row?.events), sessions: n(row?.sessions) };
    });

    const recentSessions: RecentSession[] = sessionRows.map((row) => {
      const info = requestInfo.get(n(row.last_request_id));
      return {
        session_id: String(row.session_id),
        install_id: (row.install_id as string | null) ?? null,
        client_ip: (info?.client_ip as string | null) ?? null,
        country: (info?.cf_ipcountry as string | null) ?? null,
        browser: browserLabel(info?.user_agent as string | null),
        user_agent: (info?.user_agent as string | null) ?? null,
        levels: String(row.levels ?? "")
          .split(",")
          .filter(Boolean)
          .map(levelName)
          .join(", "),
        build: (row.build as string | null) ?? null,
        runs: n(row.runs),
        won: n(row.won),
        lost: n(row.lost),
        best_wave: n(row.best_wave),
        events: n(row.events),
        first_seen: String(row.first_seen ?? ""),
        last_seen: String(row.last_seen ?? ""),
      };
    });

    const totals = totalRows[0] ?? {};
    const req = requestRows[0] ?? {};
    const body: OverviewResponse = {
      now,
      totals: { players: n(totals.players), sessions: n(totals.sessions), runs: n(totals.runs), events: n(totals.events), rows: n(totals.row_count) },
      outcomes,
      medianRunSeconds: median(seconds),
      medianRunSecondsN: seconds.length,
      live: {
        minutes,
        activeSessions: n(activeRows[0]?.sessions),
        eventsLastHour: minutes.reduce((sum, m) => sum + m.events, 0),
      },
      recentSessions,
      eventCounts: countRows.map((row) => ({ event: String(row.event ?? "(none)"), events: n(row.events), rows: n(row.row_count) })),
      health: {
        requests: n(req.requests),
        batches: n(req.batches),
        parseFailed: n(req.parse_failed),
        non200: n(req.non200),
        statuses: statusRows.map((row) => ({ status: n(row.status), method: String(row.method ?? ""), requests: n(row.requests) })),
        withoutCfRay: n(req.no_cf_ray),
        batchesWithoutCfRay: n(req.batches_no_cf_ray),
        duplicatedEventIds: n(duplicateRows[0]?.ids),
        duplicateRows: n(duplicateRows[0]?.extra_rows),
        missingSeq: n(gapRows[0]?.missing),
        sessionsWithGaps: n(gapRows[0]?.with_gaps),
        sessions: n(gapRows[0]?.sessions),
        testEvents: n(testRows[0]?.test_events),
        allEvents: n(testRows[0]?.all_events),
        problems: problemRows.map(
          (row): ProblemRequest => ({
            id: n(row.id),
            received_at: String(row.received_at ?? ""),
            status: n(row.status),
            method: String(row.method ?? ""),
            path: String(row.path ?? ""),
            client_ip: (row.client_ip as string | null) ?? null,
            parse_error: (row.parse_error as string | null) ?? null,
            user_agent: (row.user_agent as string | null) ?? null,
          }),
        ),
      },
    };
    return Response.json(body);
  } catch (error) {
    return errorResponse(error);
  }
}
