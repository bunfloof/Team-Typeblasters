"use client";
// The players page: one row per browser (install_id) with its page loads,
// runs, results, every IP and user agent it used, and its device. Clicking a
// player opens the runs page filtered to that install.
import { useMemo } from "react";
import { useRouter } from "next/navigation";
import type { ColDef } from "ag-grid-community";
import type { EChartsOption } from "echarts";
import ChartCard from "@/components/ChartCard";
import DataGrid from "@/components/DataGrid";
import { Card, EmptyState, ErrorNote, Hold, PageHeader, Spinner, StatTile } from "@/components/ui";
import { fmtDuration, fmtInt, fmtPct, fmtTime, shortId } from "@/lib/format";
import { globalParams, useGlobalFilters } from "@/lib/globalFilters";
import { AXIS_STYLE, BAR_STYLE, BLUE_RAMP, INK, TOOLTIP_STYLE } from "@/lib/theme";
import { apiUrl, useApi } from "@/lib/useApi";
import { TestCell } from "./cells";
import { errorHint, shortUserAgent } from "./display";
import type { PlayerRow, PlayersResponse } from "./types";

type GridRow = PlayerRow & { browsers: string };

const num = (value: unknown): number | null => (typeof value === "number" ? value : null);

const COLUMNS: ColDef<GridRow>[] = [
  { field: "install_id", headerName: "Install", valueFormatter: (p) => shortId(p.value), tooltipField: "install_id" },
  { field: "test", headerName: "Test", cellRenderer: TestCell, headerTooltip: "install_id starts with qa- (the team's own plays)" },
  { field: "first_seen", headerName: "First seen", cellDataType: "text", valueFormatter: (p) => fmtTime(p.value) },
  { field: "last_seen", headerName: "Last seen", sort: "desc", cellDataType: "text", valueFormatter: (p) => fmtTime(p.value) },
  { field: "sessions", headerName: "Page loads", headerTooltip: "Sessions: each load of the game page" },
  { field: "runs", headerName: "Runs" },
  { field: "wins", headerName: "Wins" },
  { field: "losses", headerName: "Losses" },
  { field: "restarts", headerName: "Restarts" },
  { field: "abandoned", headerName: "Abandoned", headerTooltip: "Runs with no run end (tab closed)" },
  { field: "best_wave", headerName: "Best wave" },
  { field: "play_seconds", headerName: "Play time", valueFormatter: (p) => fmtDuration(num(p.value)), headerTooltip: "Play time summed over its runs (m:ss)" },
  { field: "best_score", headerName: "Best score", valueFormatter: (p) => fmtInt(num(p.value)) },
  { field: "run_numbers", headerName: "Run numbers", headerTooltip: "run_start run_number of each run (1 = first try in this browser)" },
  { field: "ip_count", headerName: "IPs #" },
  { field: "ips", headerName: "IPs", tooltipField: "ips" },
  { field: "countries", headerName: "Countries" },
  { field: "browsers", headerName: "Browsers", tooltipField: "user_agents" },
  { field: "user_agents", headerName: "User agents", tooltipField: "user_agents" },
  { field: "os", headerName: "OS" },
  { field: "gpu", headerName: "GPU", tooltipField: "gpu" },
  { field: "screen", headerName: "Screen" },
  { field: "language", headerName: "Language" },
  { field: "storage_ok", headerName: "Storage ok", headerTooltip: "false = private mode: the browser forgot its install id, so each page load counts as a new player" },
  { colId: "install_full", field: "install_id", headerName: "Install id (full)" },
];

// Players are bucketed by runs started; this many or more share one bar.
const MAX_BUCKET = 10;

