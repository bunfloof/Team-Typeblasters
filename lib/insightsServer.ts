// Every dataset of the Insights page, computed on the server from the
// (deduplicated) events. Server only: it imports db.ts.
//
// To stay fast with thousands of events it never loads whole `data` JSON
// objects: a few queries each fetch the event types one section needs, with
// only the data fields it reads (one JSON_EXTRACT per field). Everything else
// is computed in TypeScript. Missing fields (older game builds) are skipped.
import { levelName } from "./catalog";
import { query, type Row } from "./db";
import { bool, dedupeEvents, num, str, toEventRow } from "./events";
import { buildEventWhere, parseEventFilter, type SqlWhere } from "./filters";
import { ABANDON_AFTER_SECONDS, groupByRun } from "./runSummary";
import type { EventRow } from "./types";
import { ci95, countBy, mean, median, share } from "@/components/insights/stats";
import type { FunnelStep, InsightsResponse, RunNumberScope } from "@/components/insights/types";

// ---- Loading ----

const EVENT_COLUMNS =
  "e.id, e.request_id, e.position_in_batch, e.received_at, e.event, e.event_id, e.batch_id, e.install_id, " +
  "e.session_id, e.run_id, e.seq, e.build, e.level, e.wave, e.t_play, e.t_real";
const FIELD_NAME = /^[A-Za-z0-9_]+$/;

function andWhere(base: SqlWhere, clause: string, params: unknown[] = []): SqlWhere {
  return { sql: (base.sql ? base.sql + " AND " : " WHERE ") + clause, params: [...base.params, ...params] };
}

