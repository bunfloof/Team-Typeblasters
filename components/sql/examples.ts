// Ready-made queries for the SQL console's Examples menu. They only use SQL
// that MySQL 8 and MariaDB 10.6+ both accept: JSON_UNQUOTE(JSON_EXTRACT(...))
// (never ->>), no window functions. Each one follows the top-bar filters:
// test plays (install_id 'qa-...') hidden unless included, the level, and
// the time range (received_at, UTC).
import { levelName } from "@/lib/catalog";

export type ExampleFilters = { includeTest: boolean; level: string; from?: string; to?: string };

export type SqlExample = {
  id: string;
  group: string;
  title: string;
  description: string;
  sql: (filters: ExampleFilters) => string;
};

// The top-bar filters as "AND ..." lines for the events table `e`.
function filterLines(filters: ExampleFilters, indent = ""): string {
  const lines = [
    filters.includeTest
      ? indent + "-- AND e.install_id NOT LIKE 'qa-%'   -- test plays are included (top bar)"
      : indent + "AND e.install_id NOT LIKE 'qa-%'   -- hides the team's test plays; delete this line to include them",
  ];
  if (filters.level) {
    lines.push(indent + "AND e.level = '" + filters.level.replace(/'/g, "''") + "'   -- " + levelName(filters.level) + " level only (top bar)");
  }
  if (filters.from) {
    lines.push(indent + "AND e.received_at >= '" + filters.from.replace(/'/g, "") + "'   -- time range start (top bar, UTC)");
  }
  if (filters.to) {
    lines.push(indent + "AND e.received_at <= '" + filters.to.replace(/'/g, "") + "'   -- time range end (top bar, UTC)");
  }
  return lines.join("\n");
}

// Keeps one row per event_id: a batch the game re-sent is stored twice.
function dedupe(indent = ""): string {
  return indent + "JOIN (SELECT MIN(id) AS id FROM events GROUP BY event_id) one_copy ON one_copy.id = e.id   -- one row per event_id";
}

const LEVEL_NAME = "CASE e.level WHEN 'Prototype' THEN 'Normal' ELSE e.level END AS level_name";

// JSON_UNQUOTE(JSON_EXTRACT(<alias>.data, '$.<field>'))
function field(name: string, alias = "e"): string {
  return "JSON_UNQUOTE(JSON_EXTRACT(" + alias + ".data, '$." + name + "'))";
}

export const EXAMPLES: SqlExample[] = [
  {
    id: "events-per-type",
    group: "Overview",
    title: "Events per type",
    description: "How many of each event, how many players and runs sent it, and duplicate rows.",
    sql: (f) => `-- How many of each event type (an event re-sent by the game is counted once).
SELECT e.event,
       COUNT(DISTINCT e.event_id) AS events,
       COUNT(*) - COUNT(DISTINCT e.event_id) AS duplicate_rows,
       COUNT(DISTINCT e.install_id) AS players,
       COUNT(DISTINCT NULLIF(e.run_id, '')) AS runs,
       MIN(e.received_at) AS first_received_utc,
       MAX(e.received_at) AS last_received_utc
FROM events e
WHERE e.event IS NOT NULL
${filterLines(f, "  ")}
GROUP BY e.event
ORDER BY events DESC;`,
  },
  {
    id: "runs",
    group: "Overview",
    title: "Runs and how they ended",
    description: "One row per run: player, level, outcome, last wave, play time, kills, WPM, accuracy.",
    sql: (f) => `-- One row per run. outcome 'none' = no run_end: the tab was closed, or the run is still going.
SELECT r.run_id,
       r.install_id,
       CASE r.level WHEN 'Prototype' THEN 'Normal' ELSE r.level END AS level_name,
       r.started_utc,
       COALESCE(${field("outcome", "x")}, 'none') AS outcome,
       r.last_wave,
       ROUND(r.play_seconds) AS play_seconds,
       ${field("kills", "x")} AS kills,
       ${field("score", "x")} AS score,
       ${field("words_per_minute", "x")} AS wpm,
       ${field("accuracy", "x")} AS accuracy,
       ${field("run_number", "x")} AS run_number
FROM (
  SELECT e.run_id,
         MIN(e.install_id) AS install_id,
         MIN(e.level) AS level,
         MIN(e.received_at) AS started_utc,
         MAX(e.wave) AS last_wave,
         MAX(e.t_play) AS play_seconds,
         MIN(CASE WHEN e.event = 'run_end' THEN e.id END) AS end_id
  FROM events e
  WHERE e.run_id IS NOT NULL AND e.run_id <> ''
${filterLines(f, "    ")}
  GROUP BY e.run_id
) r
LEFT JOIN events x ON x.id = r.end_id
ORDER BY r.started_utc DESC;`,
  },
  {
    id: "deaths-by-killer",
    group: "Deaths",
    title: "Deaths by killer and wave",
    description: "Fatal hits (damage with fatal = true) by attacker type, level and wave.",
    sql: (f) => `-- Who killed the players: fatal hits by attacker type, level and wave.
SELECT ${LEVEL_NAME},
       e.wave,
       ${field("source")} AS killed_by,
       COUNT(*) AS deaths,
       SUM(${field("ambusher")} = 'true') AS by_an_ambusher,
       SUM(${field("attacker_letters_typed")} > 0) AS while_typing_the_killer
FROM events e
${dedupe()}
WHERE e.event = 'damage'
  AND ${field("fatal")} = 'true'
${filterLines(f, "  ")}
GROUP BY e.level, e.wave, killed_by
ORDER BY e.level, e.wave, deaths DESC;`,
  },
  {
    id: "typing-at-death",
    group: "Deaths",
    title: "What players were typing when they died",
    description: "Each death with the word in progress and whether it was the killer's word.",
    sql: (f) => `-- The moment of death: what the player was typing, and the attacker's word.
-- attacker_letters_typed > 0 means the player was typing the very zombie that killed them.
SELECT e.install_id,
       e.run_id,
       ${LEVEL_NAME},
       e.wave,
       ROUND(e.t_play, 1) AS t_play,
       ${field("source")} AS killed_by,
       ${field("attacker_word")} AS attacker_word,
       ${field("attacker_letters_typed")} AS attacker_letters_typed,
       ${field("typing_target")} AS typing_target,
       ${field("typing_letters_done")} AS letters_done,
       ${field("typing_word_length")} AS word_length,
       ${field("zombies_alive")} AS zombies_alive,
       ${field("combo")} AS combo
FROM events e
${dedupe()}
WHERE e.event = 'damage'
  AND ${field("fatal")} = 'true'
${filterLines(f, "  ")}
ORDER BY e.received_at DESC;`,
  },
  {
    id: "unused-powers",
    group: "Deaths",
    title: "Unused powers at death",
    description: "Lost runs with energy for a power, or a free use, still in the pocket.",
    sql: (f) => `-- Lost runs and what the player still had but never used.
SELECT e.install_id,
       e.run_id,
       ${LEVEL_NAME},
       e.wave,
       ROUND(e.t_play) AS t_play,
       ${field("lure_charges")} AS lures_left,
       ${field("freeze_charges")} AS freezes_left,
       ${field("frenzy_kept")} AS frenzy_kept,
       ${field("rpg_kept")} AS rpg_kept,
       ${field("rpg_loaded")} AS rpg_loaded,
       ${field("skill_energy")} AS energy_left,   -- lure 6, freeze 8, FRENZY 12, RPG 18 (NULL = an older build)
       ${field("can_use_frenzy")} AS could_use_frenzy,
       ${field("can_use_rpg")} AS could_use_rpg,
       ${field("combo")} AS combo
FROM events e
${dedupe()}
WHERE e.event = 'run_end'
  AND ${field("outcome")} = 'lost'
${filterLines(f, "  ")}
ORDER BY e.received_at DESC;`,
  },
  {
    id: "zombie-fate",
    group: "Combat",
    title: "Zombie fate by kind",
    description: "Per zombie kind: shot, blown up, bit the player, rode past, and time alive.",
    sql: (f) => `-- What happened to each kind of zombie.
SELECT ${field("kind")} AS kind,
       COUNT(*) AS zombies,
       SUM(${field("outcome")} = 'shot') AS shot,
       SUM(${field("outcome")} = 'blast') AS blown_up,
       SUM(${field("outcome")} = 'bite') AS bit_the_player,
       SUM(${field("outcome")} = 'rode_past') AS rode_past,
       SUM(${field("outcome")} NOT IN ('shot', 'blast', 'bite', 'rode_past')) AS other_end,
       ROUND(AVG(${field("seconds_alive")}), 1) AS avg_seconds_alive
FROM events e
${dedupe()}
WHERE e.event = 'zombie'
${filterLines(f, "  ")}
GROUP BY kind
ORDER BY zombies DESC;`,
  },
  {
    id: "red-blasts",
    group: "Combat",
    title: "Red-zombie blasts: crowd vs kills",
    description: "Red (explosive) zombies by how many others were in the ring when they blew.",
    sql: (f) => `-- Did players wait for a crowd before blowing up a red zombie?
-- in_ring_now = other zombies inside the ring at the blast; in_ring_max = the best crowd it ever had.
SELECT CAST(${field("in_ring_now")} AS SIGNED) AS in_ring_now,
       COUNT(*) AS blasts,
       ROUND(AVG(${field("kills")}), 2) AS avg_kills,
       MAX(CAST(${field("kills")} AS SIGNED)) AS max_kills,
       ROUND(AVG(${field("in_ring_max")}), 2) AS avg_best_crowd,
       COUNT(DISTINCT e.install_id) AS players
FROM events e
${dedupe()}
WHERE e.event = 'blast'
  AND ${field("source")} = 'red'
${filterLines(f, "  ")}
GROUP BY in_ring_now
ORDER BY in_ring_now;`,
  },
  {
    id: "power-actions",
    group: "Powers",
    title: "Power and weapon actions",
    description: "Every lure / freeze / FRENZY / RPG action (buy, use, throw, empty...) by item.",
    sql: (f) => `-- Everything done with a lure, freeze, FRENZY or RPG.
SELECT ${field("item")} AS item,
       ${field("action")} AS action,
       COUNT(*) AS times,
       COUNT(DISTINCT e.install_id) AS players,
       COUNT(DISTINCT e.run_id) AS runs
FROM events e
${dedupe()}
WHERE e.event = 'power'
${filterLines(f, "  ")}
GROUP BY item, action
ORDER BY item, times DESC;`,
  },
  {
    id: "refusals",
    group: "Powers",
    title: "Power refusals by reason",
    description: "Why a power key did nothing (not enough energy, already running; older builds: slots full, combo...).",
    sql: (f) => `-- Power uses (and, in older builds, weapon buys) the game refused, and why.
SELECT ${field("action")} AS action,
       ${field("item")} AS item,
       ${field("reason")} AS reason,
       COUNT(*) AS refusals,
       COUNT(DISTINCT e.install_id) AS players,
       ROUND(AVG(${field("skill_energy")}), 1) AS avg_energy_after,
       ROUND(AVG(${field("combo")}), 1) AS avg_combo
FROM events e
${dedupe()}
WHERE e.event = 'power'
  AND ${field("action")} IN ('buy_refused', 'use_refused')
${filterLines(f, "  ")}
GROUP BY action, item, reason
ORDER BY refusals DESC;`,
  },
  {
    id: "wrong-keys",
    group: "Typing",
    title: "Most common wrong keys",
    description: "The key that was needed vs the key typed, most frequent first.",
    sql: (f) => `-- The keys players get wrong most often (inside a word; stray keys left out).
SELECT ${field("expected")} AS expected,
       ${field("typed")} AS typed,
       COUNT(*) AS times,
       COUNT(DISTINCT e.install_id) AS players,
       SUM(${field("case_only")} = 'true') AS right_letter_wrong_case
FROM events e
${dedupe()}
WHERE e.event = 'wrong_key'
  AND ${field("stray")} = 'false'
${filterLines(f, "  ")}
GROUP BY expected, typed
ORDER BY times DESC
LIMIT 100;`,
  },
  {
    id: "quiz",
    group: "Boss & tips",
    title: "Boss quiz answers by question",
    description: "Each quiz question: times asked, right, wrong, too slow, seconds to answer.",
    sql: (f) => `-- Boss quiz: how each question went.
SELECT CAST(${field("question_index")} AS SIGNED) AS question_index,
       MIN(${field("question")}) AS question,
       COUNT(*) AS asked,
       SUM(${field("result")} = 'right') AS answered_right,
       SUM(${field("result")} = 'wrong') AS answered_wrong,
       SUM(${field("result")} = 'timeout') AS too_slow,
       ROUND(AVG(${field("seconds")}), 1) AS avg_seconds
FROM events e
${dedupe()}
WHERE e.event = 'quiz'
${filterLines(f, "  ")}
GROUP BY question_index
ORDER BY question_index;`,
  },
  {
    id: "tips",
    group: "Boss & tips",
    title: "Tip boxes: seconds open",
    description: "How long each first-time tip stayed open; under 1 s = closed without reading.",
    sql: (f) => `-- How long each tip box stayed open (real seconds).
SELECT t.tip,
       COUNT(*) AS closed,
       ROUND(AVG(t.seconds_open), 1) AS avg_seconds_open,
       MIN(t.seconds_open) AS min_seconds,
       MAX(t.seconds_open) AS max_seconds,
       SUM(t.seconds_open < 1) AS closed_within_1s
FROM (
  SELECT ${field("key")} AS tip,
         CAST(${field("seconds_open")} AS DECIMAL(10,3)) AS seconds_open
  FROM events e
${dedupe("  ")}
  WHERE e.event = 'tip'
${filterLines(f, "    ")}
) t
GROUP BY t.tip
ORDER BY avg_seconds_open DESC;`,
  },
  {
    id: "players-ips",
    group: "Players & network",
    title: "Players with their IPs and browsers",
    description: "Each install_id with its page loads, runs, IPs, countries and user agents.",
    sql: (f) => `-- Every player (install_id) with where their events came from (requests table).
-- GROUP_CONCAT stops at 1024 characters by default.
SELECT e.install_id,
       COUNT(DISTINCT e.session_id) AS page_loads,
       COUNT(DISTINCT NULLIF(e.run_id, '')) AS runs,
       MIN(e.received_at) AS first_seen_utc,
       MAX(e.received_at) AS last_seen_utc,
       GROUP_CONCAT(DISTINCT r.client_ip SEPARATOR ', ') AS ips,
       GROUP_CONCAT(DISTINCT r.cf_ipcountry SEPARATOR ', ') AS countries,
       GROUP_CONCAT(DISTINCT r.sec_ch_ua_platform SEPARATOR ', ') AS platforms,
       GROUP_CONCAT(DISTINCT r.user_agent SEPARATOR ' | ') AS user_agents
FROM events e
JOIN requests r ON r.id = e.request_id
WHERE e.install_id IS NOT NULL
${filterLines(f, "  ")}
GROUP BY e.install_id
ORDER BY first_seen_utc DESC;`,
  },
  {
    id: "duplicates",
    group: "Data quality",
    title: "Events stored twice (duplicate event_id)",
    description: "Batches the game re-sent: the same event_id in more than one row.",
    sql: (f) => `-- Events stored more than once (the game re-sent a batch). Count each event_id once.
SELECT e.event_id,
       MIN(e.event) AS event,
       COUNT(*) AS copies,
       GROUP_CONCAT(e.id ORDER BY e.id) AS row_ids,
       GROUP_CONCAT(DISTINCT e.request_id) AS request_ids,
       GROUP_CONCAT(DISTINCT e.batch_id) AS batch_ids,
       MIN(e.received_at) AS first_received_utc,
       MAX(e.received_at) AS last_received_utc
FROM events e
WHERE e.event_id IS NOT NULL
${filterLines(f, "  ")}
GROUP BY e.event_id
HAVING COUNT(*) > 1
ORDER BY copies DESC, first_received_utc;`,
  },
  {
    id: "no-cloudflare",
    group: "Data quality",
    title: "Requests that skipped Cloudflare",
    description: "HTTP requests without a cf_ray: direct hits on the server, bots, scanners.",
    sql: () => `-- HTTP requests that did not come through Cloudflare (no cf_ray header).
-- (The requests table has no install_id, so the test-play filter does not apply.)
SELECT r.id,
       r.received_at,
       r.method,
       r.path,
       r.status,
       r.client_ip,
       r.peer_ip,
       r.x_forwarded_for,
       r.user_agent,
       r.origin,
       r.referer,
       r.body_bytes,
       r.event_count,
       r.parse_ok,
       r.parse_error
FROM requests r
WHERE r.cf_ray IS NULL
ORDER BY r.received_at DESC
LIMIT 1000;`,
  },
];