export default function PlayersView() {
  const router = useRouter();
  const global = useGlobalFilters();
  const { data, error, loading } = useApi<PlayersResponse>(apiUrl("/api/players", globalParams(global)));

  const rows = useMemo<GridRow[]>(
    () =>
      (data?.players || []).map((player) => ({
        ...player,
        browsers: [...new Set(player.user_agents.split(" | ").filter(Boolean).map(shortUserAgent))].join(", "),
      })),
    [data],
  );

  const stats = useMemo(() => {
    const players = rows.length;
    const returning = rows.filter((player) => player.sessions > 1 || player.runs > 1).length;
    const neverStarted = rows.filter((player) => player.runs === 0).length;
    return { players, returning, neverStarted };
  }, [rows]);
  const share = (n: number) => (stats.players > 0 ? fmtPct(n / stats.players) + " of players" : undefined);

  // Runs per player: how many players started 0, 1, 2 ... runs.
  const distribution = useMemo(() => {
    const counts = new Map<number, number>();
    let top = 0;
    for (const player of rows) {
      const bucket = Math.min(player.runs, MAX_BUCKET);
      counts.set(bucket, (counts.get(bucket) || 0) + 1);
      top = Math.max(top, bucket);
    }
    return Array.from({ length: top + 1 }, (_, runs) => ({
      runs: runs === MAX_BUCKET ? MAX_BUCKET + "+" : String(runs),
      players: counts.get(runs) || 0,
    }));
  }, [rows]);

  const option = useMemo<EChartsOption>(
    () => ({
      grid: { left: 44, right: 16, top: 32, bottom: 44 },
      tooltip: {
        ...TOOLTIP_STYLE,
        trigger: "axis",
        axisPointer: { type: "shadow", shadowStyle: { color: "rgba(11,11,11,0.04)" } },
        formatter: (params: unknown) => {
          const item = (Array.isArray(params) ? params[0] : params) as { name: string; value: number };
          return item.name + (item.name === "1" ? " run" : " runs") + ": <b>" + fmtInt(item.value) + "</b> " + (item.value === 1 ? "player" : "players");
        },
      },
      xAxis: {
        type: "category",
        data: distribution.map((bucket) => bucket.runs),
        name: "Runs started",
        nameLocation: "middle",
        nameGap: 30,
        ...AXIS_STYLE,
        splitLine: { show: false },
      },
      yAxis: { type: "value", name: "Players", minInterval: 1, ...AXIS_STYLE, axisLine: { show: false } },
      series: [
        {
          type: "bar",
          name: "Players",
          data: distribution.map((bucket) => bucket.players),
          ...BAR_STYLE,
          itemStyle: { ...BAR_STYLE.itemStyle, color: BLUE_RAMP[7] },
          label: {
            show: true,
            position: "top",
            color: INK.secondary,
            fontSize: 11,
            formatter: (params: { value?: unknown }) => (params.value ? String(params.value) : ""),
          },
        },
      ],
    }),
    [distribution],
  );

  return (
    <div>
      <PageHeader
        title="Players"
        description="One row per browser (install id): every page load, run, IP address and user agent it used. Click a player to see its runs."
      />

      {error ? (
        <div className="mb-4">
          <ErrorNote error={error} hint={errorHint(error)} />
        </div>
      ) : null}

      {data ? (
        <Hold loading={loading}>
          <div className="grid gap-4 lg:grid-cols-[240px_minmax(0,1fr)]">
            <div className="grid grid-cols-2 content-start gap-3 lg:grid-cols-1">
              <StatTile label="Players" value={fmtInt(stats.players)} note="browsers with any event" />
              <StatTile label="Returning players" value={fmtInt(stats.returning)} note={share(stats.returning) || "more than one page load or run"} />
              <StatTile label="Never pressed Start" value={fmtInt(stats.neverStarted)} note={share(stats.neverStarted) || "opened the game, no run"} />
              <StatTile
                label="Test installs (qa-)"
                value={fmtInt(data.testInstalls)}
                note={global.includeTest ? "included on this page" : "hidden: tick Include test plays"}
              />
            </div>
            <ChartCard
              title="Runs per player"
              subtitle="How many players started 0, 1, 2 … runs (a returning player has more than one)."
              option={option}
              rows={distribution}
              columns={["runs", "players"]}
              height={250}
              fileName="runs-per-player"
              empty={rows.length === 0}
              footnote="A player is one browser (install id); private windows that cannot keep the id count once per page load. 0 = opened the game but never pressed Start."
            />
          </div>

          <Card className="mt-4">
            {rows.length === 0 ? (
              <EmptyState>
                No players for these filters yet.
                {!global.includeTest && data.testInstalls > 0 ? " Only test installs so far: tick Include test plays in the top bar." : null}
              </EmptyState>
            ) : (
              <DataGrid
                title="Players"
                rows={rows}
                columnDefs={COLUMNS as ColDef[]}
                height={560}
                fileName="players"
                onRowClicked={(row) => {
                  if (window.getSelection()?.toString()) {
                    return;
                  }
                  router.push("/runs?installId=" + encodeURIComponent(String(row.install_id)));
                }}
              />
            )}
          </Card>
        </Hold>
      ) : loading ? (
        <Spinner label="Loading players" />
      ) : null}
    </div>
  );
}
