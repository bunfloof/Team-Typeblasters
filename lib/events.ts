// Loading events from the database (server side only, it imports db.ts).
import { parseJson, query, type Row } from "./db";
import type { EventRow } from "./types";

const EVENT_SELECT =
  "SELECT e.id, e.request_id, e.position_in_batch, e.received_at, e.event, e.event_id, e.batch_id, " +
  "e.install_id, e.session_id, e.run_id, e.seq, e.build, e.level, e.wave, e.t_play, e.t_real, e.data FROM events e";

function toNumberOrNull(value: unknown): number | null {
  if (value === null || value === undefined || value === "") {
    return null;
  }
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

export function toEventRow(row: Row): EventRow {
  return {
    id: Number(row.id),
    request_id: Number(row.request_id),
    position_in_batch: Number(row.position_in_batch),
    received_at: String(row.received_at),
    event: String(row.event ?? ""),
    event_id: (row.event_id as string) ?? null,
    batch_id: (row.batch_id as string) ?? null,
    install_id: (row.install_id as string) ?? null,
    session_id: (row.session_id as string) ?? null,
    run_id: (row.run_id as string) ?? null,
    seq: toNumberOrNull(row.seq),
    build: (row.build as string) ?? null,
    level: (row.level as string) ?? null,
    wave: toNumberOrNull(row.wave),
    t_play: toNumberOrNull(row.t_play),
    t_real: toNumberOrNull(row.t_real),
    data: parseJson(row.data),
  };
}

// Events matching `where` (a " WHERE ..." built by lib/filters.ts, alias e),
// oldest first. limit 0 = no limit.
export async function loadEvents(where: string, params: unknown[], limit = 0, orderBy = "e.id"): Promise<EventRow[]> {
  const sql = EVENT_SELECT + where + " ORDER BY " + orderBy + (limit > 0 ? " LIMIT " + Math.floor(limit) : "");
  const rows = await query(sql, params, 120000);
  return rows.map(toEventRow);
}

// A batch that was sent twice (the reply got lost, the game retried) is
// stored twice with the same event_id: keep the first copy of each.
export function dedupeEvents(rows: EventRow[]): EventRow[] {
  const seen = new Set<string>();
  const kept: EventRow[] = [];
  for (const row of rows) {
    const key = row.event_id || "row-" + row.id;
    if (seen.has(key)) {
      continue;
    }
    seen.add(key);
    kept.push(row);
  }
  return kept;
}

// A numeric field of an event's data (null when missing or not a number).
export function num(row: EventRow, field: string): number | null {
  return toNumberOrNull(row.data[field]);
}

export function str(row: EventRow, field: string): string | null {
  const value = row.data[field];
  return value === null || value === undefined ? null : String(value);
}

export function bool(row: EventRow, field: string): boolean {
  return row.data[field] === true || row.data[field] === "true" || row.data[field] === 1;
}
