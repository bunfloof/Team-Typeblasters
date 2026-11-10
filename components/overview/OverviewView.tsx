"use client";
// The Overview page: headline numbers, live activity for exam-day
// monitoring, the latest sessions, event counts, and the log server's health.
import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import type { ColDef, ICellRendererParams } from "ag-grid-community";
import { RefreshCw } from "lucide-react";
import ChartCard from "@/components/ChartCard";
import DataGrid from "@/components/DataGrid";
import { Card, ErrorNote, Hold, PageHeader, Spinner, StatTile, buttonClass } from "@/components/ui";
import { EVENTS } from "@/lib/catalog";
import { fmtCompact, fmtDuration, fmtInt, fmtPct, fmtTime, shortId } from "@/lib/format";
import { globalParams, useGlobalFilters } from "@/lib/globalFilters";
import { OUTCOME_COLORS, SERIES } from "@/lib/theme";
import { apiUrl, useApi } from "@/lib/useApi";
import { barOption, tipHtml } from "@/components/insights/charts";
import { configHint } from "@/components/insights/parts";
import DataHealth from "./DataHealth";
import type { OverviewResponse } from "./types";

const AUTO_REFRESH_MS = 30000;

const OUTCOMES = [
  { key: "won", label: "Won" },
  { key: "lost", label: "Lost (died)" },
  { key: "restart", label: "Restarted" },
  { key: "abandoned", label: "Abandoned" },
  { key: "playing", label: "Playing now" },
] as const;

export default function OverviewView() {
  const filters = useGlobalFilters();
  const url = apiUrl("/api/overview", globalParams(filters));
  const { data, error, loading, reload } = useApi<OverviewResponse>(url);

  // Auto refresh (for watching the exam-day playtest live). Its refetches do
  // not dim the page; a filter change or the Refresh button does.
  const [auto, setAuto] = useState(true);
  const [quietUrl, setQuietUrl] = useState<string | null>(null);
  const reloadRef = useRef(reload);
  const urlRef = useRef(url);
  useEffect(() => {
    reloadRef.current = reload;
    urlRef.current = url;
  });
  useEffect(() => {
    if (!auto) {
      return;
    }
    const timer = setInterval(() => {
      setQuietUrl(urlRef.current);
      reloadRef.current();
    }, AUTO_REFRESH_MS);
    return () => clearInterval(timer);
  }, [auto]);

  return (
    <div className="mx-auto max-w-[1400px]">
      <PageHeader
        title="Overview"
        description="Who is playing, how runs end, and whether the log server is receiving everything. The global filters in the top bar apply."
        actions={
          <>
            <label className="flex items-center gap-2 text-sm text-neutral-600">
              <input type="checkbox" checked={auto} onChange={(event) => setAuto(event.target.checked)} className="h-4 w-4 accent-[#2a78d6]" />
              Auto-refresh every 30 s
            </label>
            <button
              type="button"
              className={buttonClass}
              disabled={loading}
              onClick={() => {
                setQuietUrl(null);
                reload();
              }}
            >
              <RefreshCw className={"h-3.5 w-3.5 " + (loading ? "animate-spin" : "")} /> Refresh
            </button>
          </>
        }
      />

      {error ? (
        <div className="mb-6">
          <ErrorNote error={error} hint={configHint(error)} />
        </div>
      ) : null}
      {!data && loading ? <Spinner label="Loading the overview" /> : null}

      {data ? (
        <Hold loading={loading && quietUrl !== url}>
          <div className="flex flex-col gap-4 pb-16">
            <Totals data={data} />
            <div className="grid gap-4 xl:grid-cols-3">
              <LiveActivity live={data.live} className="xl:col-span-2" />
              <DataHealth health={data.health} />
            </div>
            <RecentSessions rows={data.recentSessions} />
            <div className="grid gap-4 xl:grid-cols-2">
              <EventCounts rows={data.eventCounts} />
              <ProblemRequests rows={data.health.problems} />
            </div>
          </div>
        </Hold>
      ) : null}
    </div>
  );
}

