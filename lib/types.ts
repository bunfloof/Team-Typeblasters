// Shapes shared by the API routes and the pages. Safe to import anywhere.

// One row of the `events` table, with its JSON `data` already parsed.
export type EventRow = {
  id: number;
  request_id: number;
  position_in_batch: number;
  received_at: string; // UTC "YYYY-MM-DD HH:MM:SS.ffffff"
  event: string;
  event_id: string | null;
  batch_id: string | null;
  install_id: string | null;
  session_id: string | null;
  run_id: string | null;
  seq: number | null;
  build: string | null;
  level: string | null;
  wave: number | null;
  t_play: number | null;
  t_real: number | null;
  data: Record<string, unknown>;
};

// The columns of `requests` the dashboard shows by default (body/headers on demand).
export const REQUEST_COLUMNS = [
  "id",
  "received_at",
  "source",
  "method",
  "path",
  "status",
  "client_ip",
  "peer_ip",
  "cf_connecting_ip",
  "x_real_ip",
  "x_forwarded_for",
  "cf_ray",
  "cf_ipcountry",
  "user_agent",
  "origin",
  "referer",
  "sec_fetch_site",
  "sec_fetch_mode",
  "sec_fetch_dest",
  "sec_ch_ua",
  "sec_ch_ua_platform",
  "sec_ch_ua_mobile",
  "accept_language",
  "content_type",
  "body_bytes",
  "body_encoding",
  "parse_ok",
  "parse_error",
  "event_count",
  "raw_id",
] as const;

export const EVENT_TABLE_COLUMNS = [
  "id",
  "request_id",
  "position_in_batch",
  "received_at",
  "event",
  "event_id",
  "batch_id",
  "install_id",
  "session_id",
  "run_id",
  "seq",
  "build",
  "level",
  "wave",
  "t_play",
  "t_real",
] as const;

// Every API route answers { error } with a non-2xx status on failure.
export type ApiError = { error: string; config?: boolean };

// GET /api/meta
export type MetaResponse = {
  dbVersion: string;
  eventCounts: { event: string; count: number }[];
  totals: {
    events: number;
    testEvents: number;
    requests: number;
    installs: number;
    sessions: number;
    runs: number;
  };
  levels: string[];
  builds: string[];
  firstReceived: string | null;
  lastReceived: string | null;
  dataFields: Record<string, string[]>; // event type -> keys seen in data (sampled)
};
