// Builds the ECharts option of the run timeline: several grids stacked
// vertically that share one x-axis (t_play seconds), one zoom slider and a
// linked crosshair. Lanes, top to bottom:
//   phases · health · combo · zombies alive · zombies (Gantt) · typing · events · typing speed
// No lane has two y-axes. Pure (no React): the component memoises the result.
import type {
  CustomSeriesOption,
  CustomSeriesRenderItemAPI,
  CustomSeriesRenderItemParams,
  CustomSeriesRenderItemReturn,
  DataZoomComponentOption,
  EChartsOption,
  GridComponentOption,
  LegendComponentOption,
  LineSeriesOption,
  ScatterSeriesOption,
  TitleComponentOption,
  TooltipComponentOption,
  XAXisComponentOption,
  YAXisComponentOption,
} from "echarts";
import { ZOMBIE_KINDS } from "@/lib/catalog";
import { AXIS_STYLE, FONT, INK, LEGEND_STYLE, STATUS, TOOLTIP_STYLE } from "@/lib/theme";
import {
  MARKER_CATEGORIES,
  dataBool,
  dataNum,
  dataStr,
  fmtClock,
  fmtT,
  tOf,
  zombieRows,
  type MarkerCategory,
  type PhaseSegment,
  type SeriesPoint,
  type Timeline,
  type WordBar,
  type ZombieBar,
  type ZombieRowMode,
} from "@/lib/timelineData";
import {
  CROSS_SYMBOL,
  LINE_INK,
  MARKER_STYLE,
  PHASE_STYLE,
  SELECTION_INK,
  WORD_OUTCOMES,
  WORD_OUTCOME_LABELS,
  WRONG_KEY_STYLE,
  ZOMBIE_OUTCOME_LABELS,
  endGlyph,
  kindColor,
  kindLabel,
  wordOutcomeGroup,
} from "./timelineStyle";

export const LANES = ["phases", "health", "combo", "alive", "zombies", "typing", "events", "wpm"] as const;
export type LaneId = (typeof LANES)[number];
const GRID = Object.fromEntries(LANES.map((lane, i) => [lane, i])) as Record<LaneId, number>;

const LEFT = 132; // room for the y-axis labels ("Powers & weapons")
const RIGHT = 24;
const TITLE_GAP = 30; // space above each lane for its title and legend

export type TimelineOptionInput = {
  timeline: Timeline;
  rowMode: ZombieRowMode;
  selectedZombie: string | null;
  legendSelected: Record<string, boolean>;
};

export type TimelineModel = {
  option: EChartsOption;
  height: number;
  gridCount: number;
};

// ---------------------------------------------------------------------------
// Small helpers
// ---------------------------------------------------------------------------

type Box = { x: number; y: number; width: number; height: number };

function coordBox(params: CustomSeriesRenderItemParams): Box {
  return params.coordSys as unknown as Box;
}

// The part of [x0, x1] inside the plot (null when entirely outside).
function clipX(x0: number, x1: number, box: Box): [number, number] | null {
  const a = Math.max(x0, box.x);
  const b = Math.min(x1, box.x + box.width);
  return b < a ? null : [a, b];
}

function withAlpha(hex: string, alpha: number): string {
  const n = parseInt(hex.slice(1), 16);
  return "rgba(" + ((n >> 16) & 255) + "," + ((n >> 8) & 255) + "," + (n & 255) + "," + alpha + ")";
}

// Rough text width (canvas text is not measured inside renderItem).
function textWidth(text: string, fontSize: number): number {
  return text.length * fontSize * 0.64;
}

function bandHeight(api: CustomSeriesRenderItemAPI): number {
  const size = api.size ? api.size([0, 1]) : 0;
  return Array.isArray(size) ? size[1] : size;
}

function esc(value: unknown): string {
  return String(value ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c] as string);
}

type TipRow = [string, string | number | null | undefined | false];

// Tooltip HTML: a title (with a colored swatch) and label / value rows.
export function tipHtml(title: string, rows: TipRow[], swatch?: string, note?: string): string {
  const dot = swatch ? '<span style="display:inline-block;width:9px;height:9px;border-radius:3px;margin-right:6px;background:' + swatch + '"></span>' : "";
  let html = '<div style="font-weight:600;margin-bottom:4px;max-width:340px;white-space:normal">' + dot + esc(title) + "</div>";
  for (const [label, value] of rows) {
    if (value === null || value === undefined || value === false || value === "") continue;
    html +=
      '<div style="display:flex;justify-content:space-between;gap:18px;line-height:1.55"><span style="color:' +
      INK.muted +
      '">' +
      esc(label) +
      '</span><span style="font-variant-numeric:tabular-nums;max-width:240px;text-align:right;white-space:normal">' +
      esc(value) +
      "</span></div>";
  }
  if (note) {
    html += '<div style="margin-top:4px;max-width:320px;white-space:normal;color:' + INK.muted + ';font-size:11px">' + esc(note) + "</div>";
  }
  return html;
}

function yesNo(value: boolean): string {
  return value ? "yes" : "no";
}

function seconds(value: number | null | undefined): string | null {
  return value === null || value === undefined ? null : (Math.round(value * 10) / 10).toString() + " s";
}

