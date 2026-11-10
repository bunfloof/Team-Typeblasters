// The run timeline (/runs/[runId]): turns the events of ONE run into the
// lanes the page draws - phases, zombies, word attempts, wrong keys, event
// markers, health / combo / zombies-alive series, typing speed and the map.
// Pure functions, no database: safe to import anywhere.
//
// Newer game builds log more (zombie_spawn, sample, prop and boss_word events;
// zombie_id, x/z positions, start_t_play, key_ms, player_x/player_z...).
// Older rows lack them, so every lane has a fallback, and each value that was
// estimated rather than measured says so (see the *Source fields).
import type { EventRow } from "./types";
import type { RunSummary } from "./runSummary";

// ---------------------------------------------------------------------------
// GET /api/runs/[runId] response
// ---------------------------------------------------------------------------

// The HTTP request that brought the run's first batch (IP and browser).
export type RunRequestInfo = {
  id: number;
  received_at: string | null;
  client_ip: string | null;
  peer_ip: string | null;
  cf_connecting_ip: string | null;
  x_real_ip: string | null;
  x_forwarded_for: string | null;
  cf_ipcountry: string | null;
  user_agent: string | null;
  origin: string | null;
  referer: string | null;
  accept_language: string | null;
  sec_ch_ua: string | null;
  sec_ch_ua_platform: string | null;
  sec_ch_ua_mobile: string | null;
};

export type RunDetailResponse = {
  summary: RunSummary;
  events: EventRow[]; // every event of the run, deduplicated, in seq order
  // The session_start event of the page load the run belongs to (its data
  // fields plus received_at), or null when it was not logged / lost.
  session: (Record<string, unknown> & { received_at: string }) | null;
  request: RunRequestInfo | null;
  ips: string[]; // every client_ip that sent a batch of this run
  siblings: {
    prev: string | null; // previous run of the same install_id
    next: string | null;
    position: number | null; // 1-based position of this run among them
    total: number;
  };
  quality: {
    rawEvents: number; // rows before dedupe
    duplicates: number; // rows dropped by dedupe (retried batches)
    seqGaps: number; // missing seq numbers inside the run (lost batches?)
    batches: number; // distinct HTTP requests
  };
  hiddenBy: string[]; // global filters that would normally hide this run
};

// ---------------------------------------------------------------------------
// Small readers for an event's `data` (lib/events.ts is server-only)
// ---------------------------------------------------------------------------

export function dataNum(e: EventRow, field: string): number | null {
  const value = e.data[field];
  if (value === null || value === undefined || value === "" || typeof value === "boolean") {
    return null;
  }
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

export function dataStr(e: EventRow, field: string): string | null {
  const value = e.data[field];
  return value === null || value === undefined || value === "" ? null : String(value);
}

export function dataBool(e: EventRow, field: string): boolean {
  const value = e.data[field];
  return value === true || value === "true" || value === 1;
}

// Seconds of play when the event happened (the timeline x).
export function tOf(e: EventRow): number {
  return e.t_play ?? dataNum(e, "t_play") ?? 0;
}

// Seconds as m:ss.s (tooltips) - e.g. 83.42 -> "1:23.4".
export function fmtT(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) {
    return "-";
  }
  const tenths = Math.round(Math.max(0, seconds) * 10);
  const m = Math.floor(tenths / 600);
  const s = (tenths % 600) / 10;
  return m + ":" + (s < 10 ? "0" : "") + s.toFixed(1);
}

// Seconds as m:ss (axis labels).
export function fmtClock(seconds: number): string {
  const total = Math.max(0, Math.round(seconds));
  return Math.floor(total / 60) + ":" + String(total % 60).padStart(2, "0");
}

// "zombie_runner" -> "runner", anything else -> null.
export function zombieKindOf(target: string | null): string | null {
  return target && target.startsWith("zombie_") ? target.slice("zombie_".length) : null;
}

// ---------------------------------------------------------------------------
// Lane types
// ---------------------------------------------------------------------------

export type PhaseKind = "ride" | "fight" | "boss";

export type PhaseSegment = {
  kind: PhaseKind;
  wave: number | null; // the wave it rides to / fights
  label: string; // "Ride to wave 2", "Wave 2", "Boss"
  start: number;
  end: number;
  // How the segment ended: its own end event, the next phase starting, the
  // run ending, or the log stopping (tab closed / still playing).
  endedBy: "end_event" | "next_phase" | "run_end" | "last_event";
  startEvent: EventRow | null;
  endEvent: EventRow | null;
};

export type AwayMark = {
  t: number;
  kind: "pause" | "hidden";
  reason: string | null; // pause: esc_key / tab_hidden / button
  seconds: number | null; // paused_seconds / hidden_seconds of the matching resume / page_visible
  seq: number | null;
};

export type ZombieBar = {
  key: string; // "z12" (zombie_id) or "e<seq>" (old rows)
  zombieId: number | null;
  kind: string;
  word: string; // its word at the end
  words: string[]; // every word it carried that we know of
  spawnT: number;
  endT: number;
  // Where spawnT comes from: the zombie_spawn event, or estimated from the
  // end event (seconds_since_wake for woken ambushers, else seconds_alive).
  spawnSource: "spawn_event" | "seconds_since_wake" | "seconds_alive" | "unknown";
  ended: boolean; // false = still alive when the log stops
  outcome: string; // zombie.outcome, or "alive" when no end event
  ghost: boolean; // an ambusher the player never saw (never_seen)
  wasTargeted: boolean;
  typingStarts: number | null;
  lettersTyped: number | null;
  wordLength: number | null;
  ambusher: boolean;
  chainLinked: boolean;
  frenzyWord: boolean;
  wordShifts: number | null;
  secondsAlive: number | null;
  blastRadius: number | null;
  ring: {
    atFirstLetter: number | null;
    atLastStart: number | null;
    maxWhileTyped: number | null;
    max: number | null;
  };
  spawnPos: { x: number; z: number } | null;
  endPos: { x: number; z: number } | null;
  wave: number | null;
  attempts: string[]; // keys of the WordBars typed at it
  spawnEvent: EventRow | null;
  endEvent: EventRow | null;
};

