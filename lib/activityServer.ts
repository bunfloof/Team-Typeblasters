// When the game was played: the stretches of time with events ("play
// sessions"), for the top bar's time-range picker. A new play session starts
// after a quiet gap of GAP_MINUTES with no event at all. Server only.
//
// One query groups the events by 10-minute slot and install_id; the slots
// are then joined into play sessions here, counting each browser
// (install_id) once per play session.
import { query } from "./db";
import { buildEventWhere, type EventFilter } from "./filters";

export const GAP_MINUTES = 30;
const SLOT_MINUTES = 10;
const MAX_SESSIONS = 120;

export type PlaySession = {
  start: string; // first event (UTC "YYYY-MM-DD HH:MM:SS")
  end: string; // last event
  players: number; // browsers (install_id), team test plays (qa-) not counted
  test_players: number; // qa- browsers (only when test plays are included)
  runs: number; // run_start events
  events: number;
  levels: string[];
  builds: string[];
};

export type ActivityResponse = { gapMinutes: number; sessions: PlaySession[]; truncated: boolean };

type SlotRow = {
  slot: number;
  install_id: string | null;
  first_at: string;
  last_at: string;
  events: number;
  runs: number;
  levels: string | null;
  builds: string | null;
};

function text(value: unknown): string {
  return value instanceof Date ? value.toISOString().slice(0, 19).replace("T", " ") : String(value ?? "");
}

// filter: the global filters (test plays, level); its time range is ignored,
// so every play session can be picked.
export async function loadActivity(filter: EventFilter): Promise<ActivityResponse> {
  const where = buildEventWhere({ ...filter, events: [], from: "", to: "", installId: "", sessionId: "", runId: "" });
  const rows = await query<SlotRow>(
    "SELECT TIMESTAMPDIFF(MINUTE, '2000-01-01 00:00:00', e.received_at) DIV " + SLOT_MINUTES + " AS slot, " +
      "e.install_id AS install_id, MIN(e.received_at) AS first_at, MAX(e.received_at) AS last_at, COUNT(*) AS events, " +
      "SUM(CASE WHEN e.event = 'run_start' THEN 1 ELSE 0 END) AS runs, " +
      "GROUP_CONCAT(DISTINCT e.level ORDER BY e.level SEPARATOR ',') AS levels, " +
      "GROUP_CONCAT(DISTINCT e.build ORDER BY e.build SEPARATOR ',') AS builds " +
      "FROM events e" + where.sql + " GROUP BY slot, e.install_id ORDER BY slot",
    where.params,
    120000,
  );

  // Join the slots into play sessions: a gap longer than GAP_MINUTES ends one.
  type Building = { start: string; end: string; installs: Set<string>; runs: number; events: number; levels: Set<string>; builds: Set<string> };
  const sessions: Building[] = [];
  let current: Building | null = null;
  for (const row of rows) {
    const first = text(row.first_at);
    const last = text(row.last_at);
    const gapMs = current ? Date.parse(first.replace(" ", "T") + "Z") - Date.parse(current.end.replace(" ", "T") + "Z") : Infinity;
    if (!current || gapMs > GAP_MINUTES * 60000) {
      current = { start: first, end: last, installs: new Set(), runs: 0, events: 0, levels: new Set(), builds: new Set() };
      sessions.push(current);
    }
    if (first < current.start) current.start = first;
    if (last > current.end) current.end = last;
    if (row.install_id) current.installs.add(row.install_id);
    current.runs += Number(row.runs) || 0;
    current.events += Number(row.events) || 0;
    for (const level of String(row.levels ?? "").split(",")) if (level) current.levels.add(level);
    for (const build of String(row.builds ?? "").split(",")) if (build) current.builds.add(build);
  }

  const newestFirst = sessions.reverse();
  return {
    gapMinutes: GAP_MINUTES,
    truncated: newestFirst.length > MAX_SESSIONS,
    sessions: newestFirst.slice(0, MAX_SESSIONS).map((s) => {
      const installs = [...s.installs];
      return {
        start: s.start.slice(0, 19),
        end: s.end.slice(0, 19),
        players: installs.filter((id) => !id.startsWith("qa-")).length,
        test_players: installs.filter((id) => id.startsWith("qa-")).length,
        runs: s.runs,
        events: s.events,
        levels: [...s.levels],
        builds: [...s.builds],
      };
    }),
  };
}