// Axis labels: m:ss, with tenths when zoomed in below a second.
function axisTime(value: number): string {
  return Number.isInteger(value) ? fmtClock(value) : fmtT(value);
}

// ---------------------------------------------------------------------------
// Tooltip texts
// ---------------------------------------------------------------------------

const SPAWN_SOURCE_NOTES: Record<ZombieBar["spawnSource"], string | undefined> = {
  spawn_event: undefined,
  seconds_since_wake: "Appear time estimated: end time minus seconds since the player saw it.",
  seconds_alive: "Appear time estimated: end time minus seconds_alive (older game build without zombie_spawn).",
  unknown: "Appear time unknown.",
};

function zombieTip(z: ZombieBar): string {
  const red = z.kind === "explosive";
  return tipHtml(
    kindLabel(z.kind) + " zombie" + (z.zombieId !== null ? " #" + z.zombieId : ""),
    [
      ["Word" + (z.words.length > 1 ? "s" : ""), z.words.join(" → ") || z.word],
      ["Appeared", fmtT(z.spawnT) + (z.spawnSource === "spawn_event" ? "" : " (est.)")],
      ["Gone", z.ended ? fmtT(z.endT) : "still alive"],
      ["How", ZOMBIE_OUTCOME_LABELS[z.outcome] ?? z.outcome],
      ["Alive for", seconds(z.endT - z.spawnT)],
      ["Typed at", yesNo(z.wasTargeted) + (z.typingStarts ? " · " + z.typingStarts + " start" + (z.typingStarts > 1 ? "s" : "") : "")],
      ["Letters typed", z.lettersTyped !== null && z.wordLength !== null ? z.lettersTyped + " of " + z.wordLength : null],
      ["Word attempts", z.attempts.length > 0 ? z.attempts.length : null],
      ["Ambusher", z.ambusher && "yes (met on a ride)"],
      ["WORD CHAIN", z.chainLinked && "yes"],
      ["FRENZY word", z.frenzyWord && "yes"],
      ["Word shifts", z.wordShifts],
      ["Blast radius", red && z.blastRadius !== null ? z.blastRadius + " m" : null],
      ["In ring at 1st letter", red ? z.ring.atFirstLetter : null],
      ["In ring at last start", red ? z.ring.atLastStart : null],
      ["In ring, max while typed", red ? z.ring.maxWhileTyped : null],
      ["In ring, max", red ? z.ring.max : null],
      ["Wave", z.wave],
    ],
    kindColor(z.kind),
    SPAWN_SOURCE_NOTES[z.spawnSource],
  );
}

function wordTip(w: WordBar, zombies: Map<string, ZombieBar>): string {
  const zombie = w.zombieKey ? zombies.get(w.zombieKey) : undefined;
  // n keys typed over n-1 gaps: the first key starts the clock.
  const typingWpm = w.typingSeconds && w.typingSeconds > 0 && w.correctKeys > 1 ? Math.round(((w.correctKeys - 1) / 5) / (w.typingSeconds / 60)) : null;
  const group = WORD_OUTCOMES.find((o) => o.id === wordOutcomeGroup(w.outcome));
  return tipHtml(
    w.word || "(word)",
    [
      ["Target", w.target + (zombie ? " · " + (zombie.zombieId !== null ? "#" + zombie.zombieId + " " : "") + (zombie.words[0] ?? "") : w.targetId !== null ? " #" + w.targetId : "")],
      ["Outcome", WORD_OUTCOME_LABELS[w.outcome] ?? w.outcome],
      ["First key", fmtT(w.start) + (w.startSource === "start_t_play" ? "" : w.startSource === "typing_seconds" ? " (est.)" : " (unknown)")],
      ["Ended", fmtT(w.end)],
      ["Correct keys", w.correctKeys],
      ["Wrong keys", w.wrongKeys || null],
      ["Typing time", seconds(w.typingSeconds)],
      ["Speed while typing", typingWpm !== null ? "≈ " + typingWpm + " WPM" : null],
      ["Keystrokes logged", w.keyTimes ? w.keyTimes.length : null],
      ["Rockets", w.rockets || null],
      ["Words finished", w.wordsFinished !== null && w.wordsFinished > 1 ? w.wordsFinished + " (WORD CHAIN)" : null],
      ["Candidates", w.candidates !== null && w.candidates > 1 ? w.candidates + " matched the 1st letter" : null],
      ["Case-sensitive", w.caseSensitive ? "yes (" + (w.wordStyle ?? "") + ")" : null],
      ["FRENZY on", w.frenzyOn && "yes"],
    ],
    group?.color,
    w.startSource === "typing_seconds" ? "Older build: the bar starts at end − typing_seconds (exact for finished words)." : undefined,
  );
}

