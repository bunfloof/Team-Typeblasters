"use client";
// The filters in the top bar that apply to every page: include the team's
// test plays (install_id "qa-..."), which level, and a time range (e.g. only
// the class playtest, without the team's own plays before it). Kept in
// localStorage, so they survive a page reload; every page re-renders when
// they change.
import { useSyncExternalStore } from "react";

export type GlobalFilters = {
  includeTest: boolean;
  level: string; // scene name ("Prototype", "Hard"); "" = all levels
  from: string; // events received from this time on (UTC "YYYY-MM-DD HH:MM:SS"); "" = no start
  to: string; // events received up to this time (UTC, inclusive); "" = no end
};

const KEY = "typeblasters.globalFilters";
const DEFAULTS: GlobalFilters = { includeTest: false, level: "", from: "", to: "" };
const listeners = new Set<() => void>();

let cachedRaw: string | null | undefined;
let cachedValue: GlobalFilters = DEFAULTS;
// Used instead of localStorage when the browser blocks it (until a reload).
let memoryValue: GlobalFilters | null = null;

function readStorage(): string | null {
  try {
    return window.localStorage.getItem(KEY);
  } catch {
    return null;
  }
}

function getSnapshot(): GlobalFilters {
  if (memoryValue) {
    return memoryValue;
  }
  const raw = readStorage();
  if (raw !== cachedRaw) {
    cachedRaw = raw;
    try {
      cachedValue = { ...DEFAULTS, ...(raw ? (JSON.parse(raw) as Partial<GlobalFilters>) : {}) };
    } catch {
      cachedValue = DEFAULTS;
    }
  }
  return cachedValue;
}

function getServerSnapshot(): GlobalFilters {
  return DEFAULTS;
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  window.addEventListener("storage", listener);
  return () => {
    listeners.delete(listener);
    window.removeEventListener("storage", listener);
  };
}

export function useGlobalFilters(): GlobalFilters {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}

export function setGlobalFilters(patch: Partial<GlobalFilters>): void {
  const next = { ...getSnapshot(), ...patch };
  try {
    window.localStorage.setItem(KEY, JSON.stringify(next));
    memoryValue = null;
  } catch {
    // storage blocked: the change only lasts until the page reloads
    memoryValue = next;
  }
  listeners.forEach((listener) => listener());
}

// The global filters as API query parameters (includeTest=1, level=...,
// from=..., to=...: every API filters received_at with them).
export function globalParams(filters: GlobalFilters): Record<string, string> {
  const params: Record<string, string> = {};
  if (filters.includeTest) params.includeTest = "1";
  if (filters.level) params.level = filters.level;
  if (filters.from) params.from = filters.from;
  if (filters.to) params.to = filters.to;
  return params;
}

// ---- The time range ----

function pad(n: number): string {
  return String(n).padStart(2, "0");
}

// A Date as UTC "YYYY-MM-DD HH:MM:SS" (the database's received_at format).
export function toUtcText(date: Date): string {
  return date.toISOString().slice(0, 19).replace("T", " ");
}

// UTC "YYYY-MM-DD HH:MM:SS(.ffffff)" -> a Date (null if it is not a time).
export function fromUtcText(text: string): Date | null {
  if (!text) return null;
  // (at most 3 decimals: some browsers do not parse microseconds)
  const iso = text.trim().replace(" ", "T").replace(/(\.\d{3})\d+/, "$1");
  const date = new Date(iso.endsWith("Z") ? iso : iso + "Z");
  return Number.isNaN(date.getTime()) ? null : date;
}

// UTC text -> the value of an <input type="datetime-local"> (local time, minutes).
export function utcToLocalInput(text: string): string {
  const date = fromUtcText(text);
  if (!date) return "";
  return date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate()) + "T" + pad(date.getHours()) + ":" + pad(date.getMinutes());
}

// A datetime-local value (local time) -> UTC text. The end of a range covers
// its whole last minute.
export function localInputToUtc(value: string, end: boolean): string {
  if (!value) return "";
  const date = new Date(value); // "YYYY-MM-DDTHH:MM" is read as local time
  if (Number.isNaN(date.getTime())) return "";
  date.setSeconds(0, 0);
  return end ? toUtcText(date).slice(0, 16) + ":59.999999" : toUtcText(date);
}

// "Nov 6, 1:05 PM" (local time) of a UTC text.
export function shortLocalTime(text: string, withDate = true): string {
  const date = fromUtcText(text);
  if (!date) return text;
  return date.toLocaleString(undefined, withDate ? { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" } : { hour: "numeric", minute: "2-digit" });
}

// The range in a few words: "All time", "Nov 6, 1:05 PM - 2:40 PM", "From Nov 6, 1:05 PM".
export function rangeLabel(filters: { from: string; to: string }): string {
  if (!filters.from && !filters.to) return "All time";
  if (!filters.to) return "From " + shortLocalTime(filters.from);
  if (!filters.from) return "Until " + shortLocalTime(filters.to);
  const start = fromUtcText(filters.from);
  const end = fromUtcText(filters.to);
  const sameDay = !!start && !!end && start.toDateString() === end.toDateString();
  return shortLocalTime(filters.from) + " – " + shortLocalTime(filters.to, !sameDay);
}

// The browser's time zone name ("America/Los_Angeles"), shown next to the inputs.
export function localTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || "local time";
  } catch {
    return "local time";
  }
}