export type WordBar = {
  key: string;
  start: number;
  end: number;
  // start_t_play (logged), t_play - typing_seconds (estimated), or the end itself.
  startSource: "start_t_play" | "typing_seconds" | "instant";
  target: string; // zombie_<kind>, barrel, crate_<kind>, orb, boss_head/arm/leg, quiz_answer
  targetId: number | null; // zombie_id (new rows), -1 / null otherwise
  zombieKey: string | null; // the ZombieBar it was typed at (by id, or matched by word + time)
  word: string;
  wordLength: number | null;
  outcome: string; // finished, dropped, dropped_by_frenzy, target_gone, run_ended
  correctKeys: number;
  wrongKeys: number;
  rockets: number;
  typingSeconds: number | null;
  wordsFinished: number | null;
  candidates: number | null;
  caseSensitive: boolean;
  wordStyle: string | null;
  frenzyOn: boolean;
  keyTimes: number[] | null; // t_play of every correct key (from key_ms), when logged
  event: EventRow;
};

export type WrongKeyMark = {
  t: number;
  typed: string;
  expected: string | null;
  caseOnly: boolean;
  stray: boolean;
  target: string | null;
  targetId: number | null;
  letterIndex: number | null;
  wordLength: number | null;
  event: EventRow;
};

// Event markers on the "Events" lane, grouped so the lane never needs more
// than 8 colors. Order = row order on the lane and color slot order.
export const MARKER_CATEGORIES = [
  { id: "damage", label: "Damage" },
  { id: "power", label: "Powers & weapons" },
  { id: "blast", label: "Explosions" },
  { id: "crate", label: "Crates & barrels" },
  { id: "boss", label: "Boss & quiz" },
  { id: "help", label: "Tips & hints" },
  { id: "away", label: "Pause & tab" },
  { id: "run", label: "Run & other" },
] as const;

export type MarkerCategory = (typeof MARKER_CATEGORIES)[number]["id"];

// Event types drawn on other lanes (not repeated as markers).
const LANE_EVENTS = new Set(["zombie", "zombie_spawn", "word", "wrong_key", "sample", "ride_start", "ride_end", "wave_start", "wave_end"]);

const CATEGORY_OF: Record<string, MarkerCategory> = {
  damage: "damage",
  blast: "blast",
  power: "power",
  crate: "crate",
  prop: "crate",
  boss_attack: "boss",
  boss_hit: "boss",
  boss_heal: "boss",
  boss_word: "boss",
  quiz: "boss",
  orb: "boss",
  tip: "help",
  hint: "help",
  tips_toggle: "help",
  pause: "away",
  resume: "away",
  page_hidden: "away",
  page_visible: "away",
};

// Which events-lane category an event type belongs to ("run" for unknown types).
export function markerCategoryOf(eventType: string): MarkerCategory {
  return CATEGORY_OF[eventType] ?? "run";
}

export type Marker = {
  t: number;
  category: MarkerCategory;
  title: string; // one line, e.g. "Bitten by a runner"
  detail: string; // second line, e.g. "health 70 -> 60 · combo lost"
  important: boolean; // fatal hit, run end
  weight: number; // blast kills (marker size)
  event: EventRow;
};

export type SeriesPoint = { t: number; v: number; event: string; note: string | null };

export type MapPoint = { x: number; z: number; t: number };

export type MapData = {
  path: MapPoint[]; // the player's positions in time order
  zombieSpawns: (MapPoint & { kind: string; key: string; word: string })[];
  zombieEnds: (MapPoint & { kind: string; key: string; word: string; outcome: string })[];
  blasts: (MapPoint & { kills: number; source: string; radius: number | null })[];
  hits: (MapPoint & { fatal: boolean; source: string; estimated: boolean })[];
  props: (MapPoint & { kind: string; word: string })[];
  hasPositions: boolean;
  extent: { minX: number; maxX: number; minZ: number; maxZ: number } | null;
};

export type Timeline = {
  duration: number; // x-axis max (play seconds)
  events: EventRow[]; // all events, seq order
  hasSamples: boolean; // the dense 1-per-second "sample" heartbeat exists
  schema: {
    spawns: boolean; // zombie_spawn events
    positions: boolean; // x/z positions
    keyTimes: boolean; // key_ms on word
    wordStarts: boolean; // start_t_play on word
  };
  phases: PhaseSegment[];
  away: AwayMark[];
  zombies: ZombieBar[]; // sorted by spawn time
  words: WordBar[]; // sorted by start
  wrongKeys: WrongKeyMark[];
  markers: Marker[];
  health: SeriesPoint[];
  combo: SeriesPoint[];
  score: SeriesPoint[];
  alive: SeriesPoint[];
  aliveSource: "samples" | "bars"; // game-reported every second, or counted from the zombie bars
  wpm: {
    windowSeconds: number;
    step: number; // seconds between fromWords points
    fromWords: SeriesPoint[]; // rolling window over the word attempts' correct keys
    fromSamples: SeriesPoint[]; // rolling window over the heartbeat's cumulative correct_keys
    runAverage: number | null; // run_end words_per_minute (rides included)
  };
  stateEvents: EventRow[]; // events carrying player state (health...), time order
  map: MapData;
};

// ---------------------------------------------------------------------------
// Building the timeline
// ---------------------------------------------------------------------------

export function sortRunEvents(events: EventRow[]): EventRow[] {
  return [...events].sort((a, b) => (a.seq ?? 0) - (b.seq ?? 0) || a.id - b.id);
}

function position(e: EventRow, xField: string, zField: string): { x: number; z: number } | null {
  const x = dataNum(e, xField);
  const z = dataNum(e, zField);
  return x === null || z === null ? null : { x, z };
}

function phaseLabel(kind: PhaseKind, wave: number | null, bossNext: boolean): string {
  if (kind === "ride") {
    return bossNext ? "Ride to boss" : wave !== null ? "Ride to wave " + wave : "Ride";
  }
  if (kind === "boss") {
    return "Boss";
  }
  return wave !== null ? "Wave " + wave : "Fight";
}

