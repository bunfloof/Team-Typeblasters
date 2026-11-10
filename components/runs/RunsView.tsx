"use client";
// The runs page: every run (Start pressed -> won / died / restarted / tab
// closed), filtered from one row of controls kept in the URL, summed up in
// stat tiles, and listed in a table. Clicking a row opens the run's timeline
// (/runs/<run_id>).
import { useMemo } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import type { ColDef } from "ag-grid-community";
import { X } from "lucide-react";
import DataGrid from "@/components/DataGrid";
import { buttonClass, Card, EmptyState, ErrorNote, Hold, inputClass, PageHeader, Spinner, StatTile } from "@/components/ui";
import { levelName } from "@/lib/catalog";
import { fmt1, fmtDuration, fmtInt, fmtPct, fmtTime, isTestInstall, shortId } from "@/lib/format";
import { intersectRange } from "@/lib/filters";
import { globalParams, rangeLabel, useGlobalFilters } from "@/lib/globalFilters";
import { apiUrl, useApi } from "@/lib/useApi";
import { KillerCell, OutcomeCell } from "./cells";
import { deviceText, errorHint, median, OUTCOME_LABELS, shortUserAgent } from "./display";
import FilterInput from "./FilterInput";
import { RUN_OUTCOMES, type RunRow, type RunsResponse } from "./types";

// URL parameters owned by the filter row. Any other filter parameter in the
// URL (e.g. search=LURKER from a link) is passed on to the API and shown as
// a removable chip. includeTest / level come from the top bar instead.
const OWN_PARAMS = ["outcome", "runNumber", "installId", "q"];
const GLOBAL_PARAMS = ["includeTest", "level"];

type GridRow = RunRow & { level_name: string; browser: string; device: string };

const num = (value: unknown): number | null => (typeof value === "number" ? value : null);

const COLUMNS: ColDef<GridRow>[] = [
  { field: "started_at", headerName: "Started", sort: "desc", cellDataType: "text", valueFormatter: (p) => fmtTime(p.value) },
  { field: "outcome", headerName: "Outcome", cellRenderer: OutcomeCell, minWidth: 130 },
  { field: "level_name", headerName: "Level" },
  { field: "run_number", headerName: "Run #", headerTooltip: "Runs started in this browser, this one included (1 = first try)" },
  {
    field: "resumed_from_wave",
    headerName: "Resumed at",
    valueFormatter: (p) => (num(p.value) === null ? "" : "wave " + p.value),
    headerTooltip: "Went on from a checkpoint at this wave (Restart after a death); empty = started from the beginning",
  },
  { field: "wave_reached", headerName: "Wave reached" },
  { field: "seconds", headerName: "Play time", valueFormatter: (p) => fmtDuration(num(p.value)), headerTooltip: "Seconds of play (pauses and tip boxes not counted)" },
  { field: "score", headerName: "Score", valueFormatter: (p) => fmtInt(num(p.value)) },
  { field: "kills", headerName: "Kills" },
  { field: "accuracy", headerName: "Accuracy", valueFormatter: (p) => (num(p.value) === null ? "" : fmt1(p.value) + "%") },
  { field: "wpm", headerName: "WPM", valueFormatter: (p) => fmt1(num(p.value)), headerTooltip: "Words per minute over the whole run, rides included" },
  { field: "killer", headerName: "Killed by", cellRenderer: KillerCell, minWidth: 150 },
  { field: "install_id", headerName: "Install", valueFormatter: (p) => shortId(p.value), tooltipField: "install_id" },
  { field: "client_ip", headerName: "IP" },
  { field: "browser", headerName: "Browser", tooltipField: "user_agent" },
  { field: "device", headerName: "Device", tooltipField: "gpu" },
  { field: "cf_ipcountry", headerName: "Country" },
  { field: "user_agent", headerName: "User agent", tooltipField: "user_agent" },
  { field: "gpu", headerName: "GPU", tooltipField: "gpu" },
  { field: "origin", headerName: "Origin" },
  { field: "best_combo", headerName: "Best combo" },
  { field: "death_wave", headerName: "Death wave" },
  { field: "death_phase", headerName: "Death phase" },
  { field: "tips_enabled", headerName: "Tips on" },
  { field: "events", headerName: "Events" },
  { field: "build", headerName: "Build" },
  { field: "session_id", headerName: "Session" },
  { field: "run_id", headerName: "Run id" },
  { field: "last_event_at", headerName: "Last event", cellDataType: "text", valueFormatter: (p) => fmtTime(p.value) },
];