function Totals({ data }: { data: OverviewResponse }) {
  const t = data.totals;
  const runs = Object.values(data.outcomes).reduce((sum, v) => sum + v, 0);
  return (
    <>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        <StatTile label="Players" value={fmtInt(t.players)} note="browsers (install ids)" />
        <StatTile label="Sessions" value={fmtInt(t.sessions)} note="page loads" />
        <StatTile label="Runs" value={fmtInt(t.runs)} note="Start presses" />
        <StatTile
          label="Events"
          value={fmtCompact(t.events)}
          note={t.rows > t.events ? fmtInt(t.rows - t.events) + " duplicate rows ignored" : "no duplicates"}
        />
        <StatTile label="Median play time" value={fmtDuration(data.medianRunSeconds)} note={"per run, n = " + fmtInt(data.medianRunSecondsN)} />
      </div>
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        {OUTCOMES.map((o) => (
          <div key={o.key} className="rounded-xl border border-black/10 bg-[#fcfcfb] px-4 py-3">
            <div className="flex items-center gap-2 text-[13px] text-neutral-500">
              <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: OUTCOME_COLORS[o.key] }} />
              {o.label}
            </div>
            <div className="mt-1 text-2xl font-semibold text-neutral-900">{fmtInt(data.outcomes[o.key])}</div>
            <div className="mt-0.5 text-xs text-neutral-500">
              {o.key === "playing"
                ? fmtInt(data.live.activeSessions) + " sessions active in the last 5 min"
                : runs === 0
                  ? "no runs yet"
                  : fmtPct(data.outcomes[o.key] / runs) + " of runs" + (o.key === "abandoned" ? " (tab closed)" : "")}
            </div>
          </div>
        ))}
      </div>
    </>
  );
}

// "2026-10-13 03:25" (UTC) -> "8:25 PM" in the viewer's time zone.
function localClock(minuteUtc: string): string {
  const date = new Date(minuteUtc.replace(" ", "T") + ":00Z");
  return Number.isNaN(date.getTime()) ? minuteUtc : date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
}

function LiveActivity({ live, className }: { live: OverviewResponse["live"]; className?: string }) {
  const option = useMemo(() => {
    const labels = live.minutes.map((m) => localClock(m.minute));
    return barOption({
      categories: labels,
      values: live.minutes.map((m) => m.events),
      colors: SERIES[0],
      valueName: "Events",
      labelInterval: 9, // a clock label every 10 minutes
      tooltip: (i) =>
        tipHtml(labels[i], [
          ["Events received", fmtInt(live.minutes[i].events)],
          ["Sessions sending", fmtInt(live.minutes[i].sessions)],
        ]),
    });
  }, [live.minutes]);
  return (
    <ChartCard
      className={className}
      title="Live: events received per minute"
      subtitle={
        <>
          Last 60 minutes, your local time · {fmtInt(live.eventsLastHour)} events · {fmtInt(live.activeSessions)} sessions active in the last 5 min
        </>
      }
      option={option}
      rows={live.minutes.map((m) => ({ minute_utc: m.minute, local_time: localClock(m.minute), events: m.events, sessions: m.sessions }))}
      getRowId={byMinute}
      height={260}
      fileName="events-per-minute"
      footnote="For watching the log server during the playtest (this is the only clock-time chart: gameplay charts use seconds of play). A flat zero while people are playing means batches are not arriving."
    />
  );
}

// Stable row ids, so the 30 s refresh keeps ticked rows ticked.
const byMinute = (row: Record<string, unknown>) => String(row.minute_utc);
const bySession = (row: Record<string, unknown>) => String(row.session_id);
const byEvent = (row: Record<string, unknown>) => String(row.event);
const byRequest = (row: Record<string, unknown>) => String(row.id);

function SessionLink(params: ICellRendererParams) {
  const sessionId = String(params.value ?? "");
  return sessionId ? (
    <Link href={"/runs?sessionId=" + encodeURIComponent(sessionId)} className="text-[#1c5cab] hover:underline">
      Runs →
    </Link>
  ) : null;
}

