// ECharts option builders shared by the Insights and Overview pages, so
// every chart has the same marks (thin bars with a rounded data end,
// hairline grid, one axis), tooltips and spacing. Safe to import anywhere.
import type {
  CustomSeriesOption,
  CustomSeriesRenderItem,
  EChartsOption,
  TooltipComponentFormatterCallbackParams,
} from "echarts";
import { AXIS_STYLE, BLUE_RAMP, INK, LEGEND_STYLE, SERIES, TOOLTIP_STYLE } from "@/lib/theme";

export type Fmt = (value: number) => string;

export const fmtPctAxis: Fmt = (value) => Math.round(value * 100) + "%";

// ---- Tooltips ----

export function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c] as string);
}

function swatch(color: string): string {
  return `<span style="display:inline-block;width:8px;height:8px;border-radius:2px;margin-right:6px;background:${color}"></span>`;
}

export type TipRow = readonly [label: string, value: string, color?: string];

// A tooltip: a bold title, then label / value rows (text in ink colors; a
// small swatch carries a series' identity).
export function tipHtml(title: string, rows: TipRow[]): string {
  let html = `<div style="font-weight:600;margin-bottom:4px">${escapeHtml(title)}</div>`;
  for (const [label, value, color] of rows) {
    html +=
      `<div style="display:flex;justify-content:space-between;gap:18px;line-height:1.6">` +
      `<span style="color:${INK.secondary}">${color ? swatch(color) : ""}${escapeHtml(label)}</span>` +
      `<span style="font-variant-numeric:tabular-nums;color:${INK.primary}">${escapeHtml(value)}</span></div>`;
  }
  return html;
}

// The data index under the pointer (axis tooltips pass an array).
export function hoveredIndex(params: TooltipComponentFormatterCallbackParams): number {
  const first = Array.isArray(params) ? params[0] : params;
  return first?.dataIndex ?? 0;
}

const axisTooltip = {
  ...TOOLTIP_STYLE,
  trigger: "axis" as const,
  axisPointer: { type: "shadow" as const, shadowStyle: { color: "rgba(11,11,11,0.04)" } },
  confine: true,
};

export const itemTooltip = { ...TOOLTIP_STYLE, trigger: "item" as const, confine: true };

// ---- Axes ----

function valueAxis(name: string | undefined, format: Fmt | undefined, max?: number, horizontal = false) {
  return {
    type: "value" as const,
    name,
    nameLocation: "middle" as const,
    nameGap: horizontal ? 26 : 44,
    max,
    minInterval: format ? undefined : 1,
    ...AXIS_STYLE,
    axisLine: { show: false },
    axisLabel: { ...AXIS_STYLE.axisLabel, formatter: format },
  };
}

function categoryAxis(categories: string[], name: string | undefined, horizontal: boolean, labelInterval = 0) {
  return {
    type: "category" as const,
    data: categories,
    name,
    nameLocation: "middle" as const,
    nameGap: horizontal ? 0 : 28,
    inverse: horizontal, // first category at the top
    ...AXIS_STYLE,
    splitLine: { show: false },
    axisLabel: { ...AXIS_STYLE.axisLabel, color: INK.secondary, interval: labelInterval, hideOverlap: true },
  };
}

// Room around the plot; axis labels are kept inside the canvas by ECharts 6.
function grid(horizontal: boolean, hasLegend = false, hasValueName = false) {
  return {
    left: horizontal ? 8 : hasValueName ? 28 : 8,
    right: horizontal ? 48 : 16,
    top: hasLegend ? 40 : 16,
    bottom: horizontal && hasValueName ? 34 : 8,
  };
}

// ---- Colors ----

// n ordinal steps of the blue ramp (light -> dark), e.g. funnel steps.
export function rampColors(n: number): string[] {
  if (n <= 1) {
    return [BLUE_RAMP[7]];
  }
  return Array.from({ length: n }, (_, i) => BLUE_RAMP[Math.round(3 + (i * 9) / (n - 1))]);
}

// ---- Bars (one series) ----

export type BarSpec = {
  categories: string[];
  values: number[];
  colors?: string | string[]; // one color, or one per bar
  horizontal?: boolean;
  valueName?: string; // value axis title
  categoryName?: string;
  format?: Fmt; // value axis labels and bar labels
  max?: number;
  tooltip: (index: number) => string; // HTML (use tipHtml)
  barLabel?: (index: number) => string; // a label at the end of each bar
  intervals?: { low: number; high: number }[]; // 95% whiskers per bar
  labelInterval?: number; // category labels: 0 = every one, 9 = every 10th
};

export function barOption(spec: BarSpec): EChartsOption {
  const horizontal = !!spec.horizontal;
  const colorOf = (i: number) => (Array.isArray(spec.colors) ? spec.colors[i] : spec.colors) ?? SERIES[0];
  const values = valueAxis(spec.valueName, spec.format, spec.max, horizontal);
  const categories = categoryAxis(spec.categories, spec.categoryName, horizontal, spec.labelInterval);
  const label = spec.barLabel;
  const series: EChartsOption["series"] = [
    {
      type: "bar",
      barMaxWidth: 24,
      itemStyle: { borderRadius: horizontal ? [0, 4, 4, 0] : [4, 4, 0, 0] },
      data: spec.values.map((value, i) => ({ value, itemStyle: { color: colorOf(i) } })),
      label: label
        ? {
            show: true,
            position: horizontal ? "right" : "top",
            color: INK.secondary,
            fontSize: 11,
            formatter: (p: { dataIndex: number }) => label(p.dataIndex),
          }
        : undefined,
    },
  ];
  if (spec.intervals) {
    series.push(intervalSeries(spec.intervals, horizontal));
  }
  return {
    animationDuration: 300,
    grid: grid(horizontal, false, !!spec.valueName),
    xAxis: horizontal ? values : categories,
    yAxis: horizontal ? categories : values,
    tooltip: { ...axisTooltip, formatter: (params) => spec.tooltip(hoveredIndex(params)) },
    series,
  };
}