function buildPhases(events: EventRow[], duration: number): PhaseSegment[] {
  const phases: PhaseSegment[] = [];
  let open: PhaseSegment | null = null;
  let lastEnd = 0;

  const close = (t: number, endedBy: PhaseSegment["endedBy"], endEvent: EventRow | null) => {
    if (!open) {
      return;
    }
    open.end = Math.max(open.start, t);
    open.endedBy = endedBy;
    open.endEvent = endEvent;
    phases.push(open);
    lastEnd = open.end;
    open = null;
  };

  const start = (kind: PhaseKind, e: EventRow, bossNext: boolean) => {
    const t = tOf(e);
    close(t, "next_phase", null);
    open = { kind, wave: e.wave, label: phaseLabel(kind, e.wave, bossNext), start: t, end: t, endedBy: "last_event", startEvent: e, endEvent: null };
  };

  // An end event without its start (lost batch): rebuild the start from `seconds`.
  const orphanEnd = (kind: PhaseKind, e: EventRow) => {
    const t = tOf(e);
    const seconds = dataNum(e, "seconds");
    const from = seconds !== null ? Math.max(lastEnd, t - seconds) : lastEnd;
    phases.push({ kind, wave: e.wave, label: phaseLabel(kind, e.wave, false), start: from, end: t, endedBy: "end_event", startEvent: null, endEvent: e });
    lastEnd = t;
  };

  for (const e of events) {
    const current = open as PhaseSegment | null;
    switch (e.event) {
      case "ride_start":
        start("ride", e, dataBool(e, "boss_next"));
        break;
      case "ride_end":
        if (current && current.kind === "ride") {
          close(tOf(e), "end_event", e);
        } else {
          orphanEnd("ride", e);
        }
        break;
      case "wave_start":
        start(dataBool(e, "boss_fight") ? "boss" : "fight", e, false);
        break;
      case "wave_end":
        if (current && current.kind !== "ride") {
          close(tOf(e), "end_event", e);
        } else {
          orphanEnd(dataBool(e, "boss_fight") ? "boss" : "fight", e);
        }
        break;
      case "run_end":
        close(tOf(e), "run_end", e);
        break;
    }
  }
  close(duration, "last_event", null);
  return phases.sort((a, b) => a.start - b.start);
}

function buildAway(events: EventRow[]): AwayMark[] {
  const marks: AwayMark[] = [];
  let openPause: AwayMark | null = null;
  let openHidden: AwayMark | null = null;
  for (const e of events) {
    if (e.event === "pause") {
      openPause = { t: tOf(e), kind: "pause", reason: dataStr(e, "reason"), seconds: null, seq: e.seq };
      marks.push(openPause);
    } else if (e.event === "resume" && openPause) {
      openPause.seconds = dataNum(e, "paused_seconds");
      openPause = null;
    } else if (e.event === "page_hidden") {
      openHidden = { t: tOf(e), kind: "hidden", reason: null, seconds: null, seq: e.seq };
      marks.push(openHidden);
    } else if (e.event === "page_visible" && openHidden) {
      openHidden.seconds = dataNum(e, "hidden_seconds");
      openHidden = null;
    }
  }
  return marks;
}

function uniqueWords(words: (string | null)[]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const word of words) {
    if (word && !seen.has(word)) {
      seen.add(word);
      out.push(word);
    }
  }
  return out;
}

function buildZombies(events: EventRow[], duration: number): ZombieBar[] {
  const spawnsById = new Map<number, EventRow>();
  const spawnsWithoutId: EventRow[] = [];
  for (const e of events) {
    if (e.event !== "zombie_spawn") {
      continue;
    }
    const id = dataNum(e, "zombie_id");
    if (id !== null && id >= 0) {
      spawnsById.set(id, e);
    } else {
      spawnsWithoutId.push(e);
    }
  }

  const bars: ZombieBar[] = [];
  const usedSpawns = new Set<EventRow>();

  const makeBar = (spawn: EventRow | null, end: EventRow | null): ZombieBar => {
    const source = end ?? spawn!;
    const id = dataNum(source, "zombie_id");
    const endT = end ? tOf(end) : duration;
    const secondsAlive = end ? dataNum(end, "seconds_alive") : null;
    const sinceWake = end ? dataNum(end, "seconds_since_wake") : null;
    const ambusher = dataBool(source, "ambusher");

    let spawnT: number;
    let spawnSource: ZombieBar["spawnSource"];
    if (spawn) {
      spawnT = tOf(spawn);
      spawnSource = "spawn_event";
    } else if (ambusher && sinceWake !== null && sinceWake >= 0) {
      spawnT = endT - sinceWake;
      spawnSource = "seconds_since_wake";
    } else if (secondsAlive !== null) {
      spawnT = endT - secondsAlive;
      spawnSource = "seconds_alive";
    } else {
      spawnT = endT;
      spawnSource = "unknown";
    }

    const outcome = end ? (dataStr(end, "outcome") ?? "unknown") : "alive";
    const ref = end ?? spawn!;
    return {
      key: id !== null && id >= 0 ? "z" + id : "e" + (source.seq ?? source.id),
      zombieId: id !== null && id >= 0 ? id : null,
      kind: dataStr(source, "kind") ?? "normal",
      word: dataStr(source, "word") ?? "",
      words: uniqueWords([spawn ? dataStr(spawn, "word") : null, end ? dataStr(end, "word") : null]),
      spawnT: Math.max(0, Math.min(spawnT, endT)),
      endT,
      spawnSource,
      ended: !!end,
      outcome,
      ghost: outcome === "never_seen",
      wasTargeted: end ? dataBool(end, "was_targeted") : false,
      typingStarts: end ? dataNum(end, "typing_starts") : null,
      lettersTyped: end ? dataNum(end, "letters_typed") : null,
      wordLength: dataNum(ref, "word_length"),
      ambusher,
      chainLinked: dataBool(ref, "chain_linked"),
      frenzyWord: dataBool(ref, "frenzy_word"),
      wordShifts: end ? dataNum(end, "word_shifts") : null,
      secondsAlive,
      blastRadius: dataNum(ref, "blast_radius") ?? (spawn ? dataNum(spawn, "blast_radius") : null),
      ring: {
        atFirstLetter: end ? dataNum(end, "in_ring_at_first_letter") : null,
        atLastStart: end ? dataNum(end, "in_ring_at_last_start") : null,
        maxWhileTyped: end ? dataNum(end, "in_ring_max_while_typed") : null,
        max: end ? dataNum(end, "in_ring_max") : null,
      },
      spawnPos: spawn ? position(spawn, "x", "z") : null,
      endPos: end ? position(end, "x", "z") : null,
      wave: (spawn ?? end)!.wave,
      attempts: [],
      spawnEvent: spawn,
      endEvent: end,
    };
  };

  for (const e of events) {
    if (e.event !== "zombie") {
      continue;
    }
    const id = dataNum(e, "zombie_id");
    const spawn = id !== null && id >= 0 ? (spawnsById.get(id) ?? null) : null;
    if (spawn) {
      usedSpawns.add(spawn);
    }
    bars.push(makeBar(spawn, e));
  }
  // Spawned but never ended (still alive when the log stops).
  for (const spawn of [...spawnsById.values(), ...spawnsWithoutId]) {
    if (!usedSpawns.has(spawn)) {
      bars.push(makeBar(spawn, null));
    }
  }
  return bars.sort((a, b) => a.spawnT - b.spawnT || a.endT - b.endT);
}