function phaseTip(p: PhaseSegment): string {
  const s = p.startEvent;
  const e = p.endEvent;
  const n = (ev: typeof s, field: string) => (ev ? dataNum(ev, field) : null);
  const planned = s && p.kind !== "ride"
    ? ZOMBIE_KINDS.map((kind) => [kind, n(s, "planned_" + kind)] as const)
        .filter(([, count]) => count)
        .map(([kind, count]) => count + " " + kindLabel(kind).toLowerCase())
        .join(", ")
    : null;
  const endedBy: Record<PhaseSegment["endedBy"], string | undefined> = {
    end_event: undefined,
    next_phase: "No end event: closed when the next phase started.",
    run_end: "Ended by the run ending (death or restart).",
    last_event: "The log stops here (tab closed, or still playing).",
  };
  return tipHtml(
    p.label,
    [
      ["Time", fmtT(p.start) + " – " + fmtT(p.end)],
      ["Duration", seconds(p.end - p.start)],
      ["Zombies planned", n(s, "zombies_planned")],
      ["Mix", planned],
      ["Zombie speed", n(s, "zombie_speed") !== null ? n(s, "zombie_speed") + " m/s" : null],
      ["Barrels / crates", s && p.kind !== "ride" ? (n(s, "barrels") ?? 0) + " / " + (n(s, "crates") ?? 0) : null],
      ["Boss health", n(s, "boss_health")],
      ["Kills", n(e, "kills")],
      ["Keys", e && n(e, "correct_keys") !== null ? n(e, "correct_keys") + " correct · " + (n(e, "wrong_keys") ?? 0) + " wrong" : null],
      ["Ambushers", e && p.kind === "ride" && n(e, "ambushers_placed") !== null ? n(e, "ambushers_woken") + " woken of " + n(e, "ambushers_placed") : null],
      ["Health", n(e, "health_at_start") !== null ? n(e, "health_at_start") + " → " + (n(e, "health") ?? "?") : null],
    ],
    PHASE_STYLE[p.kind].fill,
    (endedBy[p.endedBy] ?? "") + " Click to zoom to this phase.",
  );
}

function pointTip(title: string, p: SeriesPoint, unit = ""): string {
  return tipHtml(title + " " + p.v + unit, [["At", fmtT(p.t)], ["From", p.event.replace(/_/g, " ") + (p.note ? " (" + p.note + ")" : "")]]);
}

// ---------------------------------------------------------------------------
// renderItem functions (custom series)
// ---------------------------------------------------------------------------

function textEl(text: string, x: number, y: number, fill: string, fontSize = 10.5, weight = 600) {
  return { type: "text" as const, silent: true, style: { text, x, y, fill, font: weight + " " + fontSize + "px " + FONT, verticalAlign: "middle" as const, align: "left" as const } };
}

function makePhaseRenderer(phases: PhaseSegment[]) {
  return (params: CustomSeriesRenderItemParams, api: CustomSeriesRenderItemAPI): CustomSeriesRenderItemReturn => {
    const p = phases[api.value(2) as number];
    const box = coordBox(params);
    const [x0] = api.coord([p.start, 0]);
    const [x1] = api.coord([p.end, 0]);
    const span = clipX(x0, Math.max(x1, x0 + 1), box);
    if (!span) return null;
    const style = PHASE_STYLE[p.kind];
    const width = span[1] - span[0];
    const children: object[] = [
      { type: "rect", shape: { x: span[0], y: box.y, width, height: box.height, r: 3 }, style: { fill: style.fill, stroke: "#ffffff", lineWidth: 1 } },
    ];
    const label = width > textWidth(p.label, 10.5) + 10 ? p.label : p.wave !== null && width > 22 ? (p.kind === "ride" ? "R" : "W") + p.wave : "";
    if (label) {
      children.push(textEl(label, span[0] + 5, box.y + box.height / 2, style.text));
    }
    return { type: "group", children } as CustomSeriesRenderItemReturn;
  };
}

function makeZombieRenderer(zombies: ZombieBar[], rows: number[], selected: string | null) {
  return (params: CustomSeriesRenderItemParams, api: CustomSeriesRenderItemAPI): CustomSeriesRenderItemReturn => {
    const index = api.value(3) as number;
    const z = zombies[index];
    const box = coordBox(params);
    const [x0, yc] = api.coord([z.spawnT, rows[index]]);
    const [x1] = api.coord([z.endT, rows[index]]);
    const span = clipX(x0, Math.max(x1, x0 + 2), box);
    if (!span) return null;
    const h = Math.max(3, Math.min(13, bandHeight(api) * 0.74));
    const color = kindColor(z.kind);
    const isSelected = z.key === selected;
    const children: object[] = [
      {
        type: "rect",
        shape: { x: span[0], y: yc - h / 2, width: span[1] - span[0], height: h, r: Math.min(3, h / 2) },
        style: {
          // Typed-at zombies are solid; never-typed ones faded; never-seen ambushers outlined.
          fill: z.ghost ? "rgba(255,255,255,0)" : z.wasTargeted ? color : withAlpha(color, 0.32),
          stroke: isSelected ? SELECTION_INK : z.ghost ? color : null,
          lineWidth: isSelected ? 2 : 1,
          lineDash: z.ghost ? [3, 2] : null,
        },
      },
    ];
    // How it left: a glyph at the end of the bar.
    if (z.ended && x1 >= box.x && x1 <= box.x + box.width) {
      const glyph = endGlyph(z.outcome);
      const r = Math.max(2.5, Math.min(4, h / 2));
      if (glyph === "dot") {
        children.push({ type: "circle", shape: { cx: x1, cy: yc, r }, style: { fill: INK.primary, stroke: "#ffffff", lineWidth: 1 } });
      } else if (glyph === "diamond") {
        const s = r + 1;
        children.push({ type: "polygon", shape: { points: [[x1, yc - s], [x1 + s, yc], [x1, yc + s], [x1 - s, yc]] }, style: { fill: INK.primary, stroke: "#ffffff", lineWidth: 1 } });
      } else if (glyph === "cross") {
        const s = r + 0.5;
        children.push({ type: "line", shape: { x1: x1 - s, y1: yc - s, x2: x1 + s, y2: yc + s }, style: { stroke: STATUS.critical, lineWidth: 2 } });
        children.push({ type: "line", shape: { x1: x1 - s, y1: yc + s, x2: x1 + s, y2: yc - s }, style: { stroke: STATUS.critical, lineWidth: 2 } });
      } else {
        children.push({ type: "circle", shape: { cx: x1, cy: yc, r: r - 0.5 }, style: { fill: "#ffffff", stroke: INK.secondary, lineWidth: 1.2 } });
      }
    }
    // The word inside the bar when it fits.
    const label = z.words.length > 1 ? z.words.join("→") : z.word;
    if (label && h >= 9 && span[1] - span[0] - 12 > textWidth(label, 10)) {
      children.push(textEl(label, span[0] + 4, yc, z.wasTargeted && !z.ghost ? "#ffffff" : INK.primary, 10));
    }
    return { type: "group", children } as CustomSeriesRenderItemReturn;
  };
}

