// Colors and chart styling, in one place (safe to import anywhere).
// The dashboard is light-themed only: the zombie-kind colors were validated
// (all pairs, colorblind-safe) for the light chart surface; their dark-mode
// steps did not pass, so there is no dark theme.

export const INK = {
  surface: "#fcfcfb", // chart / card surface
  page: "#f9f9f7", // page background
  primary: "#0b0b0b",
  secondary: "#52514e",
  muted: "#898781", // axis labels, hints
  grid: "#e1e0d9", // hairline gridlines
  axis: "#c3c2b7", // baselines
  border: "rgba(11,11,11,0.10)",
};

// Categorical slots, always used in this order (never cycled past 8).
export const SERIES = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"];

// Sequential blue ramp (magnitude), light -> dark. Ordinal use: start at index 3 (step 250).
export const BLUE_RAMP = ["#cde2fb", "#b7d3f6", "#9ec5f4", "#86b6ef", "#6da7ec", "#5598e7", "#3987e5", "#2a78d6", "#256abf", "#1c5cab", "#184f95", "#104281", "#0d366b"];

// Status colors: only when the color MEANS good / bad (always with a label).
export const STATUS = {
  good: "#0ca30c",
  warning: "#fab219",
  serious: "#ec835a",
  critical: "#d03b3b",
};

// One color per zombie kind, the same everywhere (color follows the entity).
// Normal zombies are neutral gray. Validated together (all pairs) on light.
export const KIND_COLORS: Record<string, string> = {
  normal: "#898781",
  armored: "#2a78d6",
  explosive: "#eb6834", // "red zombies" (their blast ring is orange in the game)
  runner: "#1baf7a",
  shifter: "#4a3aa7",
};

export const KIND_LABELS: Record<string, string> = {
  normal: "Normal",
  runner: "Runner",
  armored: "Armored",
  explosive: "Red (explosive)",
  shifter: "Shifter",
};

// Run outcomes carry status meaning.
export const OUTCOME_COLORS: Record<string, string> = {
  won: STATUS.good,
  lost: STATUS.critical,
  restart: STATUS.warning,
  abandoned: "#898781", // tab closed / no run_end
  playing: "#2a78d6",
};

export const FONT = 'var(--font-geist-sans), system-ui, -apple-system, "Segoe UI", sans-serif';

// Base ECharts option pieces (merge into a chart's own option).
export const AXIS_STYLE = {
  axisLine: { lineStyle: { color: INK.axis, width: 1 } },
  axisTick: { show: false },
  axisLabel: { color: INK.muted, fontSize: 11, fontFamily: FONT },
  splitLine: { lineStyle: { color: INK.grid, width: 1, type: "solid" as const } },
  nameTextStyle: { color: INK.secondary, fontSize: 11, fontFamily: FONT },
};

export const TOOLTIP_STYLE = {
  backgroundColor: "#ffffff",
  borderColor: INK.border,
  borderWidth: 1,
  padding: [8, 10],
  textStyle: { color: INK.primary, fontSize: 12, fontFamily: FONT },
  extraCssText: "box-shadow: 0 4px 16px rgba(0,0,0,0.08); border-radius: 8px;",
};

export const LEGEND_STYLE = {
  textStyle: { color: INK.secondary, fontSize: 12, fontFamily: FONT },
  icon: "roundRect",
  itemWidth: 10,
  itemHeight: 10,
  itemGap: 14,
};

// Bars: at most 24 px thick, 4 px rounded data end, square at the baseline.
export const BAR_STYLE = {
  barMaxWidth: 24,
  itemStyle: { borderRadius: [4, 4, 0, 0] },
};

export const LINE_STYLE = {
  lineStyle: { width: 2, cap: "round" as const, join: "round" as const },
  symbolSize: 8,
};