// key_ms: "0,180,320" = milliseconds of each correct key since the first.
// Read defensively: offsets (non-decreasing) or gaps (summed), whichever
// matches typing_seconds best; the first key (offset 0) is added if missing.
export function parseKeyMs(text: string | null, start: number, typingSeconds: number | null): number[] | null {
  if (!text) {
    return null;
  }
  const values = text
    .split(",")
    .map((part) => Number(part.trim()))
    .filter((n) => Number.isFinite(n) && n >= 0);
  if (values.length === 0) {
    return null;
  }
  const asOffsets = values[0] === 0 ? values : [0, ...values];
  const sums: number[] = [0];
  for (const gap of values[0] === 0 ? values.slice(1) : values) {
    sums.push(sums[sums.length - 1] + gap);
  }
  const monotonic = asOffsets.every((v, i) => i === 0 || v >= asOffsets[i - 1]);
  let chosen = asOffsets;
  if (!monotonic) {
    chosen = sums;
  } else if (typingSeconds !== null && typingSeconds > 0) {
    const offsetError = Math.abs(asOffsets[asOffsets.length - 1] / 1000 - typingSeconds);
    const sumError = Math.abs(sums[sums.length - 1] / 1000 - typingSeconds);
    chosen = sumError + 0.001 < offsetError ? sums : asOffsets;
  }
  return chosen.map((ms) => start + ms / 1000);
}

function buildWords(events: EventRow[]): WordBar[] {
  const words: WordBar[] = [];
  for (const e of events) {
    if (e.event !== "word") {
      continue;
    }
    const end = tOf(e);
    const typingSeconds = dataNum(e, "typing_seconds");
    const logged = dataNum(e, "start_t_play");
    let start: number;
    let startSource: WordBar["startSource"];
    if (logged !== null) {
      start = logged;
      startSource = "start_t_play";
    } else if (typingSeconds !== null && typingSeconds > 0) {
      start = end - typingSeconds;
      startSource = "typing_seconds";
    } else {
      start = end;
      startSource = "instant";
    }
    start = Math.max(0, Math.min(start, end));
    const targetId = dataNum(e, "target_id");
    words.push({
      key: "w" + (e.seq ?? e.id),
      start,
      end,
      startSource,
      target: dataStr(e, "target") ?? "unknown",
      targetId: targetId !== null && targetId >= 0 ? targetId : null,
      zombieKey: null,
      word: dataStr(e, "word") ?? "",
      wordLength: dataNum(e, "word_length"),
      outcome: dataStr(e, "outcome") ?? "unknown",
      correctKeys: dataNum(e, "correct_keys") ?? 0,
      wrongKeys: dataNum(e, "wrong_keys") ?? 0,
      rockets: dataNum(e, "rockets") ?? 0,
      typingSeconds,
      wordsFinished: dataNum(e, "words_finished"),
      candidates: dataNum(e, "candidates"),
      caseSensitive: dataBool(e, "case_sensitive"),
      wordStyle: dataStr(e, "word_style"),
      frenzyOn: dataBool(e, "frenzy_on"),
      keyTimes: parseKeyMs(dataStr(e, "key_ms"), start, typingSeconds),
      event: e,
    });
  }
  return words.sort((a, b) => a.start - b.start || a.end - b.end);
}

// Which zombie each word attempt was typed at: target_id when logged, else
// the zombie of the same kind carrying that word whose life covers the attempt.
function linkWordsToZombies(words: WordBar[], zombies: ZombieBar[]): void {
  const linked = new Map<ZombieBar, WordBar[]>();
  const byId = new Map<number, ZombieBar>();
  for (const z of zombies) {
    if (z.zombieId !== null) {
      byId.set(z.zombieId, z);
    }
  }
  for (const w of words) {
    const kind = zombieKindOf(w.target);
    if (!kind) {
      continue;
    }
    let match: ZombieBar | undefined = w.targetId !== null ? byId.get(w.targetId) : undefined;
    if (!match && w.word) {
      const word = w.word.toLowerCase();
      let best: ZombieBar | undefined;
      for (const z of zombies) {
        const carries = z.words.some((candidate) => candidate.toLowerCase() === word);
        if (z.kind !== kind || !carries || z.spawnT - 1 > w.start || w.end > z.endT + 1) {
          continue;
        }
        // Prefer the zombie that ended closest after the attempt.
        if (!best || Math.abs(z.endT - w.end) < Math.abs(best.endT - w.end)) {
          best = z;
        }
      }
      match = best;
    }
    if (match) {
      w.zombieKey = match.key;
      match.attempts.push(w.key);
      linked.set(match, [...(linked.get(match) ?? []), w]);
      if (w.word && !match.words.includes(w.word)) {
        match.words.push(w.word);
      }
    }
  }
  // Each zombie's words in the order it carried them: its first word, the
  // words typed at it (by time), then the word it had at the end.
  for (const [z, attempts] of linked) {
    const firstWord = z.spawnSource === "spawn_event" ? (z.words[0] ?? null) : null;
    const typed = [...attempts].sort((a, b) => a.start - b.start).map((w) => w.word);
    z.words = uniqueWords([firstWord, ...typed, z.ended ? z.word : null, ...z.words]);
  }
}