function makeWordRenderer(words: WordBar[], color: string, selectedZombie: string | null) {
  return (params: CustomSeriesRenderItemParams, api: CustomSeriesRenderItemAPI): CustomSeriesRenderItemReturn => {
    const w = words[api.value(3) as number];
    const box = coordBox(params);
    const [x0, yc] = api.coord([w.start, 0]);
    const [x1] = api.coord([w.end, 0]);
    const span = clipX(x0, Math.max(x1, x0 + 2), box);
    if (!span) return null;
    const h = Math.max(6, Math.min(18, bandHeight(api) * 0.62));
    const linked = selectedZombie !== null && w.zombieKey === selectedZombie;
    const children: object[] = [
      {
        type: "rect",
        shape: { x: span[0], y: yc - h / 2, width: span[1] - span[0], height: h, r: Math.min(3, h / 2) },
        style: { fill: color, stroke: linked ? SELECTION_INK : null, lineWidth: 2 },
      },
    ];
    // One white tick per logged keystroke, when there is room for them.
    if (w.keyTimes && w.keyTimes.length > 1 && (x1 - x0) / w.keyTimes.length >= 3) {
      for (const t of w.keyTimes) {
        const [x] = api.coord([t, 0]);
        if (x >= span[0] && x <= span[1]) {
          children.push({ type: "line", silent: true, shape: { x1: x, y1: yc - h / 2 + 2, x2: x, y2: yc + h / 2 - 2 }, style: { stroke: "rgba(255,255,255,0.9)", lineWidth: 1 } });
        }
      }
    }
    if (w.word && span[1] - span[0] - 10 > textWidth(w.word, 10)) {
      children.push(textEl(w.word, span[0] + 4, yc, "#ffffff", 10));
    }
    return { type: "group", children } as CustomSeriesRenderItemReturn;
  };
}

// The selected time: a vertical line through the lane (data = [[t, 0]]).
function renderSelection(params: CustomSeriesRenderItemParams, api: CustomSeriesRenderItemAPI): CustomSeriesRenderItemReturn {
  const box = coordBox(params);
  const [x] = api.coord([api.value(0) as number, 0]);
  if (x < box.x || x > box.x + box.width) return null;
  return {
    type: "line",
    silent: true,
    shape: { x1: x, y1: box.y - 2, x2: x, y2: box.y + box.height + 2 },
    style: { stroke: SELECTION_INK, lineWidth: 1.5 },
  } as CustomSeriesRenderItemReturn;
}

export function selectionSeriesPatch(t: number | null): { series: { id: string; data: number[][] }[] } {
  return { series: LANES.map((lane) => ({ id: "sel-" + lane, data: t === null ? [] : [[t, 0]] })) };
}

// ---------------------------------------------------------------------------
// The option
// ---------------------------------------------------------------------------

const AXIS_LABEL = { ...AXIS_STYLE.axisLabel };
const SPLIT_LINE = { show: true, lineStyle: { color: INK.grid, width: 1, type: "solid" as const } };

// A step line (value holds until the next change) whose invisible points
// still show a tooltip on hover.
function stepLine(id: string, name: string, lane: LaneId, points: SeriesPoint[], area: boolean): LineSeriesOption {
  return {
    id,
    type: "line",
    name,
    xAxisIndex: GRID[lane],
    yAxisIndex: GRID[lane],
    data: points.map((p) => [p.t, p.v]),
    step: "end",
    showSymbol: true,
    showAllSymbol: true,
    symbol: "circle",
    symbolSize: 7,
    itemStyle: { color: LINE_INK, opacity: 0 },
    emphasis: { itemStyle: { opacity: 1 }, scale: 1.2 },
    lineStyle: { color: LINE_INK, width: 2 },
    areaStyle: area ? { color: "rgba(11,11,11,0.05)" } : undefined,
    animation: false,
    z: 2,
  };
}