const SESSION_COLUMNS: ColDef[] = [
  { field: "last_seen", headerName: "Last seen", valueFormatter: (p) => fmtTime(p.value as string), sort: "desc", minWidth: 180 },
  { field: "install_id", headerName: "Player", valueFormatter: (p) => shortId(p.value as string), tooltipField: "install_id", minWidth: 150 },
  { field: "session_id", headerName: "Timeline", cellRenderer: SessionLink, sortable: false, filter: false, minWidth: 100 },
  { field: "client_ip", headerName: "IP", minWidth: 130 },
  { field: "country", headerName: "Country", minWidth: 100 },
  { field: "browser", headerName: "Browser", tooltipField: "user_agent", minWidth: 190 },
  { field: "levels", headerName: "Level", minWidth: 100 },
  { field: "runs", headerName: "Runs", minWidth: 80 },
  { field: "won", headerName: "Won", minWidth: 80 },
  { field: "lost", headerName: "Lost", minWidth: 80 },
  { field: "best_wave", headerName: "Best wave", minWidth: 110 },
  { field: "events", headerName: "Events", minWidth: 90 },
  { field: "first_seen", headerName: "First seen", valueFormatter: (p) => fmtTime(p.value as string), minWidth: 180 },
  { field: "build", headerName: "Build", minWidth: 200 },
  { field: "user_agent", headerName: "User agent", minWidth: 300 },
];

function RecentSessions({ rows }: { rows: OverviewResponse["recentSessions"] }) {
  return (
    <Card title="Recent sessions" subtitle="The 20 page loads with the latest events (IP and browser of their latest request)">
      <DataGrid rows={rows} columnDefs={SESSION_COLUMNS} height={360} fileName="recent-sessions" getRowId={bySession} />
    </Card>
  );
}

function EventCounts({ rows }: { rows: OverviewResponse["eventCounts"] }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => r.event),
        values: rows.map((r) => r.events),
        colors: SERIES[0],
        horizontal: true,
        valueName: "Events (duplicates removed)",
        barLabel: (i) => fmtCompact(rows[i].events),
        tooltip: (i) =>
          tipHtml(rows[i].event, [
            ["What", EVENTS[rows[i].event]?.title ?? "-"],
            ["Events", fmtInt(rows[i].events)],
            ["Rows stored", fmtInt(rows[i].rows)],
          ]),
      }),
    [rows],
  );
  const total = rows.reduce((sum, r) => sum + r.events, 0);
  return (
    <ChartCard
      title="Events by type"
      subtitle={fmtInt(total) + " events · " + rows.length + " types"}
      option={option}
      rows={rows.map((r) => ({ event: r.event, title: EVENTS[r.event]?.title ?? "", events: r.events, rows_stored: r.rows }))}
      getRowId={byEvent}
      height={Math.max(240, rows.length * 22 + 60)}
      fileName="event-counts"
      empty={rows.length === 0}
      emptyText="No events for these filters yet."
      footnote="Rows stored can be higher than events when a batch was retried (same event_id twice). Heartbeat “sample” events come once per second of play."
    />
  );
}

const PROBLEM_COLUMNS: ColDef[] = [
  { field: "received_at", headerName: "When", valueFormatter: (p) => fmtTime(p.value as string), sort: "desc", minWidth: 180 },
  { field: "status", headerName: "Status", minWidth: 90 },
  { field: "method", headerName: "Method", minWidth: 90 },
  { field: "path", headerName: "Path", tooltipField: "path", minWidth: 160 },
  { field: "client_ip", headerName: "IP", minWidth: 130 },
  { field: "parse_error", headerName: "Parse error", tooltipField: "parse_error", minWidth: 200 },
  { field: "user_agent", headerName: "User agent", tooltipField: "user_agent", minWidth: 300 },
  { field: "id", headerName: "Request id", minWidth: 110 },
];

function ProblemRequests({ rows }: { rows: OverviewResponse["health"]["problems"] }) {
  return (
    <Card title="Requests the server refused" subtitle="Latest 25 with status 400+ or a batch that failed to parse (404s are mostly bots)">
      <DataGrid rows={rows} columnDefs={PROBLEM_COLUMNS} height={360} fileName="problem-requests" getRowId={byRequest} />
    </Card>
  );
}