function buildWrongKeys(events: EventRow[]): WrongKeyMark[] {
  return events
    .filter((e) => e.event === "wrong_key")
    .map((e) => {
      const targetId = dataNum(e, "target_id");
      return {
        t: tOf(e),
        typed: dataStr(e, "typed") ?? "?",
        expected: dataStr(e, "expected"),
        caseOnly: dataBool(e, "case_only"),
        stray: dataBool(e, "stray"),
        target: dataStr(e, "target"),
        targetId: targetId !== null && targetId >= 0 ? targetId : null,
        letterIndex: dataNum(e, "letter_index"),
        wordLength: dataNum(e, "word_length"),
        event: e,
      };
    });
}

function kindName(source: string | null): string {
  const kind = zombieKindOf(source);
  if (kind === "explosive") {
    return "a red zombie";
  }
  if (kind) {
    return (kind === "armored" ? "an " : "a ") + kind + " zombie";
  }
  return source === "orb" ? "an orb" : (source ?? "something");
}

function joinParts(parts: (string | null | false | undefined)[]): string {
  return parts.filter((part): part is string => !!part).join(" · ");
}

// A one-line title and a short detail for an event marker.
export function describeMarker(e: EventRow): { title: string; detail: string } {
  const d = (field: string) => dataStr(e, field);
  const n = (field: string) => dataNum(e, field);
  switch (e.event) {
    case "damage": {
      const fatal = dataBool(e, "fatal");
      return {
        title: (fatal ? "Fatal hit by " : "Hurt by ") + kindName(d("source")),
        detail: joinParts([
          "health " + (n("health") ?? "?") + " → " + (n("health_after") ?? "?"),
          n("combo") !== null && "combo " + n("combo") + " → " + (n("combo_after") ?? "?"),
          d("attacker_word") && "its word " + d("attacker_word") + (n("attacker_letters_typed") ? " (" + n("attacker_letters_typed") + " typed)" : " (not typed)"),
          dataBool(e, "ambusher") && "ambusher",
        ]),
      };
    }
    case "blast": {
      const source = d("source") ?? "?";
      const names: Record<string, string> = { red: "Red zombie blew up", barrel: "Barrel exploded", lure: "Lure bomb exploded", rpg: "RPG rocket hit" };
      return {
        title: names[source] ?? "Explosion (" + source + ")",
        detail: joinParts([
          (n("kills") ?? 0) + " kills",
          (n("explosions") ?? 0) > 1 && n("explosions") + " explosions in the chain",
          n("in_ring_now") !== null && n("in_ring_now") + " in the ring",
          (n("boss_damage") ?? 0) > 0 && "boss −" + n("boss_damage"),
          n("points") !== null && n("points") + " points",
        ]),
      };
    }
    case "power": {
      const action = d("action") ?? "?";
      const item = (d("item") ?? "").toUpperCase();
      const verbs: Record<string, string> = {
        throw: "Threw a lure",
        use: "Used " + item,
        empty: "Pressed " + item + " with none left",
        buy: "Bought " + item,
        buy_refused: "Could not buy " + item,
        tap_empty: "Tapped " + item + " with none kept",
        hold_released: "Let go before buying " + item,
        use_refused: "Could not use " + item,
        aim_cancelled: "Cancelled a lure aim",
        frenzy_end: "FRENZY ended",
      };
      return {
        title: verbs[action] ?? action + " " + item,
        detail: joinParts([
          d("reason") && "reason " + d("reason"),
          n("combo_before") !== null && "combo " + n("combo_before") + " → " + n("combo"),
          n("held_seconds") !== null && "held " + n("held_seconds") + " s",
          n("kills_during") !== null && n("kills_during") + " kills during it",
          d("via") && "via " + d("via"),
        ]),
      };
    }
    case "crate":
      return {
        title: "Opened a " + (d("kind") ?? "") + " crate",
        detail: joinParts([
          d("word") && "word " + d("word"),
          d("kind") === "health" && "health " + (n("health_before") ?? "?") + " → " + (n("health") ?? "?"),
          d("kind") !== "health" && (dataBool(e, "power_gained") ? "charge gained" : "already full"),
        ]),
      };
    case "prop":
      return { title: (d("kind") ?? "Prop").replace("_", " ") + " armed", detail: joinParts([d("word") && "word " + d("word")]) };
    case "boss_attack":
      return { title: "Boss attack: " + (d("attack") ?? "?"), detail: joinParts([dataBool(e, "angry") && "angry", n("boss_health") !== null && "boss health " + n("boss_health")]) };
    case "boss_hit":
      return { title: "Boss hurt (" + (d("cause") ?? "?") + ")", detail: joinParts(["−" + (n("damage") ?? "?"), "boss health " + (n("boss_health") ?? "?")]) };
    case "boss_heal":
      return { title: "Boss healed", detail: joinParts(["+" + (n("amount") ?? "?"), "boss health " + (n("boss_health") ?? "?")]) };
    case "boss_word":
      return { title: "Boss " + (d("part") ?? "") + " word", detail: d("word") ?? "" };
    case "quiz":
      return {
        title: "Quiz answer: " + (d("result") ?? "?"),
        detail: joinParts([d("question"), n("seconds") !== null && n("seconds") + " s"]),
      };
    case "orb":
      return { title: "Energy orb " + (d("outcome") ?? "gone"), detail: joinParts([d("word") && "word " + d("word")]) };
    case "tip":
      return { title: "Tip closed: " + (d("key") ?? "?"), detail: n("seconds_open") !== null ? "open " + n("seconds_open") + " s" : "" };
    case "hint":
      return { title: "Hint: " + (d("key") ?? "?"), detail: "" };
    case "tips_toggle":
      return { title: "Tips switched " + (dataBool(e, "on") ? "on" : "off"), detail: "" };
    case "pause":
      return { title: "Paused (" + (d("reason") ?? "?") + ")", detail: dataBool(e, "during_countdown") ? "during the countdown" : "" };
    case "resume":
      return { title: "Resumed", detail: n("paused_seconds") !== null ? "after " + n("paused_seconds") + " s" : "" };
    case "page_hidden":
      return { title: "Tab hidden", detail: "switched tab, minimised or closed" };
    case "page_visible":
      return { title: "Tab visible again", detail: n("hidden_seconds") !== null ? "away " + n("hidden_seconds") + " s" : "" };
    case "run_start":
      return { title: "Run started", detail: joinParts([n("run_number") !== null && "run #" + n("run_number"), dataBool(e, "tips_enabled") && "tips on"]) };
    case "run_end":
      return {
        title: "Run ended: " + (d("outcome") ?? "?"),
        detail: joinParts([
          n("kills") !== null && n("kills") + " kills",
          n("accuracy") !== null && Math.round(n("accuracy")!) + "% accuracy",
          n("words_per_minute") !== null && Math.round(n("words_per_minute")!) + " WPM",
        ]),
      };
    default:
      return { title: e.event, detail: "" };
  }
}