export function buildTimelineOption({ timeline: tl, rowMode, selectedZombie, legendSelected }: TimelineOptionInput): TimelineModel {
  const duration = Math.ceil(tl.duration);
  const zombieByKey = new Map(tl.zombies.map((z) => [z.key, z]));
  const { rows, count: rowCount } = zombieRows(tl.zombies, rowMode);

  const categories = MARKER_CATEGORIES.filter((c) => tl.markers.some((m) => m.category === c.id));
  const categoryRow = new Map<MarkerCategory, number>(categories.map((c, i) => [c.id, i]));

  // ---- layout ----
  const rowPx = rowMode === "each" ? Math.max(5, Math.min(14, 380 / Math.max(1, rowCount))) : Math.max(8, Math.min(16, 400 / Math.max(1, rowCount)));
  const heights: Record<LaneId, number> = {
    phases: 22,
    health: 84,
    combo: 52,
    alive: 52,
    zombies: Math.max(48, Math.round(rowCount * rowPx)),
    typing: 56,
    events: Math.max(3, categories.length) * 17,
    wpm: 84,
  };
  const tops = {} as Record<LaneId, number>;
  let y = 0;
  for (const lane of LANES) {
    // The first lane has its title row, then the top time axis labels.
    y += lane === "phases" ? 50 : TITLE_GAP;
    tops[lane] = y;
    y += heights[lane];
  }
  const sliderTop = y + 34;
  const height = sliderTop + 34;

  const grid: GridComponentOption[] = LANES.map((lane) => ({ left: LEFT, right: RIGHT, top: tops[lane], height: heights[lane] }));
  const last = LANES.length - 1;
  const xAxis: XAXisComponentOption[] = LANES.map((lane, g) => ({
    type: "value",
    gridIndex: g,
    min: 0,
    max: duration,
    position: g === 0 ? "top" : "bottom",
    axisLine: { show: g === last, lineStyle: { color: INK.axis, width: 1 } },
    axisTick: { show: false },
    axisLabel: { ...AXIS_LABEL, show: g === 0 || g === last, formatter: (value: number) => axisTime(value) },
    splitLine: SPLIT_LINE,
    axisPointer: {
      show: true,
      type: "line",
      snap: false,
      triggerTooltip: false,
      lineStyle: { color: INK.muted, width: 1, type: "dashed" },
      label: {
        show: g === 0 || g === last,
        formatter: (p: { value: unknown }) => fmtT(Number(p.value)),
        backgroundColor: INK.primary,
        color: "#ffffff",
        fontSize: 11,
        fontFamily: FONT,
        padding: [3, 6],
      },
    },
  }));

  const valueAxis = (lane: LaneId, extra: YAXisComponentOption): YAXisComponentOption =>
    ({
      type: "value",
      gridIndex: GRID[lane],
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: AXIS_LABEL,
      splitLine: SPLIT_LINE,
      ...extra,
    }) as YAXisComponentOption;

  const yAxis: YAXisComponentOption[] = [
    valueAxis("phases", { min: 0, max: 1, show: false }),
    valueAxis("health", { min: 0, max: 100, interval: 50 }),
    valueAxis("combo", { min: 0, minInterval: 1, splitNumber: 2 }),
    valueAxis("alive", { min: 0, minInterval: 1, splitNumber: 2 }),
    valueAxis("zombies", { min: -0.5, max: Math.max(0.5, rowCount - 0.5), inverse: true, show: false }),
    {
      type: "category",
      gridIndex: GRID.typing,
      data: ["Words", "Wrong keys"],
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: AXIS_LABEL,
      splitLine: { show: false },
    },
    {
      type: "category",
      gridIndex: GRID.events,
      data: categories.map((c) => c.label),
      inverse: true,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: AXIS_LABEL,
      splitLine: { show: true, interval: 0, lineStyle: { color: INK.grid, width: 1 } },
    },
    valueAxis("wpm", { min: 0, splitNumber: 2 }),
  ];

  // Tooltip text per series id (dataIndex -> HTML).
  const tips: Record<string, (dataIndex: number) => string> = {};
  const series: (CustomSeriesOption | LineSeriesOption | ScatterSeriesOption)[] = [];

  // ---- (a) phases + pauses ----
  series.push({
    id: "phases",
    type: "custom",
    name: "Phases",
    xAxisIndex: GRID.phases,
    yAxisIndex: GRID.phases,
    data: tl.phases.map((p, i) => [p.start, p.end, i]),
    encode: { x: [0, 1], y: 2 },
    renderItem: makePhaseRenderer(tl.phases),
    clip: true,
    z: 1,
  });
  tips.phases = (i) => phaseTip(tl.phases[i]);
  if (tl.away.length > 0) {
    series.push({
      id: "away",
      type: "scatter",
      name: "Pauses",
      xAxisIndex: GRID.phases,
      yAxisIndex: GRID.phases,
      data: tl.away.map((a) => ({ value: [a.t, 0.5], itemStyle: { color: a.kind === "pause" ? INK.primary : INK.muted } })),
      symbol: "rect",
      symbolSize: [3, 26],
      z: 4,
    });
    tips.away = (i) => {
      const a = tl.away[i];
      return a.kind === "pause"
        ? tipHtml("Paused (" + (a.reason ?? "?") + ")", [["At", fmtT(a.t)], ["Paused for", a.seconds !== null ? seconds(a.seconds) + " real time" : "no resume logged (restarted, or tab closed)"]], INK.primary, "Play time stops while paused, so a pause is an instant here.")
        : tipHtml("Tab hidden", [["At", fmtT(a.t)], ["Away for", seconds(a.seconds)]], INK.muted);
    };
  }

  // ---- (b) health ----
  series.push(stepLine("health", "Health", "health", tl.health, true));
  tips.health = (i) => pointTip("Health", tl.health[i]);
  const damage = tl.events.filter((e) => e.event === "damage");
  const hits = damage.filter((e) => !dataBool(e, "fatal"));
  const fatal = damage.filter((e) => dataBool(e, "fatal"));
  const heals = tl.events.filter((e) => e.event === "crate" && dataStr(e, "kind") === "health");
  const damageTip = (e: (typeof damage)[number]) =>
    tipHtml(
      (dataBool(e, "fatal") ? "Fatal hit" : "Hit") + " by " + (dataStr(e, "source") ?? "?").replace("zombie_", ""),
      [
        ["At", fmtT(tOf(e))],
        ["Health", (dataNum(e, "health") ?? "?") + " → " + (dataNum(e, "health_after") ?? "?")],
        ["Combo", (dataNum(e, "combo") ?? "?") + " → " + (dataNum(e, "combo_after") ?? "?")],
        ["Attacker", dataNum(e, "attacker_id") !== null ? "#" + dataNum(e, "attacker_id") : null],
        ["Its word", dataStr(e, "attacker_word")],
        ["Letters typed", dataNum(e, "attacker_letters_typed")],
        ["It lived", seconds(dataNum(e, "attacker_seconds_alive"))],
        ["Ambusher", dataBool(e, "ambusher") && "yes"],
        ["Zombies alive", dataNum(e, "zombies_alive")],
      ],
      STATUS.critical,
    );
  if (hits.length > 0) {
    series.push({
      id: "hits",
      type: "scatter",
      name: "Hit",
      xAxisIndex: GRID.health,
      yAxisIndex: GRID.health,
      data: hits.map((e) => [tOf(e), dataNum(e, "health_after") ?? 0]),
      symbol: CROSS_SYMBOL,
      symbolSize: 10,
      itemStyle: { color: STATUS.critical },
      z: 5,
    });
    tips.hits = (i) => damageTip(hits[i]);
  }
  if (fatal.length > 0) {
    series.push({
      id: "fatal",
      type: "scatter",
      name: "Fatal hit",
      xAxisIndex: GRID.health,
      yAxisIndex: GRID.health,
      data: fatal.map((e) => [tOf(e), dataNum(e, "health_after") ?? 0]),
      symbol: CROSS_SYMBOL,
      symbolSize: 16,
      itemStyle: { color: STATUS.critical },
      label: { show: true, position: "left", formatter: "Fatal", color: INK.primary, fontSize: 11, fontWeight: 600, fontFamily: FONT },
      z: 6,
    });
    tips.fatal = (i) => damageTip(fatal[i]);
  }
  if (heals.length > 0) {
    series.push({
      id: "heals",
      type: "scatter",
      name: "Health crate",
      xAxisIndex: GRID.health,
      yAxisIndex: GRID.health,
      data: heals.map((e) => [tOf(e), dataNum(e, "health") ?? 0]),
      symbol: "triangle",
      symbolSize: 10,
      itemStyle: { color: STATUS.good },
      z: 5,
    });
    tips.heals = (i) => tipHtml("Health crate " + (dataStr(heals[i], "word") ?? ""), [["At", fmtT(tOf(heals[i]))], ["Health", (dataNum(heals[i], "health_before") ?? "?") + " → " + (dataNum(heals[i], "health") ?? "?")]], STATUS.good);
  }

  // ---- (c) combo, zombies alive ----
  series.push(stepLine("combo", "Combo", "combo", tl.combo, false));
  tips.combo = (i) => pointTip("Combo", tl.combo[i]);
  series.push(stepLine("alive", "Zombies alive", "alive", tl.alive, true));
  tips.alive = (i) => pointTip("Zombies alive", tl.alive[i]);

  // ---- (d) zombies (Gantt), one series per kind (legend = kinds) ----
  const kinds = [...ZOMBIE_KINDS.filter((k) => tl.zombies.some((z) => z.kind === k)), ...new Set(tl.zombies.map((z) => z.kind).filter((k) => !(ZOMBIE_KINDS as readonly string[]).includes(k)))];
  const zombieRenderer = makeZombieRenderer(tl.zombies, rows, selectedZombie);
  for (const kind of kinds) {
    const indices = tl.zombies.map((z, i) => (z.kind === kind ? i : -1)).filter((i) => i >= 0);
    const id = "zombies-" + kind;
    series.push({
      id,
      type: "custom",
      name: kindLabel(kind),
      xAxisIndex: GRID.zombies,
      yAxisIndex: GRID.zombies,
      data: indices.map((i) => [tl.zombies[i].spawnT, tl.zombies[i].endT, rows[i], i]),
      encode: { x: [0, 1], y: 2 },
      renderItem: zombieRenderer,
      itemStyle: { color: kindColor(kind) },
      clip: true,
      progressive: 1000,
      progressiveThreshold: 3000,
      z: 3,
    });
    tips[id] = (i) => zombieTip(tl.zombies[indices[i]]);
  }

  // ---- (e) typing: attempts by outcome + wrong keys ----
  const typingLegend: string[] = [];
  for (const outcome of WORD_OUTCOMES) {
    const indices = tl.words.map((w, i) => (wordOutcomeGroup(w.outcome) === outcome.id ? i : -1)).filter((i) => i >= 0);
    if (indices.length === 0) continue;
    const id = "words-" + outcome.id;
    typingLegend.push(outcome.label);
    series.push({
      id,
      type: "custom",
      name: outcome.label,
      xAxisIndex: GRID.typing,
      yAxisIndex: GRID.typing,
      data: indices.map((i) => [tl.words[i].start, tl.words[i].end, 0, i]),
      encode: { x: [0, 1], y: 2 },
      renderItem: makeWordRenderer(tl.words, outcome.color, selectedZombie),
      itemStyle: { color: outcome.color },
      clip: true,
      progressive: 1000,
      progressiveThreshold: 3000,
      z: 3,
    });
    tips[id] = (i) => wordTip(tl.words[indices[i]], zombieByKey);
  }
  const wrongGroups = [
    { key: "wrong", marks: tl.wrongKeys.filter((k) => !k.stray && !k.caseOnly) },
    { key: "caseOnly", marks: tl.wrongKeys.filter((k) => !k.stray && k.caseOnly) },
    { key: "stray", marks: tl.wrongKeys.filter((k) => k.stray) },
  ] as const;
  for (const group of wrongGroups) {
    if (group.marks.length === 0) continue;
    const style = WRONG_KEY_STYLE[group.key];
    const id = "keys-" + group.key;
    typingLegend.push(style.label);
    series.push({
      id,
      type: "scatter",
      name: style.label,
      xAxisIndex: GRID.typing,
      yAxisIndex: GRID.typing,
      data: group.marks.map((k) => [k.t, 1]),
      symbol: "rect",
      symbolSize: [2, 14],
      itemStyle: { color: style.color },
      z: 4,
    });
    tips[id] = (i) => {
      const k = group.marks[i];
      return tipHtml(
        style.label + ": typed “" + k.typed + "”" + (k.expected ? ", needed “" + k.expected + "”" : ""),
        [
          ["At", fmtT(k.t)],
          ["Target", k.target ? k.target + (k.targetId !== null ? " #" + k.targetId : "") : null],
          ["Letter", k.letterIndex !== null && k.wordLength ? k.letterIndex + 1 + " of " + k.wordLength : null],
        ],
        style.color,
        k.stray ? "No word was being typed and the key started none." : k.caseOnly ? "Right letter, wrong case (case-sensitive word)." : undefined,
      );
    };
  }

  // ---- (f) events lane: one scatter per category ----
  for (const category of categories) {
    const style = MARKER_STYLE[category.id];
    const markers = tl.markers.filter((m) => m.category === category.id);
    const id = "markers-" + category.id;
    series.push({
      id,
      type: "scatter",
      name: style.label,
      xAxisIndex: GRID.events,
      yAxisIndex: GRID.events,
      data: markers.map((m) => ({
        value: [m.t, categoryRow.get(category.id) ?? 0],
        symbolSize: m.important ? 14 : category.id === "blast" ? 8 + Math.min(m.weight, 6) * 1.5 : 9,
      })),
      symbol: style.symbol,
      itemStyle: { color: style.color, borderColor: "#ffffff", borderWidth: 1 },
      z: 4,
    });
    tips[id] = (i) => {
      const m = markers[i];
      return tipHtml(m.title, [["At", fmtT(m.t)], ["Event", m.event.event], ["", m.detail]], style.color);
    };
  }

  // ---- (g) typing speed ----
  const wpmWords: LineSeriesOption = {
    ...stepLine("wpm-words", "From word attempts", "wpm", tl.wpm.fromWords, false),
    step: false,
    markLine:
      tl.wpm.runAverage !== null
        ? {
            silent: true,
            symbol: "none",
            lineStyle: { color: INK.muted, type: "dashed", width: 1 },
            label: { position: "insideEndTop", formatter: "run average " + Math.round(tl.wpm.runAverage) + " WPM", color: INK.secondary, fontSize: 11, fontFamily: FONT },
            data: [{ yAxis: tl.wpm.runAverage }],
          }
        : undefined,
  };
  series.push(wpmWords);
  tips["wpm-words"] = (i) => pointTip("Typing speed", tl.wpm.fromWords[i], " WPM");
  if (tl.wpm.fromSamples.length > 0) {
    series.push({
      ...stepLine("wpm-samples", "From heartbeat keys", "wpm", tl.wpm.fromSamples, false),
      step: false,
      lineStyle: { color: INK.muted, width: 2, type: "dashed" },
      itemStyle: { color: INK.muted, opacity: 0 },
    });
    tips["wpm-samples"] = (i) => pointTip("Typing speed", tl.wpm.fromSamples[i], " WPM");
  }

  // ---- selected time: one vertical line per lane (updated by merge) ----
  for (const lane of LANES) {
    series.push({
      id: "sel-" + lane,
      type: "custom",
      xAxisIndex: GRID[lane],
      yAxisIndex: GRID[lane],
      data: [],
      renderItem: renderSelection,
      silent: true,
      tooltip: { show: false },
      z: 20,
    });
  }

  // ---- titles and legends ----
  const rich = {
    t: { fontSize: 12, fontWeight: 600 as const, color: INK.primary, fontFamily: FONT },
    s: { fontSize: 11, color: INK.muted, fontFamily: FONT },
    ride: { width: 10, height: 10, borderRadius: 2, backgroundColor: PHASE_STYLE.ride.fill },
    fight: { width: 10, height: 10, borderRadius: 2, backgroundColor: PHASE_STYLE.fight.fill },
    boss: { width: 10, height: 10, borderRadius: 2, backgroundColor: PHASE_STYLE.boss.fill },
    pause: { width: 3, height: 12, backgroundColor: INK.primary },
  };
  const title = (lane: LaneId, text: string, sub: string, top = tops[lane] - 24): TitleComponentOption => ({
    text: "{t|" + text + "}" + (sub ? "  {s|" + sub + "}" : ""),
    left: 16,
    top,
    textStyle: { rich },
  });
  const titles: TitleComponentOption[] = [
    {
      text: "{t|Phases}   {ride| } {s|ride}   {fight| } {s|fight}   {boss| } {s|boss}   {pause| } {s|pause / tab hidden}",
      left: 16,
      top: 4,
      textStyle: { rich },
    },
    title("health", "Health", "0–100 · hits marked at the health left"),
    title("combo", "Combo", "kills in a row without a mistake"),
    title("alive", "Zombies on the field", tl.aliveSource === "samples" ? "from the 1 s heartbeat" : "counted from the zombie bars below"),
    title("zombies", "Zombies", "appeared → gone, " + (rowMode === "each" ? "one row each" : "packed rows")),
    title("typing", "Typing", "attempts, first key → end"),
    title("events", "Events", "hover for details"),
    title("wpm", "Typing speed (WPM)", "correct keys ÷ 5 per minute, rolling " + tl.wpm.windowSeconds + " s"),
  ];

  const legendBase: LegendComponentOption = {
    ...LEGEND_STYLE,
    type: "scroll",
    right: RIGHT,
    width: "56%",
    itemGap: 12,
    textStyle: { ...LEGEND_STYLE.textStyle, fontSize: 11 },
    pageIconSize: 9,
    pageTextStyle: { color: INK.muted, fontSize: 10 },
    selected: legendSelected,
  };
  const legends: LegendComponentOption[] = [];
  const healthNames = [hits.length > 0 && "Hit", fatal.length > 0 && "Fatal hit", heals.length > 0 && "Health crate"].filter((n): n is string => !!n);
  if (healthNames.length > 0) legends.push({ ...legendBase, top: tops.health - 26, data: healthNames, icon: undefined });
  if (kinds.length > 0) legends.push({ ...legendBase, top: tops.zombies - 26, data: kinds.map(kindLabel) });
  if (typingLegend.length > 0) legends.push({ ...legendBase, top: tops.typing - 26, data: typingLegend });
  if (categories.length > 0) legends.push({ ...legendBase, top: tops.events - 26, data: categories.map((c) => MARKER_STYLE[c.id].label), icon: undefined });
  if (tl.wpm.fromSamples.length > 0) legends.push({ ...legendBase, top: tops.wpm - 26, data: ["From word attempts", "From heartbeat keys"], icon: undefined });

  const tooltip: TooltipComponentOption = {
    ...TOOLTIP_STYLE,
    trigger: "item",
    confine: true,
    enterable: false,
    transitionDuration: 0,
    formatter: (params) => {
      const p = (Array.isArray(params) ? params[0] : params) as { seriesId?: string; dataIndex: number };
      const make = p.seriesId ? tips[p.seriesId] : undefined;
      return make ? make(p.dataIndex) : "";
    },
  };

  const allGrids = LANES.map((_, i) => i);
  const dataZoom: DataZoomComponentOption[] = [
    {
      type: "slider",
      xAxisIndex: allGrids,
      filterMode: "none",
      top: sliderTop,
      height: 22,
      left: LEFT,
      right: RIGHT,
      showDataShadow: false,
      borderColor: INK.border,
      fillerColor: "rgba(42,120,214,0.10)",
      handleStyle: { color: "#ffffff", borderColor: INK.axis },
      moveHandleStyle: { color: INK.axis },
      textStyle: { color: INK.muted, fontSize: 11, fontFamily: FONT },
      labelFormatter: (value: number) => fmtT(value),
    },
    // Drag to pan; Ctrl + wheel to zoom (a plain wheel still scrolls the page).
    { type: "inside", xAxisIndex: allGrids, filterMode: "none", zoomOnMouseWheel: "ctrl", moveOnMouseWheel: false, moveOnMouseMove: true },
  ];

  const option: EChartsOption = {
    animation: false,
    textStyle: { fontFamily: FONT },
    title: titles,
    legend: legends,
    tooltip,
    axisPointer: { link: [{ xAxisIndex: "all" }] },
    grid,
    xAxis,
    yAxis,
    dataZoom,
    series,
  };
  return { option, height, gridCount: LANES.length };
}
