"use client";
// The run timeline chart: stacked lanes sharing one t_play x-axis, a zoom
// slider, a linked crosshair, and click-to-inspect. The selected time is
// drawn by merging a tiny patch into the chart (no full re-render), and the
// zoom window survives option rebuilds (legend toggles, zombie selection).
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ECharts } from "echarts";
import { Maximize2 } from "lucide-react";
import Chart from "@/components/Chart";
import { buttonClass, inputClass } from "@/components/ui";
import { fmtT, type Timeline, type ZombieRowMode } from "@/lib/timelineData";
import { INK, STATUS } from "@/lib/theme";
import { buildTimelineOption, selectionSeriesPatch } from "./timelineOption";
import { END_GLYPH_LABELS, type EndGlyph } from "./timelineStyle";

// The glyph drawn at the end of a zombie bar (same shapes as in the chart).
function GlyphIcon({ glyph }: { glyph: EndGlyph }) {
  return (
    <svg width="11" height="11" viewBox="0 0 10 10" aria-hidden="true" className="shrink-0">
      {glyph === "dot" ? <circle cx="5" cy="5" r="3.5" fill={INK.primary} /> : null}
      {glyph === "diamond" ? <polygon points="5,0.5 9.5,5 5,9.5 0.5,5" fill={INK.primary} /> : null}
      {glyph === "cross" ? <path d="M1.5 1.5 L8.5 8.5 M1.5 8.5 L8.5 1.5" stroke={STATUS.critical} strokeWidth="2" /> : null}
      {glyph === "ring" ? <circle cx="5" cy="5" r="3" fill="#ffffff" stroke={INK.secondary} strokeWidth="1.2" /> : null}
    </svg>
  );
}

// How to read the zombie and typing lanes (the parts a legend cannot show).
function TimelineKey({ keyTimes }: { keyTimes: boolean }) {
  return (
    <div className="mb-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-neutral-600">
      <span className="font-medium text-neutral-800">Zombie bars:</span>
      <span>solid = typed at, faded = never typed, dashed = ambusher never seen; end of the bar =</span>
      {(Object.keys(END_GLYPH_LABELS) as EndGlyph[]).map((glyph) => (
        <span key={glyph} className="inline-flex items-center gap-1">
          <GlyphIcon glyph={glyph} /> {END_GLYPH_LABELS[glyph]}
        </span>
      ))}
      {keyTimes ? (
        <>
          <span className="ml-2 font-medium text-neutral-800">Typing:</span>
          <span>white ticks inside a word = each keystroke</span>
        </>
      ) : null}
    </div>
  );
}

type Props = {
  timeline: Timeline;
  selectedT: number | null;
  onSelectT: (t: number) => void;
  selectedZombie: string | null;
  onSelectZombie: (key: string | null) => void;
};

type ZoomWindow = { start: number; end: number };