// Replaces the page URL with one parameter changed (read from the address
// bar, so filters changed a moment ago are kept).
function useSetParams() {
  const router = useRouter();
  return (change: (params: URLSearchParams) => void) => {
    const params = new URLSearchParams(window.location.search);
    change(params);
    const text = params.toString();
    router.replace(text ? "/runs?" + text : "/runs", { scroll: false });
  };
}

export default function RunsView() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const setParams = useSetParams();
  const global = useGlobalFilters();

  const outcome = searchParams.get("outcome") || "";
  const runNumber = searchParams.get("runNumber") || "";
  const installId = searchParams.get("installId") || "";
  const q = searchParams.get("q") || "";

  const setParam = (name: string, value: string) =>
    setParams((params) => (value ? params.set(name, value) : params.delete(name)));

  // Other filter parameters that came with the URL (shown as chips).
  const extras = [...searchParams.entries()].filter(([name]) => !OWN_PARAMS.includes(name) && !GLOBAL_PARAMS.includes(name));
  const removeExtra = (name: string, value: string) =>
    setParams((params) => {
      const kept = params.getAll(name).filter((other) => other !== value);
      params.delete(name);
      kept.forEach((other) => params.append(name, other));
    });
  const hasFilters = OWN_PARAMS.some((name) => searchParams.has(name)) || extras.length > 0;

  // The API gets the URL filters (q is searched here) plus the global filters.
  const url = useMemo(() => {
    const params = new URLSearchParams(searchParams.toString());
    for (const name of [...GLOBAL_PARAMS, "q"]) {
      params.delete(name);
    }
    for (const [name, value] of Object.entries(globalParams(global))) {
      params.set(name, value);
    }
    // A from / to in this page's URL narrows the top bar's time range.
    const range = intersectRange(global, { from: searchParams.get("from") ?? "", to: searchParams.get("to") ?? "" });
    params.delete("from");
    params.delete("to");
    if (range.from) params.set("from", range.from);
    if (range.to) params.set("to", range.to);
    return apiUrl("/api/runs", params);
  }, [searchParams, global]);
  const { data, error, loading } = useApi<RunsResponse>(url);

  const rows = useMemo<GridRow[]>(() => {
    const needle = q.trim().toLowerCase();
    return (data?.runs || [])
      .filter(
        (run) =>
          !needle ||
          [run.run_id, run.install_id, run.session_id, run.client_ip].some((value) => value?.toLowerCase().includes(needle)),
      )
      .map((run) => ({
        ...run,
        level_name: levelName(run.level),
        browser: shortUserAgent(run.user_agent),
        device: deviceText(run.os, run.screen),
      }));
  }, [data, q]);

  const stats = useMemo(() => {
    const count = (outcomeName: string) => rows.filter((run) => run.outcome === outcomeName).length;
    return {
      runs: rows.length,
      players: new Set(rows.map((run) => run.install_id)).size,
      won: count("won"),
      lost: count("lost"),
      restart: count("restart"),
      abandoned: count("abandoned"),
      playing: count("playing"),
      medianSeconds: median(rows.map((run) => run.seconds)),
      medianWave: median(rows.map((run) => run.wave_reached)),
    };
  }, [rows]);
  const share = (n: number) => (stats.runs > 0 ? fmtPct(n / stats.runs) + " of runs" : undefined);

  return (
    <div>
      <PageHeader
        title="Runs"
        description="Every press of Start, from its first event to won, died, restarted or tab closed. Click a run to open its timeline."
      />

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <select aria-label="Outcome" value={outcome} onChange={(event) => setParam("outcome", event.target.value)} className={inputClass}>
          <option value="">All outcomes</option>
          {RUN_OUTCOMES.map((name) => (
            <option key={name} value={name}>
              {OUTCOME_LABELS[name]}
            </option>
          ))}
        </select>
        <select aria-label="Run number" value={runNumber} onChange={(event) => setParam("runNumber", event.target.value)} className={inputClass}>
          <option value="">All runs</option>
          <option value="first">First run only</option>
          <option value="repeat">Repeat runs</option>
        </select>
        <FilterInput
          value={installId}
          onCommit={(value) => setParam("installId", value.trim())}
          placeholder="Install id (exact)"
          label="Install id"
          width="w-60"
        />
        <FilterInput value={q} onCommit={(value) => setParam("q", value)} placeholder="Search ids or IP" label="Search run, install or session ids and IPs" />
        {extras.map(([name, value]) => (
          <button
            key={name + "=" + value}
            type="button"
            onClick={() => removeExtra(name, value)}
            className="inline-flex h-7 items-center gap-1 rounded-full border border-neutral-200 bg-white px-2.5 text-xs text-neutral-700 hover:bg-neutral-50"
            title="Remove this filter"
          >
            {name} = {value}
            <X className="h-3 w-3 text-neutral-400" />
          </button>
        ))}
        {hasFilters ? (
          <button type="button" onClick={() => router.replace("/runs", { scroll: false })} className={buttonClass}>
            <X className="h-3.5 w-3.5" /> Clear filters
          </button>
        ) : null}
        <span className="ml-auto text-xs text-neutral-500">
          Level: {global.level ? levelName(global.level) : "all"} · test plays {global.includeTest ? "included" : "hidden"} · {rangeLabel(global)} (top bar)
        </span>
      </div>

      {error ? (
        <div className="mb-4">
          <ErrorNote error={error} hint={errorHint(error)} />
        </div>
      ) : null}

      {data ? (
        <Hold loading={loading}>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-7">
            <StatTile
              label="Runs"
              value={fmtInt(stats.runs)}
              note={fmtInt(stats.players) + " players" + (stats.playing > 0 ? " · " + stats.playing + " playing now" : "")}
            />
            <StatTile label="Won" value={fmtInt(stats.won)} note={share(stats.won)} />
            <StatTile label="Lost" value={fmtInt(stats.lost)} note={share(stats.lost)} />
            <StatTile label="Restarted" value={fmtInt(stats.restart)} note={share(stats.restart)} />
            <StatTile label="Abandoned" value={fmtInt(stats.abandoned)} note="tab closed, no run end" />
            <StatTile label="Median time" value={fmtDuration(stats.medianSeconds)} note="play time (m:ss) per run" />
            <StatTile label="Median wave" value={fmt1(stats.medianWave)} note="highest wave reached" />
          </div>

          <Card className="mt-4">
            {rows.length === 0 ? (
              <EmptyState>
                {hasFilters ? "No runs match these filters." : "No runs yet: nobody has pressed Start."}
                {isTestInstall(installId) && !global.includeTest
                  ? " This is a test install (qa-): tick Include test plays in the top bar to see its runs."
                  : null}
              </EmptyState>
            ) : (
              <DataGrid
                title="Runs"
                rows={rows}
                columnDefs={COLUMNS as ColDef[]}
                height={620}
                fileName="runs"
                onRowClicked={(row) => {
                  // Selecting text in a cell (to copy it) is not a click on the run.
                  if (window.getSelection()?.toString()) {
                    return;
                  }
                  router.push("/runs/" + encodeURIComponent(String(row.run_id)));
                }}
              />
            )}
          </Card>
        </Hold>
      ) : loading ? (
        <Spinner label="Loading runs" />
      ) : null}
    </div>
  );
}
