// GET /api/meta?includeTest=1&level=Prototype
// What is in the database: totals, event counts, levels, builds, the time
// range, and which data fields each event type uses (from a sample).
import { errorResponse, parseJson, query } from "@/lib/db";
import { buildEventWhere, buildRequestWhere, parseEventFilter, parseRequestFilter } from "@/lib/filters";
import type { MetaResponse } from "@/lib/types";

export async function GET(request: Request) {
  try {
    const filter = parseEventFilter(new URL(request.url).searchParams);
    const where = buildEventWhere({ ...filter, events: [] });

    const [version] = await query<{ v: string }>("SELECT VERSION() AS v");
    const counts = await query<{ event: string; count: number }>(
      "SELECT e.event AS event, COUNT(*) AS count FROM events e" + where.sql + " GROUP BY e.event ORDER BY count DESC",
      where.params,
    );
    const [totals] = await query<Record<string, number>>(
      "SELECT COUNT(*) AS events, COUNT(DISTINCT e.install_id) AS installs, COUNT(DISTINCT e.session_id) AS sessions, " +
        "COUNT(DISTINCT e.run_id) AS runs, MIN(e.received_at) AS firstReceived, MAX(e.received_at) AS lastReceived FROM events e" +
        where.sql,
      where.params,
    );
    const [testCount] = await query<{ n: number }>("SELECT COUNT(*) AS n FROM events WHERE install_id LIKE 'qa-%'");
    const requestWhere = buildRequestWhere(parseRequestFilter(new URL(request.url).searchParams));
    const [requestCount] = await query<{ n: number }>("SELECT COUNT(*) AS n FROM requests r" + requestWhere.sql, requestWhere.params);
    const levels = await query<{ level: string }>("SELECT DISTINCT level FROM events WHERE level IS NOT NULL ORDER BY level");
    const builds = await query<{ build: string }>("SELECT DISTINCT build FROM events WHERE build IS NOT NULL ORDER BY build");

    // Field names per event type, from up to 200 recent rows of each.
    const dataFields: Record<string, string[]> = {};
    for (const { event } of counts) {
      const sample = await query<{ data: unknown }>("SELECT data FROM events WHERE event = ? ORDER BY id DESC LIMIT 200", [event]);
      const keys = new Set<string>();
      for (const row of sample) {
        for (const key of Object.keys(parseJson(row.data))) {
          keys.add(key);
        }
      }
      dataFields[event] = [...keys];
    }

    const body: MetaResponse = {
      dbVersion: version?.v ?? "",
      eventCounts: counts.map((row) => ({ event: row.event, count: Number(row.count) })),
      totals: {
        events: Number(totals?.events ?? 0),
        testEvents: Number(testCount?.n ?? 0),
        requests: Number(requestCount?.n ?? 0),
        installs: Number(totals?.installs ?? 0),
        sessions: Number(totals?.sessions ?? 0),
        runs: Number(totals?.runs ?? 0),
      },
      levels: levels.map((row) => row.level),
      builds: builds.map((row) => row.build),
      firstReceived: (totals?.firstReceived as unknown as string) ?? null,
      lastReceived: (totals?.lastReceived as unknown as string) ?? null,
      dataFields,
    };
    return Response.json(body);
  } catch (error) {
    return errorResponse(error);
  }
}
