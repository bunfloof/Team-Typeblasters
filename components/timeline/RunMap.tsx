"use client";
// Top-down map of the run (x east, z north, metres, same scale on both
// axes): the player's path, zombies where they appeared and where they went,
// explosions sized by kills, hits on the player, and the player at the
// selected time. Toggles to a table of every plotted point (CSV export).
import { useEffect, useMemo, useRef, useState } from "react";
import type { EChartsOption, ScatterSeriesOption, LineSeriesOption } from "echarts";
import { BarChart3, Table2 } from "lucide-react";
import Chart from "@/components/Chart";
import DataGrid from "@/components/DataGrid";
import { Card, EmptyState } from "@/components/ui";
import { ZOMBIE_KINDS } from "@/lib/catalog";
import { AXIS_STYLE, FONT, INK, LEGEND_STYLE, STATUS, TOOLTIP_STYLE } from "@/lib/theme";
import { fmtT, type MapData } from "@/lib/timelineData";
import { CROSS_SYMBOL, MARKER_STYLE, PLAYER_DOT, ZOMBIE_OUTCOME_LABELS, endGlyph, kindColor, kindLabel } from "./timelineStyle";
import { tipHtml } from "./timelineOption";

type Props = {
  map: MapData;
  position: { x: number; z: number } | null; // the player at the selected time
  selectedT: number | null;
  onSelectT: (t: number) => void;
  fileName: string;
};

const HEIGHT = 440;
const PAD = { left: 56, right: 20, top: 34, bottom: 44 };

const END_SYMBOL: Record<ReturnType<typeof endGlyph>, string> = {
  dot: "circle",
  diamond: "diamond",
  cross: CROSS_SYMBOL,
  ring: "rect",
};

// Axis ranges that keep 1 m the same length on x and z for a plot of this size.
function equalScaleRanges(extent: NonNullable<MapData["extent"]>, width: number) {
  const plotW = Math.max(50, width - PAD.left - PAD.right);
  const plotH = HEIGHT - PAD.top - PAD.bottom;
  const spanX = Math.max(10, extent.maxX - extent.minX) * 1.08;
  const spanZ = Math.max(10, extent.maxZ - extent.minZ) * 1.08;
  const metresPerPx = Math.max(spanX / plotW, spanZ / plotH);
  const cx = (extent.minX + extent.maxX) / 2;
  const cz = (extent.minZ + extent.maxZ) / 2;
  return {
    x: [cx - (metresPerPx * plotW) / 2, cx + (metresPerPx * plotW) / 2],
    z: [cz - (metresPerPx * plotH) / 2, cz + (metresPerPx * plotH) / 2],
  };
}

