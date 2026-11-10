// Colors, symbols and labels of the run timeline (shared by the timeline,
// the map, the Moment panel and the keys). Everything comes from lib/theme.ts:
// zombie kinds keep KIND_COLORS, bad things use STATUS.critical, and the
// event categories take the SERIES slots in their fixed order.
import { BLUE_RAMP, INK, KIND_COLORS, KIND_LABELS, SERIES, STATUS } from "@/lib/theme";
import { MARKER_CATEGORIES, type MarkerCategory, type PhaseKind } from "@/lib/timelineData";

// Phases are ordinal (calm ride < fight < boss): one hue, light -> dark.
export const PHASE_STYLE: Record<PhaseKind, { fill: string; text: string; label: string }> = {
  ride: { fill: "#e6e5df", text: INK.secondary, label: "Ride" },
  fight: { fill: BLUE_RAMP[3], text: INK.primary, label: "Fight" },
  boss: { fill: BLUE_RAMP[9], text: "#ffffff", label: "Boss fight" },
};

export function kindColor(kind: string): string {
  return KIND_COLORS[kind] ?? KIND_COLORS.normal;
}

export function kindLabel(kind: string): string {
  return KIND_LABELS[kind] ?? kind;
}

// How a zombie left, drawn as a glyph at the end of its bar.
export type EndGlyph = "dot" | "diamond" | "cross" | "ring";

export function endGlyph(outcome: string): EndGlyph {
  if (outcome === "shot") return "dot";
  if (outcome === "blast") return "diamond";
  if (outcome === "bite") return "cross";
  return "ring";
}

export const END_GLYPH_LABELS: Record<EndGlyph, string> = {
  dot: "shot (typed)",
  diamond: "blown up",
  cross: "bit the player",
  ring: "other end (rode past, never seen, run over…)",
};

export const ZOMBIE_OUTCOME_LABELS: Record<string, string> = {
  shot: "Shot (its word was typed)",
  blast: "Killed by an explosion",
  bite: "Reached the player and bit",
  rode_past: "Left behind on a ride",
  never_seen: "Ambusher never woken",
  lost: "Lost (fell off the map)",
  boss_died: "Removed when the boss died",
  run_over: "Alive when the run ended",
  alive: "Still alive when the log stops",
};

// Word attempts by how they ended (finished = good, dropped = bad).
export const WORD_OUTCOMES = [
  { id: "finished", label: "Word finished", color: STATUS.good },
  { id: "dropped", label: "Word dropped", color: STATUS.serious },
  { id: "target_gone", label: "Target gone", color: INK.muted },
  { id: "other", label: "Other end", color: "#c3c2b7" },
] as const;

export type WordOutcomeGroup = (typeof WORD_OUTCOMES)[number]["id"];

export function wordOutcomeGroup(outcome: string): WordOutcomeGroup {
  if (outcome === "finished" || outcome === "dropped" || outcome === "target_gone") return outcome;
  return "other";
}

export const WORD_OUTCOME_LABELS: Record<string, string> = {
  finished: "Finished",
  dropped: "Dropped (Backspace)",
  dropped_by_frenzy: "Dropped by a FRENZY",
  target_gone: "Target gone (blown up, bit…)",
  run_ended: "Run ended while typing",
};

// A filled "X" (ECharts symbols are filled shapes).
export const CROSS_SYMBOL = "path://M2,0 L5,3 L8,0 L10,2 L7,5 L10,8 L8,10 L5,7 L2,10 L0,8 L3,5 L0,2 Z";
// A thin vertical tick (wrong keys, pauses).
export const TICK_SYMBOL = "rect";

// Event-lane categories: damage is the one "bad" status color; the other
// categories take SERIES slots in order (never more than 8 colors).
export const MARKER_STYLE: Record<MarkerCategory, { color: string; symbol: string; label: string }> = (() => {
  const symbols: Record<MarkerCategory, string> = {
    damage: CROSS_SYMBOL,
    blast: "circle",
    power: "diamond",
    crate: "rect",
    boss: "triangle",
    help: "roundRect",
    away: "pin",
    run: "arrow",
  };
  const style = {} as Record<MarkerCategory, { color: string; symbol: string; label: string }>;
  let slot = 0;
  for (const category of MARKER_CATEGORIES) {
    const color = category.id === "damage" ? STATUS.critical : SERIES[slot++];
    style[category.id] = { color, symbol: symbols[category.id], label: category.label };
  }
  return style;
})();

export const WRONG_KEY_STYLE = {
  wrong: { label: "Wrong key", color: STATUS.critical },
  caseOnly: { label: "Wrong case only", color: STATUS.serious },
  stray: { label: "Stray key (no word)", color: INK.muted },
};

// Neutral line colors for the single-measure lanes (identity = lane title).
export const LINE_INK = INK.secondary;
export const SELECTION_INK = INK.primary;
export const PLAYER_DOT = SERIES[0];