const PHASE_EVENT_TITLES: Record<string, string> = {
  ride_start: "Ride started",
  ride_end: "Ride ended",
  wave_start: "Fight started",
  wave_end: "Fight won",
  session_start: "Page loaded",
};

// "explosive" -> "Red zombie", "runner" -> "Runner zombie".
function kindTitle(kind: string | null): string {
  if (kind === "explosive") {
    return "Red zombie";
  }
  const name = kind ?? "normal";
  return name.charAt(0).toUpperCase() + name.slice(1) + " zombie";
}

// One line for ANY event (the lane events included), e.g. in the Moment panel.
export function describeEvent(e: EventRow): string {
  const d = (field: string) => dataStr(e, field);
  switch (e.event) {
    case "zombie":
      return kindTitle(d("kind")) + " " + (d("word") ?? "") + " gone: " + (d("outcome") ?? "?");
    case "zombie_spawn":
      return kindTitle(d("kind")) + " appeared: " + (d("word") ?? "");
    case "word":
      return "Typed " + (d("word") ?? "?") + " at " + (d("target") ?? "?") + ": " + (d("outcome") ?? "?");
    case "wrong_key":
      return (dataBool(e, "stray") ? "Stray key " : "Wrong key ") + "“" + (d("typed") ?? "?") + "”" + (d("expected") ? ", needed “" + d("expected") + "”" : "");
    case "sample":
      return "Heartbeat";
    default: {
      if (PHASE_EVENT_TITLES[e.event]) {
        return PHASE_EVENT_TITLES[e.event] + (e.wave !== null ? " (wave " + e.wave + ")" : "");
      }
      const { title, detail } = describeMarker(e);
      return detail ? title + " · " + detail : title;
    }
  }
}

function buildMarkers(events: EventRow[]): Marker[] {
  const markers: Marker[] = [];
  for (const e of events) {
    if (LANE_EVENTS.has(e.event) || e.event === "session_start") {
      continue;
    }
    const { title, detail } = describeMarker(e);
    markers.push({
      t: tOf(e),
      category: markerCategoryOf(e.event),
      title,
      detail,
      important: (e.event === "damage" && dataBool(e, "fatal")) || e.event === "run_end",
      weight: e.event === "blast" ? (dataNum(e, "kills") ?? 0) : 0,
      event: e,
    });
  }
  return markers;
}

function pushPoint(list: SeriesPoint[], t: number, v: number | null, event: string, note: string | null = null): void {
  if (v === null) {
    return;
  }
  const last = list[list.length - 1];
  if (last && last.t === t && last.v === v) {
    return;
  }
  list.push({ t, v, event, note });
}

function buildStateSeries(events: EventRow[]) {
  const health: SeriesPoint[] = [];
  const combo: SeriesPoint[] = [];
  const score: SeriesPoint[] = [];
  const alive: SeriesPoint[] = [];
  for (const e of events) {
    const t = tOf(e);
    if (e.event === "damage") {
      // Player state on a damage event is BEFORE the hit; *_after is after it.
      pushPoint(health, t, dataNum(e, "health"), e.event, "before the hit");
      pushPoint(health, t, dataNum(e, "health_after"), e.event, "after the hit");
      pushPoint(combo, t, dataNum(e, "combo"), e.event, "before the hit");
      pushPoint(combo, t, dataNum(e, "combo_after"), e.event, "after the hit");
    } else if (e.event === "crate" && dataStr(e, "kind") === "health") {
      pushPoint(health, t, dataNum(e, "health_before"), e.event, "before the crate");
      pushPoint(health, t, dataNum(e, "health"), e.event, "after the crate");
      pushPoint(combo, t, dataNum(e, "combo"), e.event);
    } else if (e.event === "power" && dataStr(e, "action") === "buy") {
      pushPoint(health, t, dataNum(e, "health"), e.event);
      pushPoint(combo, t, dataNum(e, "combo_before"), e.event, "before buying");
      pushPoint(combo, t, dataNum(e, "combo"), e.event, "after buying");
    } else {
      pushPoint(health, t, dataNum(e, "health"), e.event);
      pushPoint(combo, t, dataNum(e, "combo"), e.event);
    }
    pushPoint(score, t, dataNum(e, "score"), e.event);
    pushPoint(alive, t, dataNum(e, "zombies_alive"), e.event);
  }
  return { health, combo, score, alive };
}

// Zombies on the field over time, counted from the zombie bars (a step series).
function aliveFromBars(zombies: ZombieBar[]): SeriesPoint[] {
  const changes: { t: number; d: number }[] = [];
  for (const z of zombies) {
    if (z.ghost) {
      continue;
    }
    changes.push({ t: z.spawnT, d: 1 });
    if (z.ended) {
      changes.push({ t: z.endT, d: -1 });
    }
  }
  changes.sort((a, b) => a.t - b.t || a.d - b.d);
  const points: SeriesPoint[] = [{ t: 0, v: 0, event: "count", note: null }];
  let count = 0;
  for (let i = 0; i < changes.length; i++) {
    count += changes[i].d;
    // One point per instant (several zombies can leave at once in a blast).
    if (i + 1 < changes.length && changes[i + 1].t === changes[i].t) {
      continue;
    }
    pushPoint(points, changes[i].t, count, "count");
  }
  return points;
}

// The time of every correct key: key_ms when logged, else spread evenly
// from the attempt's start over typing_seconds.
function correctKeyTimes(words: WordBar[]): number[] {
  const times: number[] = [];
  for (const w of words) {
    if (w.keyTimes && w.keyTimes.length > 0) {
      times.push(...w.keyTimes);
      continue;
    }
    const n = w.correctKeys;
    if (n <= 0) {
      continue;
    }
    const span = w.typingSeconds !== null && w.typingSeconds > 0 ? w.typingSeconds : w.end - w.start;
    for (let i = 0; i < n; i++) {
      times.push(n === 1 ? w.end : w.start + (span * i) / (n - 1));
    }
  }
  return times.sort((a, b) => a - b);
}

