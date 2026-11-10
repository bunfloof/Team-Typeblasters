"use client";
// /runs/[runId]: one run, second by second. Loads /api/runs/[runId] and lays
// out the header, the timeline (with its table twin), the Moment panel, the
// map and every event as a table. The selected time is shared by all of them
// and kept in the URL (?t=83.4) so a moment can be linked.
import { useCallback, useMemo, useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { BarChart3, Table2 } from "lucide-react";
import { Card, ErrorNote, Hold, Spinner, buttonClass } from "@/components/ui";
import { apiUrl, useApi } from "@/lib/useApi";
import { globalParams, useGlobalFilters } from "@/lib/globalFilters";
import { shortId } from "@/lib/format";
import { buildTimeline, momentAt, type RunDetailResponse } from "@/lib/timelineData";
import RunHeader from "./RunHeader";
import TimelineChart from "./TimelineChart";
import TimelineTables from "./TimelineTables";
import { MomentPanel, MomentStrip } from "./MomentPanel";
import RunMap from "./RunMap";
import RunEventsTable from "./RunEventsTable";

const MOMENT_WINDOW = 3; // seconds around the selected time listed in the Moment panel

export default function RunTimelinePage() {
  const params = useParams<{ runId: string }>();
  const searchParams = useSearchParams();
  const runId = params.runId ? decodeURIComponent(params.runId) : "";
  const filters = useGlobalFilters();
  const { data, error, loading, reload } = useApi<RunDetailResponse>(runId ? apiUrl("/api/runs/" + encodeURIComponent(runId), globalParams(filters)) : null);

  if (error && (!data || data.summary.run_id !== runId)) {
    const config = error.includes(".env.local");
    return (
      <div className="flex flex-col gap-3">
        <ErrorNote
          error={error}
          hint={
            config
              ? "Fill DB_PASSWORD (and the other DB_ settings) in .env.local, then restart the dashboard (pnpm dev, or pnpm start on the server)."
              : error.startsWith("No events")
                ? "Check the run id, or pick a run on the Runs page."
                : undefined
          }
        />
        <div>
          <button type="button" className={buttonClass} onClick={reload}>
            Try again
          </button>
        </div>
      </div>
    );
  }
  if (!data) {
    return <Spinner label={"Loading run " + shortId(runId)} />;
  }
  const t = Number(searchParams.get("t"));
  return (
    <Hold loading={loading}>
      {error ? (
        <div className="mb-3">
          <ErrorNote error={error} hint="Showing the last data that loaded." />
        </div>
      ) : null}
      {/* A new run starts with a fresh selection, zoom and highlight. */}
      <RunView key={data.summary.run_id} data={data} initialT={searchParams.has("t") && Number.isFinite(t) ? t : null} />
    </Hold>
  );
}

function RunView({ data, initialT }: { data: RunDetailResponse; initialT: number | null }) {
  const timeline = useMemo(() => buildTimeline(data.events, data.summary), [data]);
  const [selectedT, setSelectedT] = useState<number | null>(initialT);
  const [selectedZombie, setSelectedZombie] = useState<string | null>(null);
  const [view, setView] = useState<"chart" | "tables">("chart");
  const moment = useMemo(() => (selectedT === null ? null : momentAt(timeline, selectedT, MOMENT_WINDOW)), [timeline, selectedT]);
  const fileName = "run-" + data.summary.run_id.slice(0, 8);

  const selectT = useCallback((t: number | null) => {
    setSelectedT(t);
    // Keep the moment in the address bar (a shareable link), without a navigation.
    const url = new URL(window.location.href);
    if (t === null) url.searchParams.delete("t");
    else url.searchParams.set("t", String(Math.round(t * 10) / 10));
    window.history.replaceState(null, "", url.pathname + url.search);
  }, []);

  const notes: string[] = [];
  if (!timeline.schema.spawns) notes.push("zombie appear times are estimated from seconds_alive");
  if (!timeline.schema.wordStarts) notes.push("word bars start at end − typing_seconds");
  if (!timeline.hasSamples) notes.push("no 1 s heartbeat, so health and combo only change at logged events");
  if (!timeline.schema.keyTimes) notes.push("no per-key times (key_ms)");

  return (
    <div className="flex flex-col gap-4">
      <RunHeader data={data} />

      {data.hiddenBy.length > 0 ? (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm text-amber-900">
          The other pages hide this run because {data.hiddenBy.join(" and ")}. It is shown here because you opened it directly.
        </div>
      ) : null}

      <Card
        title="Timeline"
        subtitle={
          <>
            Every lane shares the same clock: seconds of play in this run (pauses and tip boxes excluded).
            {notes.length > 0 ? <> Older game build: {notes.join("; ")}.</> : null}
          </>
        }
        actions={
          <div className="flex rounded-md border border-neutral-200 bg-white p-0.5">
            {(["chart", "tables"] as const).map((mode) => (
              <button
                key={mode}
                type="button"
                onClick={() => setView(mode)}
                className={"rounded px-2 py-1 " + (view === mode ? "bg-neutral-100 text-neutral-900" : "text-neutral-500")}
                aria-label={mode === "chart" ? "Timeline view" : "Table view"}
                title={mode === "chart" ? "Timeline" : "The numbers behind each lane"}
              >
                {mode === "chart" ? <BarChart3 className="h-3.5 w-3.5" /> : <Table2 className="h-3.5 w-3.5" />}
              </button>
            ))}
          </div>
        }
      >
        <div className="sticky top-[53px] z-10 -mx-5 mb-3 border-y border-black/5 bg-[#fcfcfb]/95 px-5 py-2 backdrop-blur">
          <MomentStrip moment={moment} onClear={() => selectT(null)} />
        </div>
        {view === "chart" ? (
          <TimelineChart timeline={timeline} selectedT={selectedT} onSelectT={selectT} selectedZombie={selectedZombie} onSelectZombie={setSelectedZombie} />
        ) : (
          <TimelineTables timeline={timeline} fileName={fileName} onSelectT={selectT} />
        )}
      </Card>

      <div className="grid gap-4 xl:grid-cols-2">
        <MomentPanel
          moment={moment}
          duration={timeline.duration}
          onSelectT={selectT}
          selectedZombie={selectedZombie}
          onSelectZombie={setSelectedZombie}
          windowSeconds={MOMENT_WINDOW}
        />
        <RunMap map={timeline.map} position={moment?.position ?? null} selectedT={selectedT} onSelectT={selectT} fileName={fileName + "-map"} />
      </div>

      <Card title="All events of this run" subtitle="Every logged event in order, data fields as columns. Click a row to inspect its moment; tick rows to export them.">
        <RunEventsTable timeline={timeline} fileName={fileName + "-events"} onSelectT={selectT} />
      </Card>
    </div>
  );
}
