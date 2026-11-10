// The catalog's event groups, in reading order, with their labels (used by
// the Explorer's event-type picker and the Event catalog page).
import { EVENTS, type EventDoc } from "@/lib/catalog";

export const GROUP_ORDER: EventDoc["group"][] = ["session", "run", "level", "typing", "zombies", "combat", "powers", "boss", "ui"];

export const GROUP_LABELS: Record<EventDoc["group"], string> = {
  session: "Session",
  run: "Run",
  level: "Level & fights",
  typing: "Typing",
  zombies: "Zombies",
  combat: "Combat",
  powers: "Powers & crates",
  boss: "Boss",
  ui: "Interface & tips",
};

export type EventGroup = { key: string; label: string; types: string[] };

// Catalog event types by group, then an "Other" group for types found in the
// database that the catalog does not describe.
export function groupEventTypes(typesInDatabase: string[] = []): EventGroup[] {
  const groups: EventGroup[] = GROUP_ORDER.map((group) => ({
    key: group,
    label: GROUP_LABELS[group],
    types: Object.keys(EVENTS).filter((type) => EVENTS[type].group === group),
  }));
  const other = typesInDatabase.filter((type) => !EVENTS[type]).sort();
  if (other.length > 0) {
    groups.push({ key: "other", label: "Other (not in the catalog)", types: other });
  }
  return groups.filter((group) => group.types.length > 0);
}