const WPM_WINDOW = 10;

function buildWpm(words: WordBar[], samples: EventRow[], duration: number, runAverage: number | null): Timeline["wpm"] {
  const step = duration > 1800 ? 2 : 1;
  const keys = correctKeyTimes(words);
  const fromWords: SeriesPoint[] = [];
  let lo = 0;
  let hi = 0;
  for (let t = 0; t <= duration + step / 2; t += step) {
    while (hi < keys.length && keys[hi] <= t) hi++;
    while (lo < hi && keys[lo] <= t - WPM_WINDOW) lo++;
    // (keys / 5) words per (window / 60) minutes
    fromWords.push({ t, v: Math.round(((hi - lo) / 5) * (60 / WPM_WINDOW) * 10) / 10, event: "words", note: hi - lo + " correct keys in the last " + WPM_WINDOW + " s" });
  }

  // Heartbeat: cumulative correct_keys every second -> difference over the window.
  const fromSamples: SeriesPoint[] = [];
  const counted = samples
    .map((e) => ({ t: tOf(e), keys: dataNum(e, "correct_keys") }))
    .filter((s): s is { t: number; keys: number } => s.keys !== null);
  const keysAt = (t: number): number => {
    if (t <= 0) return 0;
    let value = 0;
    for (const s of counted) {
      if (s.t > t) break;
      value = s.keys;
    }
    return value;
  };
  for (const s of counted) {
    const delta = s.keys - keysAt(s.t - WPM_WINDOW);
    fromSamples.push({ t: s.t, v: Math.round((delta / 5) * (60 / WPM_WINDOW) * 10) / 10, event: "sample", note: delta + " correct keys in the last " + WPM_WINDOW + " s" });
  }
  return { windowSeconds: WPM_WINDOW, step, fromWords, fromSamples, runAverage };
}

function buildMap(events: EventRow[], zombies: ZombieBar[]): MapData {
  // Every logged position is kept (standing still too), so positionAt()
  // never interpolates across a stop.
  const path: MapPoint[] = [];
  for (const e of events) {
    const p = position(e, "player_x", "player_z");
    if (p) path.push({ ...p, t: tOf(e) });
  }
  path.sort((a, b) => a.t - b.t);

  const zombieSpawns: MapData["zombieSpawns"] = [];
  const zombieEnds: MapData["zombieEnds"] = [];
  for (const z of zombies) {
    if (z.spawnPos) zombieSpawns.push({ ...z.spawnPos, t: z.spawnT, kind: z.kind, key: z.key, word: z.words[0] ?? z.word });
    if (z.endPos) zombieEnds.push({ ...z.endPos, t: z.endT, kind: z.kind, key: z.key, word: z.word, outcome: z.outcome });
  }

  const blasts: MapData["blasts"] = [];
  const hits: MapData["hits"] = [];
  const props: MapData["props"] = [];
  for (const e of events) {
    if (e.event === "blast") {
      const p = position(e, "x", "z");
      if (p) blasts.push({ ...p, t: tOf(e), kills: dataNum(e, "kills") ?? 0, source: dataStr(e, "source") ?? "?", radius: dataNum(e, "blast_radius") });
    } else if (e.event === "damage") {
      const logged = position(e, "player_x", "player_z");
      const p = logged ?? positionAt(path, tOf(e));
      if (p) hits.push({ x: p.x, z: p.z, t: tOf(e), fatal: dataBool(e, "fatal"), source: dataStr(e, "source") ?? "?", estimated: !logged });
    } else if (e.event === "prop") {
      const p = position(e, "x", "z");
      if (p) props.push({ ...p, t: tOf(e), kind: dataStr(e, "kind") ?? "?", word: dataStr(e, "word") ?? "" });
    }
  }

  const all: { x: number; z: number }[] = [...path, ...zombieSpawns, ...zombieEnds, ...blasts, ...hits, ...props];
  let extent: MapData["extent"] = null;
  if (all.length > 0) {
    extent = { minX: Infinity, maxX: -Infinity, minZ: Infinity, maxZ: -Infinity };
    for (const p of all) {
      extent.minX = Math.min(extent.minX, p.x);
      extent.maxX = Math.max(extent.maxX, p.x);
      extent.minZ = Math.min(extent.minZ, p.z);
      extent.maxZ = Math.max(extent.maxZ, p.z);
    }
  }
  return { path, zombieSpawns, zombieEnds, blasts, hits, props, hasPositions: all.length > 0, extent };
}

// Builds every lane from the (deduplicated) events of one run.
export function buildTimeline(rawEvents: EventRow[], summary?: RunSummary | null): Timeline {
  const events = sortRunEvents(rawEvents);
  let duration = 0;
  for (const e of events) {
    duration = Math.max(duration, tOf(e));
  }
  duration = Math.max(duration, 1);

  const samples = events.filter((e) => e.event === "sample");
  const zombies = buildZombies(events, duration);
  const words = buildWords(events);
  linkWordsToZombies(words, zombies);
  const series = buildStateSeries(events);
  const runEnd = events.find((e) => e.event === "run_end");
  const runAverage = summary?.wpm ?? (runEnd ? dataNum(runEnd, "words_per_minute") : null);
  const map = buildMap(events, zombies);

  return {
    duration,
    events,
    hasSamples: samples.length > 0,
    schema: {
      // (true when there is nothing to judge: a run that ended before any
      // zombie or word is not an "older build")
      spawns: !events.some((e) => e.event === "zombie") || events.some((e) => e.event === "zombie_spawn"),
      positions: map.hasPositions,
      keyTimes: words.length === 0 || words.some((w) => w.keyTimes !== null),
      wordStarts: words.length === 0 || words.some((w) => w.startSource === "start_t_play"),
    },
    phases: buildPhases(events, duration),
    away: buildAway(events),
    zombies,
    words,
    wrongKeys: buildWrongKeys(events),
    markers: buildMarkers(events),
    health: series.health,
    combo: series.combo,
    score: series.score,
    alive: samples.length > 0 ? series.alive : aliveFromBars(zombies),
    aliveSource: samples.length > 0 ? "samples" : "bars",
    wpm: buildWpm(words, samples, duration, runAverage),
    stateEvents: events.filter((e) => dataNum(e, "health") !== null),
    map,
  };
}

