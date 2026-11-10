// Runs and players summed up from the events table (server side only: it
// imports db.ts). Used by app/api/runs/route.ts and app/api/players/route.ts.
//
// Instead of loading every event of every run, a run is summed up from:
//   1. one grouped query (wave reached, play seconds, event count, times...),
//   2. its key events: run_start, run_end and the fatal damage,
//   3. for runs without a run_end, the score of its last player-state event.
// summarizeFromParts() then applies the same rules as summarizeRun() in
// lib/runSummary.ts (which needs all the events of a run).
import { query, type Row } from "./db";
import { dedupeEvents, loadEvents, num, str } from "./events";
import { EVENTS } from "./catalog";
import { buildEventWhere, EMPTY_EVENT_FILTER, type EventFilter, type SqlWhere } from "./filters";
import { ABANDON_AFTER_SECONDS, groupByRun, type RunOutcome, type RunSummary } from "./runSummary";
import type { EventRow } from "./types";
import type { PlayerRow, PlayersResponse, RunNumberFilter, RunRow } from "../components/runs/types";

// ---- Small helpers ----

function toNumber(value: unknown): number | null {
  if (value === null || value === undefined || value === "") {
    return null;
  }
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

function toText(value: unknown): string | null {
  return value === null || value === undefined || value === "" ? null : String(value);
}

function utcMillis(text: string | null): number | null {
  if (!text) {
    return null;
  }
  const ms = Date.parse(text.replace(" ", "T") + "Z");
  return Number.isNaN(ms) ? null : ms;
}

// " WHERE a AND b" with more conditions added (the where may be empty).
function andWhere(where: SqlWhere, ...conditions: string[]): string {
  const parts = where.sql ? [where.sql.replace(/^ WHERE /, "")] : [];
  parts.push(...conditions);
  return parts.length > 0 ? " WHERE " + parts.join(" AND ") : "";
}

function placeholders(values: unknown[]): string {
  return values.map(() => "?").join(",");
}

// Runs a query once per chunk of values (keeps IN (...) lists short).
async function inChunks<T, R>(values: T[], run: (chunk: T[]) => Promise<R[]>, size = 500): Promise<R[]> {
  const out: R[] = [];
  for (let start = 0; start < values.length; start += size) {
    out.push(...(await run(values.slice(start, start + size))));
  }
  return out;
}

// ---- Which runs ----

// The events of the runs that match `filter`. Filters that are the same for
// every event of a run (test install, install, session, run, level, build)
// apply to the events directly; the others (event type, wave, time, text,
// data conditions) pick the runs that have at least one matching event, and
// the run is then summed up from ALL its events.
export function buildRunWhere(filter: EventFilter): SqlWhere {
  const direct = buildEventWhere(
    {
      ...EMPTY_EVENT_FILTER,
      includeTest: filter.includeTest,
      installId: filter.installId,
      sessionId: filter.sessionId,
      runId: filter.runId,
      level: filter.level,
      build: filter.build,
    },
    "e",
  );
  const conditions = ["e.run_id IS NOT NULL", "e.run_id <> ''"];
  const params = [...direct.params];

  const perEvent =
    filter.events.length > 0 ||
    filter.waveMin !== null ||
    filter.waveMax !== null ||
    !!filter.from ||
    !!filter.to ||
    !!filter.search ||
    filter.json.length > 0;
  if (perEvent) {
    const matching = buildEventWhere(
      {
        ...EMPTY_EVENT_FILTER,
        includeTest: true, // the test filter is already on the outer query
        events: filter.events,
        waveMin: filter.waveMin,
        waveMax: filter.waveMax,
        from: filter.from,
        to: filter.to,
        search: filter.search,
        json: filter.json,
      },
      "m",
    );
    conditions.push("e.run_id IN (SELECT m.run_id FROM events m" + matching.sql + ")");
    params.push(...matching.params);
  }
  return { sql: andWhere(direct, ...conditions), params };
}

// ---- Summing up runs ----

// Per-run numbers that SQL can group (duplicated events do not change MAX/MIN;
// the event count counts each event_id once).
export type RunAggregate = {
  run_id: string;
  install_id: string | null;
  session_id: string | null;
  level: string | null;
  build: string | null;
  events: number;
  wave_reached: number;
  seconds: number;
  started_at: string | null;
  last_event_at: string | null;
  first_request_id: number | null;
};

const AGGREGATE_SELECT =
  "SELECT e.run_id AS run_id, MIN(e.install_id) AS install_id, MIN(e.session_id) AS session_id, " +
  "MIN(e.level) AS level, MIN(e.build) AS build, " +
  "COUNT(DISTINCT e.event_id) + SUM(CASE WHEN e.event_id IS NULL THEN 1 ELSE 0 END) AS events, " +
  "MAX(e.wave) AS wave_reached, MAX(e.t_play) AS seconds, " +
  "MIN(e.received_at) AS started_at, MAX(e.received_at) AS last_event_at, MIN(e.request_id) AS first_request_id " +
  "FROM events e";

function toAggregate(row: Row): RunAggregate {
  return {
    run_id: String(row.run_id),
    install_id: toText(row.install_id),
    session_id: toText(row.session_id),
    level: toText(row.level),
    build: toText(row.build),
    events: toNumber(row.events) ?? 0,
    wave_reached: toNumber(row.wave_reached) ?? 0,
    seconds: toNumber(row.seconds) ?? 0,
    started_at: toText(row.started_at),
    last_event_at: toText(row.last_event_at),
    first_request_id: toNumber(row.first_request_id),
  };
}

function isFatal(e: EventRow): boolean {
  return e.event === "damage" && (e.data.fatal === true || e.data.fatal === "true");
}

// Same rules as summarizeRun() (lib/runSummary.ts), from the parts above.
// keyEvents: the run's run_start / run_end / fatal damage events in seq order.
// lastScore: score of its last player-state event (used when there is no run_end).
export function summarizeFromParts(
  agg: RunAggregate,
  keyEvents: EventRow[],
  lastScore: number | null,
  nowMs: number,
): RunSummary {
  const runStart = keyEvents.find((e) => e.event === "run_start");
  const runEnd = keyEvents.find((e) => e.event === "run_end");
  const fatal = keyEvents.find(isFatal);

  let outcome: RunOutcome;
  if (runEnd && typeof runEnd.data.outcome === "string") {
    outcome = runEnd.data.outcome as RunOutcome;
  } else {
    const lastMs = utcMillis(agg.last_event_at);
    outcome = lastMs !== null && nowMs - lastMs < ABANDON_AFTER_SECONDS * 1000 ? "playing" : "abandoned";
  }

  return {
    run_id: agg.run_id,
    install_id: agg.install_id,
    session_id: agg.session_id,
    level: agg.level,
    build: agg.build,
    test: !!agg.install_id?.startsWith("qa-"),
    run_number: toNumber(runStart?.data.run_number ?? runEnd?.data.run_number),
    resumed_from_wave: (toNumber(runStart?.data.from_checkpoint_wave) ?? 0) > 0 ? toNumber(runStart?.data.from_checkpoint_wave) : null,
    outcome,
    wave_reached: agg.wave_reached,
    seconds: agg.seconds,
    score: runEnd ? (toNumber(runEnd.data.score) ?? lastScore) : lastScore,
    kills: toNumber(runEnd?.data.kills),
    accuracy: toNumber(runEnd?.data.accuracy),
    wpm: toNumber(runEnd?.data.words_per_minute),
    best_combo: toNumber(runEnd?.data.best_combo),
    killer: fatal ? toText(fatal.data.source) : null,
    death_wave: fatal ? fatal.wave : null,
    death_phase: fatal ? toText(fatal.data.phase) : null,
    tips_enabled: runStart ? runStart.data.tips_enabled === true : null,
    events: agg.events,
    started_at: agg.started_at,
    last_event_at: agg.last_event_at,
    first_request_id: agg.first_request_id,
  };
}

// Events that carry the player state (and so the score so far).
const SCORE_EVENTS = Object.keys(EVENTS).filter((type) => EVENTS[type].playerState);

// run_id -> score of the run's last player-state event.
async function loadLastScores(runIds: string[]): Promise<Map<string, number | null>> {
  const rows = await inChunks(runIds, (chunk) =>
    query(
      "SELECT e.run_id AS run_id, JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.score')) AS score FROM events e " +
        "JOIN (SELECT x.run_id AS run_id, MAX(x.seq) AS seq FROM events x " +
        "WHERE x.run_id IN (" + placeholders(chunk) + ") AND x.event IN (" + placeholders(SCORE_EVENTS) + ") " +
        "GROUP BY x.run_id) lastev ON lastev.run_id = e.run_id AND lastev.seq = e.seq",
      [...chunk, ...SCORE_EVENTS],
      120000,
    ),
  );
  const scores = new Map<string, number | null>();
  for (const row of rows) {
    scores.set(String(row.run_id), toNumber(row.score));
  }
  return scores;
}

// Every run matching `filter`, summed up (newest first).
export async function summarizeRuns(filter: EventFilter): Promise<RunSummary[]> {
  const where = buildRunWhere(filter);
  const aggregates = (await query(AGGREGATE_SELECT + where.sql + " GROUP BY e.run_id", where.params, 120000)).map(toAggregate);
  if (aggregates.length === 0) {
    return [];
  }

  // (written as one IN list so the index on `event` can be used)
  const keyRows = await loadEvents(
    where.sql +
      " AND e.event IN (?, ?, ?) AND (e.event <> ? OR JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.fatal')) = 'true')",
    [...where.params, "run_start", "run_end", "damage", "damage"],
  );
  const keyByRun = groupByRun(dedupeEvents(keyRows));

  const withoutEnd = aggregates
    .filter((agg) => !(keyByRun.get(agg.run_id) || []).some((e) => e.event === "run_end"))
    .map((agg) => agg.run_id);
  const lastScores = await loadLastScores(withoutEnd);

  const nowMs = Date.now();
  const runs = aggregates.map((agg) =>
    summarizeFromParts(agg, keyByRun.get(agg.run_id) || [], lastScores.get(agg.run_id) ?? null, nowMs),
  );
  runs.sort((a, b) => (b.started_at || "").localeCompare(a.started_at || ""));
  return runs;
}

// ---- Where a run was played from ----

type RequestInfo = Pick<RunRow, "client_ip" | "user_agent" | "origin" | "cf_ipcountry">;
type DeviceInfo = Pick<RunRow, "os" | "gpu" | "screen">;

async function loadRequestInfo(requestIds: number[]): Promise<Map<number, RequestInfo>> {
  const rows = await inChunks(requestIds, (chunk) =>
    query(
      "SELECT r.id AS id, r.client_ip AS client_ip, r.user_agent AS user_agent, r.origin AS origin, " +
        "r.cf_ipcountry AS cf_ipcountry FROM requests r WHERE r.id IN (" + placeholders(chunk) + ")",
      chunk,
    ),
  );
  const info = new Map<number, RequestInfo>();
  for (const row of rows) {
    info.set(Number(row.id), {
      client_ip: toText(row.client_ip),
      user_agent: toText(row.user_agent),
      origin: toText(row.origin),
      cf_ipcountry: toText(row.cf_ipcountry),
    });
  }
  return info;
}

function deviceOf(sessionStart: EventRow): DeviceInfo {
  const width = num(sessionStart, "screen_width");
  const height = num(sessionStart, "screen_height");
  return {
    os: str(sessionStart, "os"),
    gpu: str(sessionStart, "gpu"),
    screen: width !== null && height !== null ? width + "x" + height : null,
  };
}

// session_id -> the device its session_start reported (first copy kept).
async function loadSessionDevices(sessionIds: string[]): Promise<Map<string, DeviceInfo>> {
  const rows = await inChunks(sessionIds, (chunk) =>
    loadEvents(" WHERE e.event = ? AND e.session_id IN (" + placeholders(chunk) + ")", ["session_start", ...chunk]),
  );
  const devices = new Map<string, DeviceInfo>();
  for (const row of rows) {
    if (row.session_id && !devices.has(row.session_id)) {
      devices.set(row.session_id, deviceOf(row));
    }
  }
  return devices;
}

export type RunListOptions = {
  outcomes: RunOutcome[]; // empty = every outcome
  runNumber: RunNumberFilter;
};

function keepRun(run: RunSummary, options: RunListOptions): boolean {
  if (options.outcomes.length > 0 && !options.outcomes.includes(run.outcome)) {
    return false;
  }
  if (options.runNumber === "first") {
    return run.run_number === 1;
  }
  if (options.runNumber === "repeat") {
    return run.run_number !== null && run.run_number > 1;
  }
  return true;
}

// GET /api/runs: the runs, with the IP / browser of their first request and
// the device of their session.
export async function loadRuns(filter: EventFilter, options: RunListOptions): Promise<RunRow[]> {
  const runs = (await summarizeRuns(filter)).filter((run) => keepRun(run, options));
  const requestIds = [...new Set(runs.map((run) => run.first_request_id).filter((id): id is number => id !== null))];
  const sessionIds = [...new Set(runs.map((run) => run.session_id).filter((id): id is string => !!id))];
  const requests = await loadRequestInfo(requestIds);
  const devices = await loadSessionDevices(sessionIds);

  return runs.map((run) => {
    const request = run.first_request_id !== null ? requests.get(run.first_request_id) : undefined;
    const device = run.session_id ? devices.get(run.session_id) : undefined;
    return {
      ...run,
      client_ip: request?.client_ip ?? null,
      user_agent: request?.user_agent ?? null,
      origin: request?.origin ?? null,
      cf_ipcountry: request?.cf_ipcountry ?? null,
      os: device?.os ?? null,
      gpu: device?.gpu ?? null,
      screen: device?.screen ?? null,
    };
  });
}

// ---- Players (one row per install_id) ----

// The non-empty values, each once, in first-seen order.
function distinct(values: (string | null)[]): string[] {
  return [...new Set(values.filter((value): value is string => !!value))];
}

// Appends to the list kept under `key` in `map`.
function pushTo<T>(map: Map<string, T[]>, key: string, value: T): void {
  const list = map.get(key);
  if (list) {
    list.push(value);
  } else {
    map.set(key, [value]);
  }
}

// GET /api/players: every install that has events under the global filters
// (test plays, level), with its runs summed up.
// from / to: the top bar's time range (received_at, UTC); empty = no bound.
export async function loadPlayers(includeTest: boolean, level: string, from = "", to = ""): Promise<PlayersResponse> {
  const where = buildEventWhere({ ...EMPTY_EVENT_FILTER, includeTest, level, from, to }, "e");

  // When it was seen and how many page loads.
  const installRows = await query(
    "SELECT e.install_id AS install_id, MIN(e.received_at) AS first_seen, MAX(e.received_at) AS last_seen, " +
      "COUNT(DISTINCT e.session_id) AS sessions FROM events e" +
      andWhere(where, "e.install_id IS NOT NULL", "e.install_id <> ''") +
      " GROUP BY e.install_id",
    where.params,
    120000,
  );

  // Its runs.
  const runsByInstall = new Map<string, RunSummary[]>();
  for (const run of await summarizeRuns({ ...EMPTY_EVENT_FILTER, includeTest, level, from, to })) {
    pushTo(runsByInstall, run.install_id || "", run);
  }

  // Every IP and browser it sent from (the requests that carried its events).
  const networkRows = await query(
    "SELECT x.install_id AS install_id, r.client_ip AS client_ip, r.user_agent AS user_agent, " +
      "r.cf_ipcountry AS country, MIN(r.id) AS first_request FROM " +
      "(SELECT DISTINCT e.install_id AS install_id, e.request_id AS request_id FROM events e" +
      andWhere(where, "e.install_id IS NOT NULL") +
      ") x JOIN requests r ON r.id = x.request_id " +
      "GROUP BY x.install_id, r.client_ip, r.user_agent, r.cf_ipcountry ORDER BY first_request",
    where.params,
    120000,
  );
  const networkByInstall = new Map<string, Row[]>();
  for (const row of networkRows) {
    pushTo(networkByInstall, String(row.install_id), row);
  }

  // Its device, from its latest session_start (any level: the page load
  // happens before a level is picked).
  const testOnly = buildEventWhere({ ...EMPTY_EVENT_FILTER, includeTest }, "e");
  const sessionStarts = await loadEvents(andWhere(testOnly, "e.event = ?"), [...testOnly.params, "session_start"]);
  const latestStart = new Map<string, EventRow>();
  const storageFailed = new Set<string>();
  for (const e of dedupeEvents(sessionStarts)) {
    if (!e.install_id) {
      continue;
    }
    latestStart.set(e.install_id, e); // rows come oldest first, so the last one wins
    if (e.data.storage_ok === false) {
      storageFailed.add(e.install_id);
    }
  }

  // Test installs under the level filter, counted even when they are hidden.
  const levelOnly = buildEventWhere({ ...EMPTY_EVENT_FILTER, includeTest: true, level }, "e");
  const [testCount] = await query(
    "SELECT COUNT(DISTINCT e.install_id) AS n FROM events e" + andWhere(levelOnly, "e.install_id LIKE ?"),
    [...levelOnly.params, "qa-%"],
  );

  const players: PlayerRow[] = installRows.map((row) => {
    const installId = String(row.install_id);
    const runs = runsByInstall.get(installId) || [];
    const network = networkByInstall.get(installId) || [];
    const start = latestStart.get(installId);
    const device = start ? deviceOf(start) : null;
    const scores = runs.map((run) => run.score).filter((score): score is number => score !== null);
    const runNumbers = [...new Set(runs.map((run) => run.run_number).filter((n): n is number => n !== null))];
    const ips = distinct(network.map((r) => toText(r.client_ip)));
    const userAgents = distinct(network.map((r) => toText(r.user_agent)));
    return {
      install_id: installId,
      test: installId.startsWith("qa-"),
      first_seen: toText(row.first_seen),
      last_seen: toText(row.last_seen),
      sessions: toNumber(row.sessions) ?? 0,
      runs: runs.length,
      wins: runs.filter((run) => run.outcome === "won").length,
      losses: runs.filter((run) => run.outcome === "lost").length,
      restarts: runs.filter((run) => run.outcome === "restart").length,
      abandoned: runs.filter((run) => run.outcome === "abandoned").length,
      best_wave: runs.length > 0 ? Math.max(...runs.map((run) => run.wave_reached)) : null,
      play_seconds: runs.reduce((sum, run) => sum + run.seconds, 0),
      best_score: scores.length > 0 ? Math.max(...scores) : null,
      run_numbers: runNumbers.sort((a, b) => a - b).join(", "),
      ip_count: ips.length,
      ips: ips.join(", "),
      countries: distinct(network.map((r) => toText(r.country))).join(", "),
      user_agent_count: userAgents.length,
      user_agents: userAgents.join(" | "),
      os: device?.os ?? null,
      gpu: device?.gpu ?? null,
      screen: device?.screen ?? null,
      language: start ? str(start, "language") : null,
      storage_ok: start ? !storageFailed.has(installId) : null,
    };
  });
  players.sort((a, b) => (b.last_seen || "").localeCompare(a.last_seen || ""));
  return { players, testInstalls: toNumber(testCount?.n) ?? 0 };
}