// Whiskers (e.g. a 95% confidence interval) drawn over bars.
function intervalSeries(intervals: { low: number; high: number }[], horizontal: boolean): CustomSeriesOption {
  const renderItem: CustomSeriesRenderItem = (_params, api) => {
    const index = Number(api.value(0));
    const low = api.coord(horizontal ? [api.value(1), index] : [index, api.value(1)]);
    const high = api.coord(horizontal ? [api.value(2), index] : [index, api.value(2)]);
    const cap = 5;
    const style = { stroke: INK.primary, lineWidth: 1.5 };
    const capAt = (point: number[]) =>
      horizontal
        ? { type: "line" as const, shape: { x1: point[0], y1: point[1] - cap, x2: point[0], y2: point[1] + cap }, style }
        : { type: "line" as const, shape: { x1: point[0] - cap, y1: point[1], x2: point[0] + cap, y2: point[1] }, style };
    return {
      type: "group",
      children: [
        { type: "line", shape: { x1: low[0], y1: low[1], x2: high[0], y2: high[1] }, style },
        capAt(low),
        capAt(high),
      ],
    };
  };
  return {
    type: "custom",
    name: "95% interval",
    renderItem,
    data: intervals.map((interval, i) => [i, interval.low, interval.high]),
    z: 5,
    silent: true,
  };
}

// ---- Bars (several series, stacked or side by side) ----

export type BarSeriesSpec = { name: string; color: string; values: number[] };

export type MultiBarSpec = {
  categories: string[];
  series: BarSeriesSpec[];
  stacked?: boolean;
  horizontal?: boolean;
  valueName?: string;
  format?: Fmt;
  tooltip?: (index: number) => string; // default: every series' value and the total
};

export function multiBarOption(spec: MultiBarSpec): EChartsOption {
  const horizontal = !!spec.horizontal;
  const stacked = !!spec.stacked;
  const radius = horizontal ? [0, 4, 4, 0] : [4, 4, 0, 0];
  // Only the outermost segment of a stack gets the rounded end.
  const topSeries = spec.categories.map((_, i) => {
    let top = -1;
    spec.series.forEach((s, si) => {
      if ((s.values[i] ?? 0) > 0) top = si;
    });
    return top;
  });
  const format = spec.format ?? ((v: number) => String(v));
  const defaultTooltip = (i: number) =>
    tipHtml(spec.categories[i], [
      ...spec.series.filter((s) => (s.values[i] ?? 0) !== 0 || !stacked).map((s) => [s.name, format(s.values[i] ?? 0), s.color] as const),
      ...(stacked ? [["Total", format(spec.series.reduce((sum, s) => sum + (s.values[i] ?? 0), 0))] as const] : []),
    ]);
  return {
    animationDuration: 300,
    legend: { ...LEGEND_STYLE, top: 0, left: 0, data: spec.series.map((s) => s.name) },
    grid: grid(horizontal, true, !!spec.valueName),
    xAxis: horizontal ? valueAxis(spec.valueName, spec.format, undefined, true) : categoryAxis(spec.categories, undefined, false),
    yAxis: horizontal ? categoryAxis(spec.categories, undefined, true) : valueAxis(spec.valueName, spec.format),
    tooltip: { ...axisTooltip, formatter: (params) => (spec.tooltip ?? defaultTooltip)(hoveredIndex(params)) },
    series: spec.series.map((s, si) => ({
      type: "bar" as const,
      name: s.name,
      stack: stacked ? "total" : undefined,
      barMaxWidth: 24,
      barGap: "10%",
      itemStyle: { color: s.color, borderColor: stacked ? INK.surface : undefined, borderWidth: stacked ? 1 : 0 },
      data: s.values.map((value, i) => ({
        value,
        itemStyle: { borderRadius: !stacked || topSeries[i] === si ? radius : 0 },
      })),
    })),
  };
}

// ---- Histograms ----

// Counts per bin as bars (the bins are the categories).
export function histogramOption(
  labels: string[],
  counts: number[],
  opts: { color?: string | string[]; valueName?: string; categoryName?: string; unit: string; noun: string },
): EChartsOption {
  const total = counts.reduce((sum, c) => sum + c, 0);
  return barOption({
    categories: labels,
    values: counts,
    colors: opts.color ?? SERIES[0],
    valueName: opts.valueName ?? opts.noun,
    categoryName: opts.categoryName,
    tooltip: (i) =>
      tipHtml(labels[i] + (opts.unit ? " " + opts.unit : ""), [
        [opts.noun, String(counts[i])],
        ["Share", total > 0 ? Math.round((counts[i] / total) * 100) + "%" : "-"],
      ]),
  });
}

// ---- Value axes used as a list of slots (dot strips, jittered scatters) ----

// A value axis from -0.5 to count - 0.5 with one label per whole number
// (slot i is drawn at value i, so dots can be nudged around it).
export function slotAxis(count: number, labelFor: (slot: number) => string) {
  const slots = Array.from({ length: Math.max(1, count) }, (_, i) => i);
  return {
    type: "value" as const,
    min: -0.5,
    max: Math.max(1, count) - 0.5,
    ...AXIS_STYLE,
    axisLine: { ...AXIS_STYLE.axisLine, onZero: false }, // at the edge, not across the plot at 0
    axisTick: { show: false, customValues: slots },
    axisLabel: { ...AXIS_STYLE.axisLabel, color: INK.secondary, hideOverlap: true, customValues: slots, formatter: (v: number) => labelFor(v) },
  };
}
