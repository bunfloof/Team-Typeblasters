// Plain-English labels and fixed colors for the values the game logs.
// Safe to import anywhere.
import { INK, KIND_COLORS, KIND_LABELS, SERIES } from "@/lib/theme";

// What dealt a hit: the five zombie kinds, the boss's energy orb, or other.
export const KILLER_ORDER = ["normal", "runner", "armored", "explosive", "shifter", "orb", "other"];

// The orb gets the first categorical slot no zombie kind uses (amber; run
// through the palette validator against the kind colors: CVD-separable).
export const ORB_COLOR = SERIES[3];
export const OTHER_COLOR = INK.axis;

export function killerLabel(killer: string): string {
  if (killer === "orb") return "Energy orb";
  if (killer === "other") return "Other";
  return KIND_LABELS[killer] ?? killer;
}

export function killerColor(killer: string): string {
  if (killer === "orb") return ORB_COLOR;
  return KIND_COLORS[killer] ?? OTHER_COLOR;
}

// A damage source (zombie_runner, orb) as a killer key.
export function sourceKiller(source: string): string {
  const kind = source.startsWith("zombie_") ? source.slice(7) : source;
  return kind === "orb" || KIND_COLORS[kind] ? kind : "other";
}

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

// typing_target / word target values: zombie_runner, crate_health, boss_head, ...
export function targetLabel(target: string): string {
  if (target === "none") return "Nothing";
  if (target === "unknown") return "Not logged";
  if (target === "barrel") return "Barrel";
  if (target === "orb") return "Energy orb";
  if (target === "quiz_answer") return "Quiz answer";
  if (target.startsWith("zombie_")) return (KIND_LABELS[target.slice(7)] ?? target.slice(7)) + " zombie";
  if (target.startsWith("crate_")) return capitalize(target.slice(6)) + " crate";
  if (target.startsWith("boss_")) return "Boss " + target.slice(5);
  return target;
}

export const RED_OUTCOME_LABELS: Record<string, string> = {
  word: "Blown up by its word",
  chained: "Chained by another blast",
  bite: "Bit the player",
  rode_past: "Rode past (ambush)",
  run_over: "Alive when the run ended",
  shot: "Shot",
  other: "Other",
};

export const BLAST_SOURCE_LABELS: Record<string, string> = {
  red: "Red zombie word",
  barrel: "Barrel",
  lure: "Lure bomb",
  rpg: "RPG rocket",
};

export const ITEM_LABELS: Record<string, string> = {
  lure: "Lure",
  freeze: "Freeze",
  frenzy: "FRENZY",
  rpg: "RPG",
  shield: "Shield",
  any: "Any of them",
};

export const REFUSAL_LABELS: Record<string, string> = {
  not_enough_energy: "Not enough energy",
  not_enough_energy_or_not_playing: "Not enough energy at release",
  already_active: "Already running",
  already_loaded: "RPG already loaded (older builds)",
  none_kept: "None kept (older builds)",
  frenzy_on: "FRENZY already on (older builds)",
  slots_full: "Slots full (older builds)",
  one_per_wave: "One per wave (older builds)",
  not_enough_combo: "Not enough combo (older builds)",
};

export const WORD_OUTCOME_LABELS: Record<string, string> = {
  finished: "Finished",
  dropped: "Dropped (Backspace)",
  dropped_by_frenzy: "Dropped by a FRENZY",
  target_gone: "Target gone",
  run_ended: "Run ended",
};

export const PAUSE_LABELS: Record<string, string> = {
  esc_key: "Esc key",
  tab_hidden: "Tab hidden",
  button: "Pause button",
};

export function positionLabel(position: number): string {
  return position === -1 ? "Too slow" : (["Left", "Middle", "Right"][position] ?? "Position " + position);
}

export function labelOf(map: Record<string, string>, key: string): string {
  return map[key] ?? key;
}

// A key as shown in a table: space and empty values made visible.
export function keyLabel(key: string): string {
  if (key === " ") return "␣ (space)";
  if (key === "") return "(none)";
  return key;
}
