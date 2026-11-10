// Response shapes of GET /api/runs and GET /api/players. Safe to import
// anywhere (types only).
import type { RunOutcome, RunSummary } from "@/lib/runSummary";

// One run, plus where it was played from: the IP and browser of the HTTP
// request that carried its first events, and the device its page load
// (session_start) reported.
export type RunRow = RunSummary & {
  client_ip: string | null;
  user_agent: string | null;
  origin: string | null;
  cf_ipcountry: string | null;
  os: string | null;
  gpu: string | null;
  screen: string | null; // "1920x1080" (game canvas size)
};

export type RunsResponse = { runs: RunRow[] };

// URL / API filter on run_start's run_number: "" (all), "first" (= 1) or "repeat" (> 1).
export type RunNumberFilter = "" | "first" | "repeat";

export const RUN_OUTCOMES: RunOutcome[] = ["won", "lost", "restart", "abandoned", "playing"];

// One browser (install_id) with everything it did under the global filters.
export type PlayerRow = {
  install_id: string;
  test: boolean; // install_id qa-... (the team's own plays)
  first_seen: string | null; // received_at (UTC) of its first event
  last_seen: string | null;
  sessions: number; // page loads
  runs: number; // presses of Start
  wins: number;
  losses: number;
  restarts: number;
  abandoned: number;
  best_wave: number | null; // null = never started a run
  play_seconds: number; // play time summed over its runs
  best_score: number | null;
  run_numbers: string; // run_start run_number of each run, "1, 2, 3"
  ip_count: number;
  ips: string; // every client IP it used, comma separated, first seen first
  countries: string; // Cloudflare country codes, comma separated
  user_agent_count: number;
  user_agents: string; // every user agent, " | " separated (they contain commas)
  os: string | null; // from its latest session_start
  gpu: string | null;
  screen: string | null;
  language: string | null;
  storage_ok: boolean | null; // false = some page load could not keep the install id (private mode)
};

export type PlayersResponse = {
  players: PlayerRow[];
  testInstalls: number; // qa- installs under the level filter (counted even when they are hidden)
};