// ---------------------------------------------------------------------------
// Zombie lane rows
// ---------------------------------------------------------------------------

export type ZombieRowMode = "each" | "packed";

// "each": one row per zombie in spawn order. "packed": a zombie reuses the
// first row that is free again (rows = most zombies alive at once).
export function zombieRows(zombies: ZombieBar[], mode: ZombieRowMode): { rows: number[]; count: number } {
  if (mode === "each") {
    return { rows: zombies.map((_, i) => i), count: zombies.length };
  }
  const rowEnds: number[] = [];
  const rows = zombies.map((z) => {
    let row = rowEnds.findIndex((end) => end + 0.4 <= z.spawnT);
    if (row < 0) {
      row = rowEnds.length;
      rowEnds.push(z.endT);
    } else {
      rowEnds[row] = z.endT;
    }
    return row;
  });
  return { rows, count: rowEnds.length };
}

// ---------------------------------------------------------------------------
// The state at one moment (the "Moment" panel)
// ---------------------------------------------------------------------------

// The player's position at t, interpolated between logged positions.
export function positionAt(path: MapPoint[], t: number): { x: number; z: number } | null {
  if (path.length === 0) {
    return null;
  }
  if (t <= path[0].t) return { x: path[0].x, z: path[0].z };
  const last = path[path.length - 1];
  if (t >= last.t) return { x: last.x, z: last.z };
  let lo = 0;
  let hi = path.length - 1;
  while (hi - lo > 1) {
    const mid = (lo + hi) >> 1;
    if (path[mid].t <= t) lo = mid;
    else hi = mid;
  }
  const a = path[lo];
  const b = path[hi];
  const f = b.t === a.t ? 0 : (t - a.t) / (b.t - a.t);
  return { x: a.x + (b.x - a.x) * f, z: a.z + (b.z - a.z) * f };
}

// The last element whose t is <= t (binary search on a time-sorted list).
function lastAtOrBefore<T>(list: T[], t: number, timeOf: (item: T) => number): T | null {
  let lo = 0;
  let hi = list.length - 1;
  let found: T | null = null;
  while (lo <= hi) {
    const mid = (lo + hi) >> 1;
    if (timeOf(list[mid]) <= t) {
      found = list[mid];
      lo = mid + 1;
    } else {
      hi = mid - 1;
    }
  }
  return found;
}

// The value of a step series at t (its last point at or before t).
export function valueAt(points: SeriesPoint[], t: number): number | null {
  return lastAtOrBefore(points, t, (p) => p.t)?.v ?? null;
}

export type Moment = {
  t: number;
  state: EventRow | null; // the latest event carrying player state at or before t
  health: number | null; // from the series (a hit at t counts as already taken)
  combo: number | null;
  score: number | null;
  alive: number | null;
  phase: PhaseSegment | null;
  zombiesOnField: ZombieBar[];
  typing: WordBar | null; // attempt in progress at t
  wpm: number | null;
  position: { x: number; z: number } | null;
  nearby: EventRow[]; // events within ± windowSeconds (heartbeats left out)
};

export function momentAt(timeline: Timeline, t: number, windowSeconds = 3): Moment {
  const state = lastAtOrBefore(timeline.stateEvents, t, tOf) ?? null;
  let phase: PhaseSegment | null = null;
  for (const p of timeline.phases) {
    if (p.start <= t && t <= p.end) phase = p;
  }
  return {
    t,
    state,
    health: valueAt(timeline.health, t),
    combo: valueAt(timeline.combo, t),
    score: valueAt(timeline.score, t),
    alive: valueAt(timeline.alive, t),
    phase,
    zombiesOnField: timeline.zombies.filter((z) => !z.ghost && z.spawnT <= t && t <= z.endT),
    typing: timeline.words.find((w) => w.start <= t && t <= w.end) ?? null,
    wpm: valueAt(timeline.wpm.fromWords, t),
    position: positionAt(timeline.map.path, t),
    nearby: timeline.events.filter((e) => e.event !== "sample" && Math.abs(tOf(e) - t) <= windowSeconds),
  };
}

// ---------------------------------------------------------------------------
// Events table: every event with its data fields flattened into columns
// ---------------------------------------------------------------------------

// Columns that already exist on the row (not repeated from `data`).
const DATA_DUPLICATES = new Set(["event", "seq", "event_id", "run_id", "level", "t_real", "t_play", "wave", "install_id", "session_id", "build", "batch_id"]);

// fieldOrder: data field names in catalog order (union over the event types).
export function flattenEvents(events: EventRow[], fieldOrder: string[]): { rows: Record<string, unknown>[]; columns: string[] } {
  const seen = new Set<string>();
  for (const e of events) {
    for (const key of Object.keys(e.data)) {
      if (!DATA_DUPLICATES.has(key)) seen.add(key);
    }
  }
  const dataColumns = [
    ...fieldOrder.filter((name) => seen.has(name) && name !== "state" && name !== "phase"),
    ...[...seen].filter((name) => !fieldOrder.includes(name) && name !== "state" && name !== "phase").sort(),
  ];
  const lead = ["seq", "time", "t_play", "event", "wave", "phase", "state"];
  const tail = ["t_real", "id", "event_id", "request_id", "position_in_batch", "batch_id", "received_at"];
  const rows = events.map((e) => {
    const row: Record<string, unknown> = {
      seq: e.seq,
      time: fmtT(tOf(e)),
      t_play: e.t_play,
      event: e.event,
      wave: e.wave,
      phase: e.data.phase ?? null,
      state: e.data.state ?? null,
    };
    for (const name of dataColumns) {
      row[name] = e.data[name] ?? null;
    }
    row.t_real = e.t_real;
    row.id = e.id;
    row.event_id = e.event_id;
    row.request_id = e.request_id;
    row.position_in_batch = e.position_in_batch;
    row.batch_id = e.batch_id;
    row.received_at = e.received_at;
    return row;
  });
  return { rows, columns: [...lead, ...dataColumns, ...tail] };
}