// A JSON_EXTRACT result (JSON text such as "\"shot\"", "12.5", "true") as a JS value.
function parseExtracted(raw: unknown): unknown {
  if (raw === null || raw === undefined) {
    return undefined;
  }
  const text = Buffer.isBuffer(raw) ? raw.toString("utf8") : raw;
  if (typeof text !== "string") {
    return text;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

// Events of these types (deduplicated, oldest first) with only `fields` in data.
async function loadSlim(base: SqlWhere, events: string[], fields: string[], extraSql = ""): Promise<EventRow[]> {
  const safeFields = fields.filter((field) => FIELD_NAME.test(field));
  const extracts = safeFields.map((field) => "JSON_EXTRACT(e.data, '$." + field + "') AS `f_" + field + "`");
  const where = andWhere(base, "e.event IN (" + events.map(() => "?").join(",") + ")" + extraSql, events);
  const sql = "SELECT " + [EVENT_COLUMNS, ...extracts].join(", ") + " FROM events e" + where.sql + " ORDER BY e.id";
  const rows = await query(sql, where.params, 120000);
  const parsed = rows.map((row) => {
    const data: Record<string, unknown> = {};
    for (const field of safeFields) {
      const value = parseExtracted(row["f_" + field]);
      if (value !== undefined) {
        data[field] = value;
      }
    }
    return toEventRow({ ...row, data });
  });
  return dedupeEvents(parsed);
}

const PROGRESS_EVENTS = ["run_start", "run_end", "ride_start", "ride_end", "wave_start", "wave_end", "damage"];
const PROGRESS_FIELDS = [
  "run_number", "from_checkpoint_wave", "outcome", "words_per_minute", "accuracy", "correct_keys", "wrong_keys",
  "boss_next", "boss_fight", "health", "phase", "source", "amount", "fatal", "typing_target",
  "attacker_letters_typed", "lure_charges", "freeze_charges", "frenzy_kept", "rpg_kept", "rpg_loaded",
  "skill_energy", "frenzy_on", "freeze_on", "player_x", "player_z",
];
const RED_EVENTS = ["zombie", "blast"];
const RED_FIELDS = ["kind", "outcome", "zombie_id", "source", "kills", "in_ring_now", "in_ring_at_last_start", "in_ring_at_first_letter", "typing_seconds"];
// Of the "zombie" events only the red (explosive) ones are needed.
const RED_ONLY = " AND (e.event <> 'zombie' OR JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.kind')) = 'explosive')";
const POWER_EVENTS = ["power", "crate", "wave_reward"];
const POWER_FIELDS = ["action", "item", "reason", "kills_during", "kind", "power_gained", "energy_spent", "energy_gained", "free_reward"];
// Zombies killed (by a word or a blast): only their time is needed, to find
// the kills made while a freeze was on.
const KILL_EVENTS = ["zombie"];
const KILLS_ONLY = " AND JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.outcome')) IN ('shot', 'blast')";
const TYPING_EVENTS = ["word", "wrong_key"];
const TYPING_FIELDS = ["outcome", "word_length", "correct_keys", "typing_seconds", "case_sensitive", "typed", "expected", "stray", "case_only"];
const MISC_EVENTS = ["quiz", "tip", "pause", "resume", "page_visible"];
const MISC_FIELDS = [
  "question_index", "question", "correct_position", "chosen_position", "result", "seconds",
  "key", "seconds_open", "reason", "during_countdown", "paused_seconds", "hidden_seconds",
];

// One row per run over ALL its events (heartbeat samples included): highest
// wave, last arrival, the most skill energy held (current builds: every power
// costs energy; NULL = an older build, where FRENZY / RPG were bought with
// combo), whether a FRENZY / RPG was ever affordable that older way, and
// whether a zombie was killed while carrying a FRENZY word (a FRENZY still
// running when the run ends has no frenzy_end).
const RUN_AGGREGATE_SQL =
  "SELECT e.run_id AS run_id, MIN(e.install_id) AS install_id, MAX(e.level) AS level, MAX(e.wave) AS wave, " +
  "MAX(e.received_at) AS last_at, " +
  "MAX(CAST(JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.skill_energy')) AS DECIMAL(10, 2))) AS max_energy, " +
  "MAX(CASE WHEN JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.can_buy_frenzy')) = 'true' THEN 1 ELSE 0 END) AS can_frenzy, " +
  "MAX(CASE WHEN JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.can_buy_rpg')) = 'true' THEN 1 ELSE 0 END) AS can_rpg, " +
  "MAX(CASE WHEN e.event = 'zombie' AND JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.frenzy_word')) = 'true' " +
  "AND JSON_UNQUOTE(JSON_EXTRACT(e.data, '$.outcome')) IN ('shot', 'blast') THEN 1 ELSE 0 END) AS frenzy_kill " +
  "FROM events e";

// ---- Runs ----

type RunInfo = {
  run_id: string;
  install_id: string | null;
  level: string;
  run_number: number | null;
  outcome: string; // won, lost, restart, abandoned, playing
  wave: number;
  lastAt: string;
  canBuy: { frenzy: boolean; rpg: boolean }; // older builds: the combo could buy it
  frenzyKill: boolean; // a zombie died carrying a FRENZY word
  maxEnergy: number | null; // the most skill energy held; null = an older build (no energy)
  resumedFrom: number; // run_start's from_checkpoint_wave: > 0 = went on from a checkpoint
};

// What each power costs in skill energy (Powers.cs; at most 20 is held).
const ENERGY_COST: Record<string, number> = { lure: 6, freeze: 8, frenzy: 12, rpg: 18 };
const POWER_ITEMS = ["lure", "freeze", "frenzy", "rpg"] as const;
// How long a freeze lasts, in seconds of play (twice as long in Normal's guided wave 1).
const FREEZE_SECONDS = 4;

type Context = {
  runs: Map<string, RunInfo>; // the runs in scope
  levels: string[]; // their levels, most runs first
  multiLevel: boolean;
};

function utcMillis(text: string): number {
  return Date.parse(text.replace(" ", "T") + "Z");
}

function parseScope(value: string | null): RunNumberScope {
  return value === "first" || value === "repeat" ? value : "";
}

function inScope(scope: RunNumberScope, runNumber: number | null): boolean {
  if (scope === "first") {
    return runNumber === 1;
  }
  if (scope === "repeat") {
    return runNumber !== null && runNumber > 1;
  }
  return true;
}

// "Wave 2", "Ride 3", "Boss" (with the level name in front when the runs
// cover more than one level), and a number that sorts them in play order.
function stageOf(ctx: Context, level: string | null, wave: number | null, phase: string | null): { stage: string; order: number } {
  const w = wave ?? 0;
  const step = phase === "ride" ? 0 : phase === "boss" ? 2 : 1;
  let stage = step === 0 ? "Ride " + w : step === 2 ? "Boss" : "Wave " + w;
  if (ctx.multiLevel) {
    stage = levelName(level) + " · " + stage;
  }
  const levelIndex = Math.max(0, ctx.levels.indexOf(level ?? ""));
  return { stage, order: levelIndex * 1000 + w * 3 + step };
}

// zombie_runner -> runner, orb -> orb, anything else -> other.
const KILLERS = ["normal", "runner", "armored", "explosive", "shifter"];
function killerOf(source: string | null): string {
  if (source && source.startsWith("zombie_") && KILLERS.includes(source.slice(7))) {
    return source.slice(7);
  }
  return source === "orb" ? "orb" : "other";
}

function addTo(map: Map<string, Set<string>>, key: string, runId: string): void {
  let set = map.get(key);
  if (!set) {
    set = new Set();
    map.set(key, set);
  }
  set.add(runId);
}

function numbers(values: (number | null)[]): number[] {
  return values.filter((v): v is number => v !== null && Number.isFinite(v));
}

// ---- Q1: deaths ----

// The fatal hit of each run (the first one, in case of a duplicate).
function fatalHits(progress: EventRow[]): EventRow[] {
  const byRun = new Map<string, EventRow>();
  for (const e of progress) {
    if (e.event === "damage" && bool(e, "fatal") && e.run_id && !byRun.has(e.run_id)) {
      byRun.set(e.run_id, e);
    }
  }
  return [...byRun.values()];
}

// Runs that went on from a checkpoint (Restart after a death) start at a
// later wave, so they are left out of the funnel: they would count at the
// later waves without having passed the earlier ones.
function funnelSteps(ctx: Context, progress: EventRow[]): FunnelStep[] {
  const out: FunnelStep[] = [];
  for (const level of ctx.levels) {
    const allLevelRuns = [...ctx.runs.values()].filter((run) => run.level === level);
    const levelRuns = allLevelRuns.filter((run) => run.resumedFrom <= 0);
    const counted = new Set(levelRuns.map((run) => run.run_id));
    const reached = new Map<string, Set<string>>();
    const bossWaves = new Set<number>();
    let maxWave = 0;
    let hasRides = false;
    for (const e of progress) {
      const w = e.wave ?? 0;
      if (e.level !== level || !e.run_id || !counted.has(e.run_id) || w < 1) {
        continue;
      }
      // ride_start / ride_end carry the number of the fight the ride leads to.
      // A fight counts as reached by its wave_start, or (Normal's guided
      // wave 1 in builds before it logged one) by the end of the ride to it
      // or its wave_end.
      if (e.event === "ride_start") {
        addTo(reached, "ride-" + w, e.run_id);
        if (bool(e, "boss_next")) bossWaves.add(w);
        hasRides = true;
      } else if (e.event === "ride_end") {
        addTo(reached, "ride-" + w, e.run_id);
        addTo(reached, "fight-" + w, e.run_id);
        hasRides = true;
      } else if (e.event === "wave_start" || e.event === "wave_end") {
        addTo(reached, "fight-" + w, e.run_id);
        if (bool(e, "boss_fight")) bossWaves.add(w);
      } else {
        continue;
      }
      maxWave = Math.max(maxWave, w);
    }
    const steps: { step: string; runs: number }[] = [];
    for (let w = 1; w <= maxWave; w++) {
      const boss = bossWaves.has(w);
      if (hasRides) {
        steps.push({ step: boss ? "Ride to boss" : "Ride " + w, runs: reached.get("ride-" + w)?.size ?? 0 });
      }
      steps.push({ step: boss ? "Boss" : "Wave " + w, runs: reached.get("fight-" + w)?.size ?? 0 });
    }
    if (level !== "Endless") {
      steps.push({ step: "Won", runs: levelRuns.filter((run) => run.outcome === "won").length }); // (Endless cannot be won)
    }
    const resumed = allLevelRuns.length - levelRuns.length;
    steps.forEach((s, i) => {
      out.push({
        level,
        level_name: levelName(level),
        order: i,
        step: s.step,
        runs: s.runs,
        share: share(s.runs, levelRuns.length),
        level_runs: levelRuns.length,
        lost_since_previous: (i === 0 ? levelRuns.length : steps[i - 1].runs) - s.runs,
        resumed_runs: resumed,
      });
    });
  }
  return out;
}

function deathsSection(ctx: Context, progress: EventRow[], fatals: EventRow[]): InsightsResponse["deaths"] {
  const runs = [...ctx.runs.values()];
  const deathRuns = new Set(fatals.map((e) => e.run_id as string));
  for (const run of runs) {
    if (run.outcome === "lost") deathRuns.add(run.run_id);
  }
  const attackerKnown = fatals.filter((e) => num(e, "attacker_letters_typed") !== null);
  const typingAttacker = attackerKnown.filter((e) => (num(e, "attacker_letters_typed") ?? 0) > 0).length;

  // Deaths by stage and killer.
  const stageCounts = new Map<string, { stage: string; order: number; killer: string; deaths: number }>();
  for (const e of fatals) {
    const { stage, order } = stageOf(ctx, e.level, e.wave, str(e, "phase"));
    const killer = killerOf(str(e, "source"));
    const key = stage + "|" + killer;
    const entry = stageCounts.get(key) ?? { stage, order, killer, deaths: 0 };
    entry.deaths += 1;
    stageCounts.set(key, entry);
  }

  // What the player was typing at the fatal hit.
  const typing = new Map<string, { target: string; deaths: number; typing_attacker: number }>();
  for (const e of fatals) {
    const target = str(e, "typing_target") ?? "unknown";
    const entry = typing.get(target) ?? { target, deaths: 0, typing_attacker: 0 };
    entry.deaths += 1;
    if ((num(e, "attacker_letters_typed") ?? 0) > 0) entry.typing_attacker += 1;
    typing.set(target, entry);
  }

  // Health at the start of each fight: fights the run cleared vs died in.
  const cleared = new Set(progress.filter((e) => e.event === "wave_end").map((e) => e.run_id + "|" + e.wave));
  const diedIn = new Set(fatals.filter((e) => str(e, "phase") !== "ride").map((e) => e.run_id + "|" + e.wave));
  const healthPoints: InsightsResponse["deaths"]["healthPoints"] = [];
  for (const e of progress) {
    const health = num(e, "health");
    if (e.event !== "wave_start" || health === null || !e.run_id) {
      continue;
    }
    const key = e.run_id + "|" + e.wave;
    const result = cleared.has(key) ? "cleared" : diedIn.has(key) ? "died" : null;
    if (!result) {
      continue; // restarted or left during the fight
    }
    const { stage, order } = stageOf(ctx, e.level, e.wave, bool(e, "boss_fight") ? "boss" : "fight");
    healthPoints.push({ run_id: e.run_id, fight: stage, order, result, health });
  }
  const summaryGroups = new Map<string, { fight: string; order: number; result: "cleared" | "died"; values: number[] }>();
  for (const p of healthPoints) {
    const key = p.fight + "|" + p.result;
    const group = summaryGroups.get(key) ?? { fight: p.fight, order: p.order, result: p.result, values: [] };
    group.values.push(p.health);
    summaryGroups.set(key, group);
  }

  // Every hit, by source.
  const sources = new Map<string, { source: string; hits: number; damage: number; fatal: number }>();
  for (const e of progress) {
    if (e.event !== "damage") continue;
    const source = str(e, "source") ?? "unknown";
    const entry = sources.get(source) ?? { source, hits: 0, damage: 0, fatal: 0 };
    entry.hits += 1;
    entry.damage += num(e, "amount") ?? 0;
    if (bool(e, "fatal")) entry.fatal += 1;
    sources.set(source, entry);
  }

  const deathPositions: InsightsResponse["deaths"]["deathPositions"] = [];
  for (const e of fatals) {
    const x = num(e, "player_x");
    const z = num(e, "player_z");
    if (x !== null && z !== null) {
      deathPositions.push({ run_id: e.run_id as string, level: e.level ?? "", x, z, killer: killerOf(str(e, "source")), stage: stageOf(ctx, e.level, e.wave, str(e, "phase")).stage });
    }
  }

  return {
    tiles: {
      runs: runs.length,
      deaths: deathRuns.size,
      deathRate: runs.length > 0 ? deathRuns.size / runs.length : null,
      medianWave: median(runs.map((run) => run.wave)),
      typingAttacker: attackerKnown.length > 0 ? typingAttacker / attackerKnown.length : null,
      typingAttackerN: attackerKnown.length,
    },
    funnel: funnelSteps(ctx, progress),
    byStage: [...stageCounts.values()].sort((a, b) => a.order - b.order),
    typingAtDeath: [...typing.values()].sort((a, b) => b.deaths - a.deaths),
    healthPoints,
    healthSummary: [...summaryGroups.values()]
      .sort((a, b) => a.order - b.order)
      .map((g) => ({ fight: g.fight, order: g.order, result: g.result, runs: g.values.length, mean: mean(g.values) as number, median: median(g.values) as number })),
    damageSources: [...sources.values()].sort((a, b) => b.hits - a.hits),
    deathPositions,
  };
}

// ---- Q2: red zombies ----

function redOutcomeKey(outcome: string | null): string {
  switch (outcome) {
    case "blast":
    case "bite":
    case "run_over":
    case "shot":
      return outcome;
    case "rode_past":
    case "never_seen":
      return "rode_past";
    default:
      return "other";
  }
}

const RED_OUTCOME_ORDER = ["word", "chained", "bite", "rode_past", "run_over", "shot", "other"];

function redSection(ctx: Context, red: EventRow[]): InsightsResponse["red"] {
  const zombies = red.filter((e) => e.event === "zombie" && str(e, "kind") === "explosive");
  const blasts = red.filter((e) => e.event === "blast");
  const redBlasts = blasts.filter((e) => str(e, "source") === "red");

  // A red zombie with outcome "blast" was blown up by its own word (it has a
  // red "blast" event) or chained by another explosion. With zombie_id (new
  // builds) the two are matched exactly; before that, per run, as many of
  // them as there were red blasts count as "its word".
  const blastsByRun = groupByRun(redBlasts);
  const outcomes = new Map<string, number>();
  for (const [runId, list] of groupByRun(zombies)) {
    const runBlasts = blastsByRun.get(runId) ?? [];
    const ownIds = new Set(numbers(runBlasts.map((b) => num(b, "zombie_id"))));
    let ownWithoutId = runBlasts.filter((b) => num(b, "zombie_id") === null).length;
    for (const z of list) {
      let key = redOutcomeKey(str(z, "outcome"));
      if (key === "blast") {
        const id = num(z, "zombie_id");
        if (id !== null && ownIds.has(id)) {
          key = "word";
        } else if (id === null && ownWithoutId > 0) {
          ownWithoutId -= 1;
          key = "word";
        } else {
          key = "chained";
        }
      }
      outcomes.set(key, (outcomes.get(key) ?? 0) + 1);
    }
  }

  const scatter: InsightsResponse["red"]["blasts"] = [];
  for (const b of redBlasts) {
    const atStart = num(b, "in_ring_at_last_start") ?? num(b, "in_ring_at_first_letter");
    const atBlast = num(b, "in_ring_now");
    if (atStart === null || atBlast === null || atStart < 0 || atBlast < 0 || !b.run_id) {
      continue;
    }
    scatter.push({
      run_id: b.run_id,
      at_start: atStart,
      at_blast: atBlast,
      kills: num(b, "kills") ?? 0,
      run_number: ctx.runs.get(b.run_id)?.run_number ?? null,
      typing_seconds: num(b, "typing_seconds"),
    });
  }

  // Learning: kills per red blast by the player's run number.
  const byNumber = new Map<string, number[]>();
  for (const b of redBlasts) {
    const runNumber = b.run_id ? ctx.runs.get(b.run_id)?.run_number ?? null : null;
    const kills = num(b, "kills");
    if (runNumber === null || kills === null) continue;
    const bucket = runNumber >= 4 ? "4+" : String(runNumber);
    byNumber.set(bucket, [...(byNumber.get(bucket) ?? []), kills]);
  }

  const bySource = new Map<string, number[]>();
  for (const b of blasts) {
    const source = str(b, "source") ?? "unknown";
    bySource.set(source, [...(bySource.get(source) ?? []), num(b, "kills") ?? 0]);
  }
  const sourceOrder = ["red", "barrel", "lure", "rpg"];

  return {
    redZombies: zombies.length,
    outcomes: RED_OUTCOME_ORDER.filter((key) => (outcomes.get(key) ?? 0) > 0).map((key) => ({
      outcome: key,
      zombies: outcomes.get(key) ?? 0,
      share: share(outcomes.get(key) ?? 0, zombies.length),
    })),
    blasts: scatter,
    redBlasts: redBlasts.length,
    killsPerRedBlast: numbers(redBlasts.map((b) => num(b, "kills"))),
    byRunNumber: ["1", "2", "3", "4+"]
      .filter((bucket) => byNumber.has(bucket))
      .map((bucket) => {
        const values = byNumber.get(bucket) ?? [];
        return { run_number: bucket, blasts: values.length, mean_kills: mean(values) ?? 0, ci95: ci95(values) };
      }),
    bySource: [...bySource.entries()]
      .sort((a, b) => {
        const ia = sourceOrder.indexOf(a[0]);
        const ib = sourceOrder.indexOf(b[0]);
        return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
      })
      .map(([source, values]) => ({
        source,
        blasts: values.length,
        mean_kills: mean(values) ?? 0,
        total_kills: values.reduce((sum, v) => sum + v, 0),
        ci95: ci95(values),
      })),
  };
}

// ---- Q3: powers and weapons ----
//
// Current builds: every power (lure, freeze, FRENZY, RPG) costs skill energy,
// earned by kills and energy crates (Endless wave rewards can also give a
// free use). Older builds (runs with no skill_energy): lure and freeze
// charges came from crates, FRENZY / RPG were bought with combo; their charts
// are kept for that data.

const PRESS_ACTIONS = ["tap_empty", "hold_released", "buy_refused", "buy"]; // older builds
const TRY_ACTIONS = ["use", "throw", "use_refused", "aim_cancelled"]; // current builds: the key was pressed

function isUse(e: EventRow, item: string): boolean {
  if (e.event !== "power" || str(e, "item") !== item) {
    return false;
  }
  const action = str(e, "action");
  return item === "lure" ? action === "throw" : action === "use";
}

// A charge or weapon gained: a crate charge or a bought weapon (older builds),
// or a free use from a wave reward.
function isGain(e: EventRow, item: string): boolean {
  if (e.event === "power" && str(e, "item") === item && str(e, "action") === "reward") {
    return true;
  }
  if (item === "lure" || item === "freeze") {
    return e.event === "crate" && str(e, "kind") === item && bool(e, "power_gained");
  }
  return e.event === "power" && str(e, "item") === item && str(e, "action") === "buy";
}

// Could the player have used this power right then (player state of the event)?
function couldUse(e: EventRow, item: string): boolean {
  const energy = num(e, "skill_energy");
  const canPay = energy !== null && energy >= ENERGY_COST[item];
  switch (item) {
    case "lure":
      return (num(e, "lure_charges") ?? 0) > 0 || canPay;
    case "freeze":
      return ((num(e, "freeze_charges") ?? 0) > 0 || canPay) && !bool(e, "freeze_on");
    case "frenzy":
      return ((num(e, "frenzy_kept") ?? 0) > 0 || canPay) && !bool(e, "frenzy_on");
    default:
      return (num(e, "rpg_kept") ?? 0) > 0 || bool(e, "rpg_loaded") || canPay;
  }
}

// Runs where a zombie was killed while a freeze was on (a kill within the
// freeze's seconds of play after it was used).
function freezeKillRuns(powers: EventRow[], kills: EventRow[]): Set<string> {
  const killTimes = new Map<string, number[]>();
  for (const k of kills) {
    if (k.run_id && k.t_play !== null) {
      killTimes.set(k.run_id, [...(killTimes.get(k.run_id) ?? []), k.t_play]);
    }
  }
  const out = new Set<string>();
  for (const e of powers) {
    if (!e.run_id || e.t_play === null || !isUse(e, "freeze")) continue;
    const start = e.t_play;
    const seconds = e.level === "Normal" && e.wave === 1 ? FREEZE_SECONDS * 2 : FREEZE_SECONDS;
    if ((killTimes.get(e.run_id) ?? []).some((t) => t >= start && t <= start + seconds)) {
      out.add(e.run_id);
    }
  }
  return out;
}

function powersSection(ctx: Context, fatals: EventRow[], powers: EventRow[], blasts: EventRow[], kills: EventRow[]): InsightsResponse["powers"] {
  const energyRunIds = new Set([...ctx.runs.values()].filter((run) => run.maxEnergy !== null).map((run) => run.run_id));
  const legacyRunList = [...ctx.runs.values()].filter((run) => run.maxEnergy === null);
  const legacyRunIds = new Set(legacyRunList.map((run) => run.run_id));
  const energyPowers = powers.filter((e) => !!e.run_id && energyRunIds.has(e.run_id));
  const legacyPowers = powers.filter((e) => !!e.run_id && legacyRunIds.has(e.run_id));
  const energyRuns = energyRunIds.size;
  const legacyRuns = legacyRunIds.size;

  // What the player could still have used when the fatal hit landed (state
  // BEFORE the hit): a charge or kept weapon, or enough energy for it.
  const withState = fatals.filter((e) => num(e, "lure_charges") !== null || num(e, "skill_energy") !== null);
  const unusedAtDeath = POWER_ITEMS.map((item) => {
    const deaths = withState.filter((e) => couldUse(e, item)).length;
    return { item: item as string, deaths, share: share(deaths, withState.length) };
  });
  const anyUnused = withState.filter((e) => POWER_ITEMS.some((item) => couldUse(e, item))).length;
  unusedAtDeath.push({ item: "any", deaths: anyUnused, share: share(anyUnused, withState.length) });

  // Current builds, per power: could afford -> pressed its key -> used -> got kills.
  const killedBy: Record<string, Set<string>> = {
    lure: new Set(),
    freeze: freezeKillRuns(energyPowers, kills),
    frenzy: new Set(),
    rpg: new Set(),
  };
  for (const b of blasts) {
    const source = str(b, "source");
    if (b.run_id && energyRunIds.has(b.run_id) && (source === "lure" || source === "rpg") && (num(b, "kills") ?? 0) > 0) {
      killedBy[source].add(b.run_id);
    }
  }
  for (const e of energyPowers) {
    if (str(e, "action") === "frenzy_end" && (num(e, "kills_during") ?? 0) > 0) killedBy.frenzy.add(e.run_id as string);
  }
  for (const run of ctx.runs.values()) {
    if (run.frenzyKill && energyRunIds.has(run.run_id)) killedBy.frenzy.add(run.run_id);
  }
  const powerFunnel: InsightsResponse["powers"]["powerFunnel"] = [];
  const energyUse: InsightsResponse["powers"]["energyUse"] = [];
  for (const item of POWER_ITEMS) {
    const afford = new Set([...ctx.runs.values()].filter((run) => run.maxEnergy !== null && run.maxEnergy >= ENERGY_COST[item]).map((run) => run.run_id));
    const pressed = new Set<string>();
    const usedRuns = new Set<string>();
    let uses = 0;
    let freeUses = 0;
    let spent = 0;
    for (const e of energyPowers) {
      if (str(e, "item") !== item || e.event !== "power") continue;
      const runId = e.run_id as string;
      const action = str(e, "action");
      if (action === "reward") afford.add(runId); // a free use from a wave reward
      if (action && TRY_ACTIONS.includes(action)) pressed.add(runId);
      if (isUse(e, item)) {
        usedRuns.add(runId);
        afford.add(runId); // a used power was affordable
        uses += 1;
        if (bool(e, "free_reward")) freeUses += 1;
        spent += num(e, "energy_spent") ?? 0;
      }
    }
    const steps: [string, number][] = [
      ["Could afford it", afford.size],
      ["Pressed its key", pressed.size],
      ["Used it", usedRuns.size],
      ["Got kills with it", killedBy[item].size],
    ];
    steps.forEach(([step, runs], order) => powerFunnel.push({ item, order, step, runs, share: share(runs, energyRuns) }));
    energyUse.push({
      item,
      uses,
      free_uses: freeUses,
      energy_spent: spent,
      runs_used: usedRuns.size,
      paid_per_run: share(uses - freeUses, energyRuns),
      free_per_run: share(freeUses, energyRuns),
    });
  }

  // Energy that came in from crates and wave rewards (kills give +1 each,
  // which is not logged as its own event).
  const energyIn = { crates: 0, crate_energy: 0, crates_when_full: 0, rewards: 0, reward_energy: 0 };
  for (const e of energyPowers) {
    if (e.event === "crate" && str(e, "kind") === "energy") {
      const gained = num(e, "energy_gained") ?? 0;
      energyIn.crates += 1;
      energyIn.crate_energy += gained;
      if (gained <= 0) energyIn.crates_when_full += 1;
    } else if (e.event === "wave_reward") {
      energyIn.rewards += 1;
      energyIn.reward_energy += num(e, "energy_gained") ?? 0;
    }
  }

  // Older builds: lure / freeze charges gained from crates vs used.
  const gainedUsed = (["lure", "freeze"] as const).map((item) => {
    const gainedRuns = new Set<string>();
    const usedRuns = new Set<string>();
    let gained = 0;
    let used = 0;
    let whenFull = 0;
    for (const e of legacyPowers) {
      if (e.event === "crate" && str(e, "kind") === item) {
        if (bool(e, "power_gained")) {
          gained += 1;
          if (e.run_id) gainedRuns.add(e.run_id);
        } else {
          whenFull += 1;
        }
      } else if (isUse(e, item)) {
        used += 1;
        if (e.run_id) usedRuns.add(e.run_id);
      }
    }
    return {
      item,
      runs: legacyRuns,
      gained,
      used,
      gained_per_run: share(gained, legacyRuns),
      used_per_run: share(used, legacyRuns),
      runs_gained: gainedRuns.size,
      runs_used: usedRuns.size,
      crates_when_full: whenFull,
    };
  });

  // Older builds: combo-weapon funnel: affordable -> key pressed -> bought -> used -> kills.
  const weaponFunnel: InsightsResponse["powers"]["weaponFunnel"] = [];
  for (const weapon of ["frenzy", "rpg"] as const) {
    const pressed = new Set<string>();
    const bought = new Set<string>();
    const usedRuns = new Set<string>();
    const killed = new Set<string>();
    for (const e of legacyPowers) {
      if (e.event !== "power" || !e.run_id) continue;
      const action = str(e, "action");
      const item = str(e, "item");
      if (item === weapon && action && PRESS_ACTIONS.includes(action)) pressed.add(e.run_id);
      if (item === weapon && action === "buy") bought.add(e.run_id);
      if (item === weapon && action === "use") usedRuns.add(e.run_id);
      if (weapon === "frenzy" && action === "frenzy_end" && (num(e, "kills_during") ?? 0) > 0) killed.add(e.run_id);
    }
    if (weapon === "frenzy") {
      for (const run of legacyRunList) {
        if (run.frenzyKill) killed.add(run.run_id);
      }
    }
    if (weapon === "rpg") {
      for (const b of blasts) {
        if (b.run_id && legacyRunIds.has(b.run_id) && str(b, "source") === "rpg" && (num(b, "kills") ?? 0) > 0) killed.add(b.run_id);
      }
    }
    const affordable = legacyRunList.filter((run) => run.canBuy[weapon]).length;
    const steps: [string, number][] = [
      ["Could afford it", affordable],
      ["Pressed its key", pressed.size],
      ["Bought it", bought.size],
      ["Used it", usedRuns.size],
      ["Got kills with it", killed.size],
    ];
    steps.forEach(([step, runs], order) => weaponFunnel.push({ weapon, order, step, runs, share: share(runs, legacyRuns) }));
  }

  // A press that did nothing: use_refused (current builds) or buy_refused (older).
  const refusalCounts = new Map<string, { item: string; reason: string; count: number }>();
  for (const e of powers) {
    const action = str(e, "action");
    if (e.event !== "power" || (action !== "buy_refused" && action !== "use_refused")) continue;
    const item = str(e, "item") ?? "unknown";
    const reason = str(e, "reason") ?? "unknown";
    const entry = refusalCounts.get(item + "|" + reason) ?? { item, reason, count: 0 };
    entry.count += 1;
    refusalCounts.set(item + "|" + reason, entry);
  }

  // Seconds from the first gain of an item to its first use, per run.
  const gainToUse: InsightsResponse["powers"]["gainToUse"] = [];
  for (const [runId, list] of groupByRun(powers)) {
    for (const item of POWER_ITEMS) {
      const gainIndex = list.findIndex((e) => isGain(e, item));
      if (gainIndex < 0) continue;
      const gain = list[gainIndex];
      const use = list.slice(gainIndex + 1).find((e) => isUse(e, item));
      const seconds = use && use.t_play !== null && gain.t_play !== null ? Math.max(0, use.t_play - gain.t_play) : null;
      gainToUse.push({ item, run_id: runId, seconds });
    }
  }

  return {
    deaths: withState.length,
    energyRuns,
    legacyRuns,
    unusedAtDeath,
    powerFunnel,
    energyUse,
    energyIn,
    gainedUsed,
    weaponFunnel,
    refusals: [...refusalCounts.values()].sort((a, b) => b.count - a.count),
    gainToUse,
  };
}

// ---- Typing ----

function typingSection(progress: EventRow[], typing: EventRow[], ctx: Context): InsightsResponse["typing"] {
  const runEnds = progress.filter((e) => e.event === "run_end" && e.run_id);
  const words = typing.filter((e) => e.event === "word");
  const wrong = typing.filter((e) => e.event === "wrong_key");

  // Letters per second for finished words: n correct keys = n-1 gaps.
  const speedGroups = new Map<string, { word_length: number; case: "plain" | "case-sensitive"; values: number[] }>();
  for (const w of words) {
    const keys = num(w, "correct_keys");
    const seconds = num(w, "typing_seconds");
    const length = num(w, "word_length");
    if (str(w, "outcome") !== "finished" || keys === null || keys < 2 || seconds === null || seconds <= 0 || length === null) {
      continue;
    }
    const caseKind = bool(w, "case_sensitive") ? "case-sensitive" : "plain";
    const key = length + "|" + caseKind;
    const group = speedGroups.get(key) ?? { word_length: length, case: caseKind, values: [] };
    group.values.push((keys - 1) / seconds);
    speedGroups.set(key, group);
  }

  let stray = 0;
  let caseOnly = 0;
  let midWord = 0;
  const pairs = new Map<string, { expected: string; typed: string; count: number; share: number; case_only: boolean }>();
  for (const e of wrong) {
    // A "stray" key with case_only is the first letter of a case-sensitive
    // word in the wrong case: a case mistake, not a key that matches nothing.
    const isCase = bool(e, "case_only");
    if (bool(e, "stray") && !isCase) {
      stray += 1;
      continue;
    }
    if (isCase) caseOnly += 1;
    else midWord += 1;
    const expected = str(e, "expected");
    const typed = str(e, "typed");
    if (expected === null || typed === null) continue;
    // Letters in capitals (case did not matter), except case-only slips.
    const ex = isCase ? expected : expected.toUpperCase();
    const ty = isCase ? typed : typed.toUpperCase();
    const key = ex + "\u0000" + ty;
    const entry = pairs.get(key) ?? { expected: ex, typed: ty, count: 0, share: 0, case_only: isCase };
    entry.count += 1;
    pairs.set(key, entry);
  }
  const pairTotal = [...pairs.values()].reduce((sum, p) => sum + p.count, 0);

  return {
    // A run with no key pressed has no speed or accuracy (the game reports
    // 0 WPM and 100% for it), so it is left out of those charts.
    runs: runEnds.map((e) => {
      const typed = (num(e, "correct_keys") ?? 0) + (num(e, "wrong_keys") ?? 0);
      return {
        run_id: e.run_id as string,
        run_number: ctx.runs.get(e.run_id as string)?.run_number ?? num(e, "run_number"),
        wpm: typed > 0 ? num(e, "words_per_minute") : null,
        accuracy: typed > 0 ? num(e, "accuracy") : null,
      };
    }),
    attempts: [...countBy(words, (w) => str(w, "outcome") ?? "unknown").entries()]
      .map(([outcome, count]) => ({ outcome, count }))
      .sort((a, b) => b.count - a.count),
    speedByLength: [...speedGroups.values()]
      .sort((a, b) => a.word_length - b.word_length || a.case.localeCompare(b.case))
      .map((g) => ({ word_length: g.word_length, case: g.case, words: g.values.length, mean: mean(g.values) as number, median: median(g.values) as number, ci95: ci95(g.values) })),
    wrongKeys: { total: wrong.length, stray, midWord, caseOnly },
    wrongPairs: [...pairs.values()]
      .sort((a, b) => b.count - a.count)
      .slice(0, 60)
      .map((p) => ({ ...p, share: share(p.count, pairTotal) })),
    correctKeys: runEnds.reduce((sum, e) => sum + (num(e, "correct_keys") ?? 0), 0),
    wrongKeysRunEnd: runEnds.reduce((sum, e) => sum + (num(e, "wrong_keys") ?? 0), 0),
  };
}

// ---- Boss quiz, tips, pauses ----

function moreSection(misc: EventRow[]): InsightsResponse["more"] {
  const quizzes = misc.filter((e) => e.event === "quiz");
  const questions = new Map<string, { question_index: number | null; question: string; results: string[]; seconds: number[] }>();
  const positions = new Map<string, { correct_position: number; chosen_position: number; count: number }>();
  for (const q of quizzes) {
    const index = num(q, "question_index");
    const text = str(q, "question") ?? "";
    const key = index !== null ? "i" + index : "t" + text;
    const group = questions.get(key) ?? { question_index: index, question: text, results: [], seconds: [] };
    group.results.push(str(q, "result") ?? "unknown");
    const seconds = num(q, "seconds");
    if (seconds !== null) group.seconds.push(seconds);
    questions.set(key, group);

    const correct = num(q, "correct_position");
    const chosen = num(q, "chosen_position");
    if (correct !== null && chosen !== null) {
      const pk = correct + "|" + chosen;
      const entry = positions.get(pk) ?? { correct_position: correct, chosen_position: chosen, count: 0 };
      entry.count += 1;
      positions.set(pk, entry);
    }
  }

  const tipGroups = new Map<string, number[]>();
  const tipPoints: { key: string; seconds: number }[] = [];
  for (const e of misc) {
    const seconds = num(e, "seconds_open");
    if (e.event !== "tip" || seconds === null) continue;
    const key = str(e, "key") ?? "unknown";
    tipGroups.set(key, [...(tipGroups.get(key) ?? []), seconds]);
    tipPoints.push({ key, seconds });
  }

  // Pair each pause with the resume that ended it (same session, next in
  // seq order; a second pause during the countdown belongs to the first).
  const pauseGroups = new Map<string, { pauses: number; paused: number[] }>();
  const bySession = new Map<string, EventRow[]>();
  for (const e of misc) {
    if ((e.event === "pause" || e.event === "resume") && e.session_id) {
      bySession.set(e.session_id, [...(bySession.get(e.session_id) ?? []), e]);
    }
  }
  for (const list of bySession.values()) {
    list.sort((a, b) => (a.seq ?? 0) - (b.seq ?? 0));
    let open: string | null = null;
    for (const e of list) {
      if (e.event === "pause") {
        const reason = str(e, "reason") ?? "unknown";
        const group = pauseGroups.get(reason) ?? { pauses: 0, paused: [] };
        group.pauses += 1;
        pauseGroups.set(reason, group);
        // Only a pause during the resume countdown continues the open one; a
        // pause left open (Restart from the pause panel) is closed by the next.
        if (open === null || !bool(e, "during_countdown")) open = reason;
      } else if (open !== null) {
        const seconds = num(e, "paused_seconds");
        if (seconds !== null) pauseGroups.get(open)?.paused.push(seconds);
        open = null;
      }
    }
  }

  return {
    quiz: [...questions.values()]
      .sort((a, b) => (a.question_index ?? 9999) - (b.question_index ?? 9999))
      .map((g) => {
        const right = g.results.filter((r) => r === "right").length;
        return {
          question_index: g.question_index,
          question: g.question,
          asked: g.results.length,
          right,
          wrong: g.results.filter((r) => r === "wrong").length,
          timeout: g.results.filter((r) => r === "timeout").length,
          share_right: share(right, g.results.length),
          mean_seconds: mean(g.seconds),
        };
      }),
    quizPositions: [...positions.values()],
    tips: [...tipGroups.entries()]
      .map(([key, values]) => ({
        key,
        closed: values.length,
        median_seconds: median(values) as number,
        share_within_2s: share(values.filter((v) => v <= 2).length, values.length),
      }))
      .sort((a, b) => b.closed - a.closed),
    tipPoints,
    pauses: [...pauseGroups.entries()]
      .map(([reason, g]) => ({ reason, pauses: g.pauses, median_paused_seconds: median(g.paused), with_resume: g.paused.length }))
      .sort((a, b) => b.pauses - a.pauses),
    hiddenSeconds: numbers(misc.filter((e) => e.event === "page_visible").map((e) => num(e, "hidden_seconds"))),
  };
}

// ---- Entry point ----

// Everything the computation needs, as loaded from the database.
export type InsightsInput = {
  now: string; // database UTC_TIMESTAMP
  builds: string[];
  runRows: Row[]; // RUN_AGGREGATE_SQL rows
  progress: EventRow[];
  red: EventRow[];
  powers: EventRow[];
  kills: EventRow[]; // zombies killed, time only (KILL_EVENTS)
  typing: EventRow[];
  misc: EventRow[];
};

// searchParams: the global filters (includeTest, level), build, and
// runNumber=first|repeat; any other lib/filters.ts parameter also applies.
export async function computeInsights(searchParams: URLSearchParams): Promise<InsightsResponse> {
  const filter = parseEventFilter(searchParams);
  const base = buildEventWhere({ ...filter, events: [] });
  const noBuild = buildEventWhere({ ...filter, events: [], build: "" });
  const runWhere = andWhere(base, "e.run_id IS NOT NULL AND e.run_id <> ''");

  const [nowRows, buildRows, runRows, progress, red, powers, kills, typing, misc] = await Promise.all([
    query<{ now: string }>("SELECT UTC_TIMESTAMP(6) AS now"),
    query<{ build: string }>(
      "SELECT DISTINCT e.build AS build FROM events e" + andWhere(noBuild, "e.build IS NOT NULL").sql + " ORDER BY e.build",
      noBuild.params,
    ),
    query(RUN_AGGREGATE_SQL + runWhere.sql + " GROUP BY e.run_id", runWhere.params, 120000),
    loadSlim(base, PROGRESS_EVENTS, PROGRESS_FIELDS),
    loadSlim(base, RED_EVENTS, RED_FIELDS, RED_ONLY),
    loadSlim(base, POWER_EVENTS, POWER_FIELDS),
    loadSlim(base, KILL_EVENTS, [], KILLS_ONLY),
    loadSlim(base, TYPING_EVENTS, TYPING_FIELDS),
    loadSlim(base, MISC_EVENTS, MISC_FIELDS),
  ]);
  const input: InsightsInput = {
    now: String(nowRows[0]?.now ?? ""),
    builds: buildRows.map((row) => row.build),
    runRows,
    progress,
    red,
    powers,
    kills,
    typing,
    misc,
  };
  return buildInsights(input, parseScope(searchParams.get("runNumber")), filter.build);
}

// The computation itself (no database), on events loaded as above.
export function buildInsights(input: InsightsInput, scope: RunNumberScope, build: string): InsightsResponse {
  // Every run: run number and outcome from run_start / run_end; a run with
  // no run_end is abandoned (or still playing if its last event is recent).
  const nowMs = utcMillis(input.now);
  const allRuns = new Map<string, RunInfo>();
  for (const row of input.runRows) {
    allRuns.set(String(row.run_id), {
      run_id: String(row.run_id),
      install_id: (row.install_id as string | null) ?? null,
      level: String(row.level ?? ""),
      run_number: null,
      outcome: "",
      wave: Number(row.wave ?? 0),
      lastAt: String(row.last_at ?? ""),
      canBuy: { frenzy: Number(row.can_frenzy) === 1, rpg: Number(row.can_rpg) === 1 },
      frenzyKill: Number(row.frenzy_kill) === 1,
      maxEnergy: row.max_energy === null || row.max_energy === undefined ? null : Number(row.max_energy),
      resumedFrom: 0,
    });
  }
  for (const e of input.progress) {
    const run = e.run_id ? allRuns.get(e.run_id) : undefined;
    if (!run || (e.event !== "run_start" && e.event !== "run_end")) continue;
    const runNumber = num(e, "run_number");
    if (runNumber !== null) run.run_number = runNumber;
    if (e.event === "run_start") run.resumedFrom = num(e, "from_checkpoint_wave") ?? 0;
    if (e.event === "run_end" && str(e, "outcome")) run.outcome = str(e, "outcome") as string;
  }
  for (const run of allRuns.values()) {
    if (!run.outcome) {
      const age = nowMs - utcMillis(run.lastAt);
      run.outcome = Number.isFinite(age) && age < ABANDON_AFTER_SECONDS * 1000 ? "playing" : "abandoned";
    }
  }

  // The runs in scope (all / first / repeat) and their events.
  const runs = new Map([...allRuns].filter(([, run]) => inScope(scope, run.run_number)));
  const keep = (e: EventRow) => scope === "" || (!!e.run_id && runs.has(e.run_id));
  const progress = input.progress.filter(keep);
  const red = input.red.filter(keep);

  const levelCounts = countBy([...runs.values()], (run) => run.level);
  const levels = [...levelCounts.entries()].sort((a, b) => b[1] - a[1]).map(([level]) => level);
  const ctx: Context = { runs, levels, multiLevel: levels.length > 1 };
  const fatals = fatalHits(progress);

  return {
    scope: {
      runNumber: scope,
      build,
      builds: input.builds,
      levels,
      runs: runs.size,
      players: new Set([...runs.values()].map((run) => run.install_id)).size,
      runsWithoutNumber: [...allRuns.values()].filter((run) => run.run_number === null).length,
    },
    deaths: deathsSection(ctx, progress, fatals),
    red: redSection(ctx, red),
    powers: powersSection(ctx, fatals, input.powers.filter(keep), red, input.kills.filter(keep)),
    typing: typingSection(progress, input.typing.filter(keep), ctx),
    more: moreSection(input.misc.filter(keep)),
  };
}
