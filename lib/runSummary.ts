// One run (Start pressed -> won / died / restarted / tab closed) summed up
// from its events. Safe to import anywhere (no database code).
import type { EventRow } from "./types";

export type RunOutcome = "won" | "lost" | "restart" | "abandoned" | "playing";

export type RunSummary = {
  run_id: string;
  install_id: string | null;
  session_id: string | null;
  level: string | null;
  build: string | null;
  test: boolean; // install_id qa-...
  run_number: number | null; // 1 = first run in this browser
  resumed_from_wave: number | null; // run_start from_checkpoint_wave: went on from a checkpoint at this wave (Restart after a death); null = from the start
  outcome: RunOutcome;
  wave_reached: number; // highest wave seen
  seconds: number; // play seconds (highest t_play)
  score: number | null;
  kills: number | null;
  accuracy: number | null; // 0-100
  wpm: number | null; // run_end words_per_minute (rides included)
  best_combo: number | null;
  killer: string | null; // lost: what dealt the fatal hit (zombie_runner, orb...)
  death_wave: number | null;
  death_phase: string | null;
  tips_enabled: boolean | null;
  events: number;
  started_at: string | null; // received_at of the first event (UTC)
  last_event_at: string | null;
  first_request_id: number | null; // join to requests for IP / browser
};

// A run with no run_end whose last event is older than this is "abandoned"
// (the tab was closed or the browser crashed); newer ones are "playing".
export const ABANDON_AFTER_SECONDS = 180;

function n(value: unknown): number | null {
  if (value === null || value === undefined || value === "") {
    return null;
  }
  const x = Number(value);
  return Number.isFinite(x) ? x : null;
}

// UTC text from the database ("YYYY-MM-DD HH:MM:SS.ffffff") -> epoch ms.
export function utcMillis(text: string | null): number | null {
  if (!text) {
    return null;
  }
  const ms = Date.parse(text.replace(" ", "T") + "Z");
  return Number.isNaN(ms) ? null : ms;
}

// events: every (deduplicated) event of ONE run, in seq order.
export function summarizeRun(events: EventRow[], nowMs = Date.now()): RunSummary {
  const first = events[0];
  const last = events[events.length - 1];
  const runStart = events.find((e) => e.event === "run_start");
  const runEnd = events.find((e) => e.event === "run_end");
  const fatal = events.find((e) => e.event === "damage" && (e.data.fatal === true || e.data.fatal === "true"));

  let outcome: RunOutcome;
  if (runEnd && typeof runEnd.data.outcome === "string") {
    outcome = runEnd.data.outcome as RunOutcome;
  } else {
    const lastMs = utcMillis(last?.received_at ?? null);
    outcome = lastMs !== null && nowMs - lastMs < ABANDON_AFTER_SECONDS * 1000 ? "playing" : "abandoned";
  }

  let waveReached = 0;
  let seconds = 0;
  let score: number | null = null;
  for (const e of events) {
    waveReached = Math.max(waveReached, e.wave ?? 0);
    seconds = Math.max(seconds, e.t_play ?? 0);
    const s = n(e.data.score);
    if (s !== null) {
      score = s;
    }
  }

  return {
    run_id: first?.run_id ?? "",
    install_id: first?.install_id ?? null,
    session_id: first?.session_id ?? null,
    level: first?.level ?? null,
    build: first?.build ?? null,
    test: !!first?.install_id?.startsWith("qa-"),
    run_number: n(runStart?.data.run_number ?? runEnd?.data.run_number),
    resumed_from_wave: (n(runStart?.data.from_checkpoint_wave) ?? 0) > 0 ? n(runStart?.data.from_checkpoint_wave) : null,
    outcome,
    wave_reached: waveReached,
    seconds,
    score: runEnd ? (n(runEnd.data.score) ?? score) : score,
    kills: n(runEnd?.data.kills),
    accuracy: n(runEnd?.data.accuracy),
    wpm: n(runEnd?.data.words_per_minute),
    best_combo: n(runEnd?.data.best_combo),
    killer: fatal ? String(fatal.data.source ?? "") || null : null,
    death_wave: fatal ? fatal.wave : null,
    death_phase: fatal ? ((fatal.data.phase as string) ?? null) : null,
    tips_enabled: runStart ? runStart.data.tips_enabled === true : null,
    events: events.length,
    started_at: first?.received_at ?? null,
    last_event_at: last?.received_at ?? null,
    first_request_id: first?.request_id ?? null,
  };
}

// Groups events (any order) by run_id and sorts each run by seq.
export function groupByRun(events: EventRow[]): Map<string, EventRow[]> {
  const runs = new Map<string, EventRow[]>();
  for (const e of events) {
    if (!e.run_id) {
      continue;
    }
    let list = runs.get(e.run_id);
    if (!list) {
      list = [];
      runs.set(e.run_id, list);
    }
    list.push(e);
  }
  for (const list of runs.values()) {
    list.sort((a, b) => (a.seq ?? 0) - (b.seq ?? 0) || a.id - b.id);
  }
  return runs;
}