export default function TimelineChart({ timeline, selectedT, onSelectT, selectedZombie, onSelectZombie }: Props) {
  const [rowMode, setRowMode] = useState<ZombieRowMode>(timeline.zombies.length <= 40 ? "each" : "packed");
  const [legendSelected, setLegendSelected] = useState<Record<string, boolean>>({});
  const [chart, setChart] = useState<ECharts | null>(null);
  const zoomRef = useRef<ZoomWindow | null>(null);
  const wrapRef = useRef<HTMLDivElement>(null);

  // ECharts' inside zoom swallows EVERY wheel event over the lanes (it checks
  // the Ctrl requirement only after blocking the page scroll), so the page
  // could not scroll over this tall chart. Plain wheel events are stopped
  // before they reach the chart and scroll the page; Ctrl + wheel zooms.
  useEffect(() => {
    const box = wrapRef.current;
    if (!box) return;
    const onWheel = (event: Event) => {
      const wheel = event as WheelEvent;
      if (!wheel.ctrlKey && !wheel.metaKey) {
        event.stopPropagation();
      }
    };
    for (const name of ["wheel", "mousewheel"]) {
      box.addEventListener(name, onWheel, { capture: true, passive: true });
    }
    return () => {
      for (const name of ["wheel", "mousewheel"]) {
        box.removeEventListener(name, onWheel, { capture: true });
      }
    };
  }, []);

  const model = useMemo(
    () => buildTimelineOption({ timeline, rowMode, selectedZombie, legendSelected }),
    [timeline, rowMode, selectedZombie, legendSelected],
  );

  // The chart's event handlers are bound once; they read the latest props here.
  const latest = useRef({ timeline, onSelectT, onSelectZombie, selectedZombie, gridCount: model.gridCount });
  useEffect(() => {
    latest.current = { timeline, onSelectT, onSelectZombie, selectedZombie, gridCount: model.gridCount };
  });

  const zoomTo = useCallback(
    (from: number, to: number) => {
      chart?.dispatchAction({ type: "dataZoom", dataZoomIndex: 0, startValue: Math.max(0, from), endValue: Math.min(timeline.duration, to) });
    },
    [chart, timeline.duration],
  );

  const onReady = useCallback((instance: ECharts) => {
    // A click anywhere inside a lane selects that time.
    instance.getZr().on("click", (event: { offsetX: number; offsetY: number }) => {
      const point = [event.offsetX, event.offsetY];
      for (let g = 0; g < latest.current.gridCount; g++) {
        if (instance.containPixel({ gridIndex: g }, point)) {
          const value = instance.convertFromPixel({ gridIndex: g }, point) as number[];
          const t = Math.max(0, Math.min(latest.current.timeline.duration, value[0]));
          latest.current.onSelectT(Math.round(t * 10) / 10);
          return;
        }
      }
    });
    // A click on a zombie or word selects the zombie; on a phase, zooms to it.
    instance.on("click", (raw) => {
      const params = raw as { seriesId?: string; value?: unknown };
      const id = params.seriesId ?? "";
      const value = Array.isArray(params.value) ? (params.value as number[]) : [];
      const { timeline: tl, onSelectZombie: selectZombie, selectedZombie: current } = latest.current;
      if (id.startsWith("zombies-")) {
        const zombie = tl.zombies[value[3]];
        if (zombie) selectZombie(zombie.key === current ? null : zombie.key);
      } else if (id.startsWith("words-")) {
        const word = tl.words[value[3]];
        if (word?.zombieKey) selectZombie(word.zombieKey === current ? null : word.zombieKey);
      } else if (id === "phases") {
        const phase = tl.phases[value[2]];
        if (phase) {
          const pad = Math.max(1, (phase.end - phase.start) * 0.04);
          instance.dispatchAction({ type: "dataZoom", dataZoomIndex: 0, startValue: Math.max(0, phase.start - pad), endValue: Math.min(tl.duration, phase.end + pad) });
        }
      }
    });
    // Remember the zoom window (in %) so a rebuilt option can restore it.
    instance.on("datazoom", (raw) => {
      const params = raw as { start?: number; end?: number; batch?: { start?: number; end?: number }[] };
      const zoom = params.batch?.[0] ?? params;
      if (typeof zoom.start === "number" && typeof zoom.end === "number") {
        zoomRef.current = { start: zoom.start, end: zoom.end };
        return;
      }
      const options = instance.getOption() as { dataZoom?: { start?: number; end?: number }[] };
      const current = options.dataZoom?.[0];
      if (current && typeof current.start === "number" && typeof current.end === "number") {
        zoomRef.current = { start: current.start, end: current.end };
      }
    });
    instance.on("legendselectchanged", (raw) => {
      const params = raw as { selected: Record<string, boolean> };
      setLegendSelected({ ...params.selected });
    });
    setChart(instance);
  }, []);

  // After every option change (the Chart replaced the whole option) and on
  // every selection: restore the zoom window and draw the selected time.
  useEffect(() => {
    if (!chart) return;
    const zoom = zoomRef.current;
    if (zoom && (zoom.start > 0 || zoom.end < 100)) {
      chart.dispatchAction({ type: "dataZoom", dataZoomIndex: 0, start: zoom.start, end: zoom.end });
    }
    chart.setOption(selectionSeriesPatch(selectedT));
  }, [chart, model, selectedT]);

  return (
    <div>
      <div className="mb-2 flex flex-wrap items-center gap-2 text-sm">
        <button type="button" className={buttonClass} onClick={() => zoomTo(0, timeline.duration)} disabled={!chart}>
          <Maximize2 className="h-3.5 w-3.5" /> Whole run
        </button>
        <select
          className={inputClass}
          value=""
          disabled={!chart || timeline.phases.length === 0}
          onChange={(event) => {
            const phase = timeline.phases[Number(event.target.value)];
            if (phase) zoomTo(phase.start - 1, phase.end + 1);
          }}
          aria-label="Zoom to a phase"
        >
          <option value="">Zoom to a phase…</option>
          {timeline.phases.map((phase, i) => (
            <option key={i} value={i}>
              {phase.label} ({fmtT(phase.start)} – {fmtT(phase.end)})
            </option>
          ))}
        </select>
        <div className="flex rounded-md border border-neutral-200 bg-white p-0.5 text-xs" role="group" aria-label="Zombie rows">
          {(["each", "packed"] as const).map((mode) => (
            <button
              key={mode}
              type="button"
              onClick={() => setRowMode(mode)}
              className={"rounded px-2 py-1 " + (rowMode === mode ? "bg-neutral-100 font-medium text-neutral-900" : "text-neutral-500")}
            >
              {mode === "each" ? "One row per zombie" : "Packed rows"}
            </button>
          ))}
        </div>
        {selectedZombie ? (
          <button type="button" className={buttonClass} onClick={() => onSelectZombie(null)}>
            Clear zombie highlight
          </button>
        ) : null}
        <span className="ml-auto text-xs text-neutral-500">
          Click: inspect a moment · drag: pan · Ctrl + wheel or slider: zoom · click a phase: zoom to it · click a zombie: highlight its words
        </span>
      </div>
      <TimelineKey keyTimes={timeline.schema.keyTimes} />
      <div ref={wrapRef}>
        <Chart option={model.option} height={model.height} onReady={onReady} ariaLabel="Run timeline: phases, health, combo, zombies, typing, events and typing speed over play time" />
      </div>
    </div>
  );
}
