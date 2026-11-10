// Response shape of GET /api/overview. Safe to import anywhere (types only).

export type RecentSession = {
  session_id: string;
  install_id: string | null;
  client_ip: string | null; // of the session's latest request
  country: string | null; // cf_ipcountry
  browser: string; // short name parsed from the user agent, e.g. "Chrome 141 · Windows"
  user_agent: string | null;
  levels: string; // level names, comma separated
  build: string | null;
  runs: number;
  won: number;
  lost: number;
  best_wave: number;
  events: number;
  first_seen: string; // received_at, UTC
  last_seen: string;
};

export type ProblemRequest = {
  id: number;
  received_at: string;
  status: number;
  method: string;
  path: string;
  client_ip: string | null;
  parse_error: string | null;
  user_agent: string | null;
};

export type OverviewResponse = {
  now: string; // database clock, UTC
  totals: { players: number; sessions: number; runs: number; events: number; rows: number };
  outcomes: { won: number; lost: number; restart: number; abandoned: number; playing: number };
  medianRunSeconds: number | null; // play seconds per run (runs still playing left out)
  medianRunSecondsN: number;
  live: {
    minutes: { minute: string; events: number; sessions: number }[]; // last 60 minutes, UTC "YYYY-MM-DD HH:MM", oldest first
    activeSessions: number; // sessions with an event in the last 5 minutes
    eventsLastHour: number;
  };
  recentSessions: RecentSession[];
  eventCounts: { event: string; events: number; rows: number }[]; // events = distinct event_id
  health: {
    requests: number;
    batches: number; // POST /v1/batch requests (parse_ok set)
    parseFailed: number;
    non200: number;
    statuses: { status: number; method: string; requests: number }[];
    withoutCfRay: number;
    batchesWithoutCfRay: number;
    duplicatedEventIds: number; // event_ids stored more than once (retried batches)
    duplicateRows: number; // the extra copies
    missingSeq: number; // seq numbers missing inside sessions (lost events)
    sessionsWithGaps: number;
    sessions: number;
    testEvents: number; // whole table, install_id qa-...
    allEvents: number;
    problems: ProblemRequest[]; // latest requests with status >= 400 or a parse error
  };
};