export default function RunMap({ map, position, selectedT, onSelectT, fileName }: Props) {
  const [view, setView] = useState<"chart" | "table">("chart");
  const [width, setWidth] = useState(640);
  const boxRef = useRef<HTMLDivElement>(null);
  const selectRef = useRef(onSelectT);
  useEffect(() => {
    selectRef.current = onSelectT;
  });

  // The plot width decides the axis ranges (equal scale), so follow it.
  useEffect(() => {
    const box = boxRef.current;
    if (!box) return;
    const observer = new ResizeObserver((entries) => {
      const next = Math.round(entries[0].contentRect.width);
      if (next > 0) setWidth(next);
    });
    observer.observe(box);
    return () => observer.disconnect();
  }, [view]);

  const option = useMemo<EChartsOption | null>(() => {
    if (!map.extent) return null;
    const ranges = equalScaleRanges(map.extent, width);
    const tips: Record<string, (i: number) => string> = {};
    const series: (ScatterSeriesOption | LineSeriesOption)[] = [];

    series.push({
      id: "path",
      type: "line",
      name: "Player path",
      data: map.path.map((p) => [p.x, p.z, p.t]),
      showSymbol: true,
      showAllSymbol: true,
      symbolSize: 6,
      itemStyle: { color: INK.muted, opacity: 0 },
      emphasis: { itemStyle: { opacity: 1 } },
      lineStyle: { color: INK.muted, width: 2 },
      z: 1,
    });
    tips.path = (i) => tipHtml("Player", [["At", fmtT(map.path[i].t)], ["x, z", map.path[i].x.toFixed(1) + ", " + map.path[i].z.toFixed(1)]], INK.muted);

    const kinds = ZOMBIE_KINDS.filter((k) => map.zombieSpawns.some((z) => z.kind === k) || map.zombieEnds.some((z) => z.kind === k));
    for (const kind of kinds) {
      const spawns = map.zombieSpawns.filter((z) => z.kind === kind);
      const ends = map.zombieEnds.filter((z) => z.kind === kind);
      const name = kindLabel(kind);
      const color = kindColor(kind);
      // Appeared: a hollow ring; gone: a filled shape by how it left.
      series.push({
        id: "spawn-" + kind,
        type: "scatter",
        name,
        data: spawns.map((z) => [z.x, z.z, z.t]),
        symbol: "emptyCircle",
        symbolSize: 8,
        itemStyle: { color, borderWidth: 1.5 },
        z: 3,
      });
      tips["spawn-" + kind] = (i) => tipHtml(name + " zombie appeared", [["Word", spawns[i].word], ["At", fmtT(spawns[i].t)]], color);
      series.push({
        id: "end-" + kind,
        type: "scatter",
        name,
        data: ends.map((z) => ({
          value: [z.x, z.z, z.t],
          symbol: END_SYMBOL[endGlyph(z.outcome)],
          itemStyle: z.outcome === "bite" ? { color: STATUS.critical } : { color },
        })),
        symbolSize: 9,
        itemStyle: { color, borderColor: "#ffffff", borderWidth: 1 },
        z: 4,
      });
      tips["end-" + kind] = (i) => tipHtml(name + " zombie gone", [["Word", ends[i].word], ["How", ZOMBIE_OUTCOME_LABELS[ends[i].outcome] ?? ends[i].outcome], ["At", fmtT(ends[i].t)]], color);
    }

    if (map.props.length > 0) {
      series.push({ id: "props", type: "scatter", name: "Barrels & crates", data: map.props.map((p) => [p.x, p.z, p.t]), symbol: "rect", symbolSize: 7, itemStyle: { color: INK.axis }, z: 2 });
      tips.props = (i) => tipHtml(map.props[i].kind.replace("_", " "), [["Word", map.props[i].word], ["Armed at", fmtT(map.props[i].t)]], INK.axis);
    }
    if (map.blasts.length > 0) {
      const color = MARKER_STYLE.blast.color;
      series.push({
        id: "blasts",
        type: "scatter",
        name: "Explosions",
        data: map.blasts.map((b) => ({ value: [b.x, b.z, b.t], symbolSize: 10 + Math.min(b.kills, 8) * 3 })),
        itemStyle: { color: color + "33", borderColor: color, borderWidth: 1.5 },
        z: 5,
      });
      tips.blasts = (i) => tipHtml("Explosion (" + map.blasts[i].source + ")", [["Kills", map.blasts[i].kills], ["At", fmtT(map.blasts[i].t)], ["Radius", map.blasts[i].radius !== null ? map.blasts[i].radius + " m" : null]], color);
    }
    if (map.hits.length > 0) {
      series.push({
        id: "hits",
        type: "scatter",
        name: "Hits on the player",
        data: map.hits.map((h) => ({
          value: [h.x, h.z, h.t],
          symbolSize: h.fatal ? 18 : 10,
          label: h.fatal ? { show: true, position: "right", formatter: "Fatal", color: INK.primary, fontWeight: 600, fontSize: 11, fontFamily: FONT } : undefined,
        })),
        symbol: CROSS_SYMBOL,
        itemStyle: { color: STATUS.critical },
        z: 6,
      });
      tips.hits = (i) =>
        tipHtml((map.hits[i].fatal ? "Fatal hit" : "Hit") + " by " + map.hits[i].source.replace("zombie_", ""), [["At", fmtT(map.hits[i].t)]], STATUS.critical, map.hits[i].estimated ? "Position interpolated from the player path." : undefined);
    }
    if (position && selectedT !== null) {
      series.push({
        id: "me",
        type: "scatter",
        name: "Player at the selected time",
        data: [[position.x, position.z, selectedT]],
        symbolSize: 13,
        itemStyle: { color: PLAYER_DOT, borderColor: "#ffffff", borderWidth: 2 },
        z: 10,
      });
      tips.me = () => tipHtml("Player at " + fmtT(selectedT), [["x, z", position.x.toFixed(1) + ", " + position.z.toFixed(1)]], PLAYER_DOT);
    }

    const axis = (name: string, range: number[]) => ({
      type: "value" as const,
      name,
      nameLocation: "middle" as const,
      min: range[0],
      max: range[1],
      ...AXIS_STYLE,
      axisLabel: { ...AXIS_STYLE.axisLabel, showMinLabel: false, showMaxLabel: false, formatter: (v: number) => String(Math.round(v)) },
    });

    return {
      animation: false,
      textStyle: { fontFamily: FONT },
      grid: { left: PAD.left, right: PAD.right, top: PAD.top, bottom: PAD.bottom },
      legend: { ...LEGEND_STYLE, icon: undefined, top: 0, left: PAD.left, right: PAD.right, type: "scroll", textStyle: { ...LEGEND_STYLE.textStyle, fontSize: 11 } },
      tooltip: {
        ...TOOLTIP_STYLE,
        trigger: "item",
        confine: true,
        formatter: (params) => {
          const p = (Array.isArray(params) ? params[0] : params) as { seriesId?: string; dataIndex: number };
          const make = p.seriesId ? tips[p.seriesId] : undefined;
          return make ? make(p.dataIndex) : "";
        },
      },
      xAxis: { ...axis("x (m, east)", ranges.x), nameGap: 26 },
      yAxis: { ...axis("z (m, north)", ranges.z), nameGap: 38 },
      series,
    };
  }, [map, width, position, selectedT]);

  // Clicking any point selects its time (value[2]).
  const onEvents = useMemo(
    () => ({
      click: (raw: unknown) => {
        const value = (raw as { value?: unknown }).value;
        if (Array.isArray(value) && typeof value[2] === "number") {
          selectRef.current(value[2]);
        }
      },
    }),
    [],
  );

  const rows = useMemo(
    () => [
      ...map.path.map((p) => ({ what: "player", t: p.t, x: p.x, z: p.z, kind: null, outcome: null, word: null, kills: null })),
      ...map.zombieSpawns.map((z) => ({ what: "zombie appeared", t: z.t, x: z.x, z: z.z, kind: z.kind, outcome: null, word: z.word, kills: null })),
      ...map.zombieEnds.map((z) => ({ what: "zombie gone", t: z.t, x: z.x, z: z.z, kind: z.kind, outcome: z.outcome, word: z.word, kills: null })),
      ...map.blasts.map((b) => ({ what: "explosion", t: b.t, x: b.x, z: b.z, kind: b.source, outcome: null, word: null, kills: b.kills })),
      ...map.hits.map((h) => ({ what: h.fatal ? "fatal hit" : "hit", t: h.t, x: h.x, z: h.z, kind: h.source, outcome: null, word: null, kills: null })),
      ...map.props.map((p) => ({ what: "prop", t: p.t, x: p.x, z: p.z, kind: p.kind, outcome: null, word: p.word, kills: null })),
    ],
    [map],
  );

  const toggle = (
    <div className="flex rounded-md border border-neutral-200 bg-white p-0.5">
      {(["chart", "table"] as const).map((mode) => (
        <button
          key={mode}
          type="button"
          onClick={() => setView(mode)}
          className={"rounded px-2 py-1 " + (view === mode ? "bg-neutral-100 text-neutral-900" : "text-neutral-500")}
          aria-label={mode === "chart" ? "Map view" : "Table view"}
          title={mode === "chart" ? "Map" : "Table"}
        >
          {mode === "chart" ? <BarChart3 className="h-3.5 w-3.5" /> : <Table2 className="h-3.5 w-3.5" />}
        </button>
      ))}
    </div>
  );

  return (
    <Card title="Map" subtitle="From above, in metres (equal scale). Click a point to jump to its time." actions={map.hasPositions ? toggle : undefined}>
      {!map.hasPositions || !option ? (
        <EmptyState>
          No positions in this run yet. The game build that played it did not log x / z: the player path, where zombies appeared and died, and where
          explosions went off arrive with the new build (sample, zombie_spawn and zombie events with positions).
        </EmptyState>
      ) : view === "chart" ? (
        <div ref={boxRef}>
          <Chart option={option} height={HEIGHT} onEvents={onEvents} ariaLabel="Map of the run: player path, zombies, explosions and hits" />
          <p className="mt-2 text-xs text-neutral-500">
            Zombies: ○ where it appeared, then where it went: ● shot, ◆ blown up, ✕ bit the player (red), ■ other. Explosion circles grow with their kills.
          </p>
        </div>
      ) : (
        <DataGrid rows={rows} height={HEIGHT} fileName={fileName} />
      )}
    </Card>
  );
}
