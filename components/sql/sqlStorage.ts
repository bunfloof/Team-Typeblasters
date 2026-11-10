"use client";
// What the SQL console remembers in this browser (localStorage): the query
// being written, the last 30 queries run, and the row limit. Conveniences
// only: if storage is blocked, everything still works for this page view.
import { useSyncExternalStore } from "react";

export type HistoryEntry = {
  sql: string;
  at: string; // ISO time it ran
  rowCount: number | null; // null when it failed
  ms: number | null;
  error: string | null;
};

const DRAFT_KEY = "typeblasters.sql.draft";
const HISTORY_KEY = "typeblasters.sql.history";
const MAX_ROWS_KEY = "typeblasters.sql.maxRows";
const HISTORY_SIZE = 30;

function read(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value);
  } catch {
    // storage blocked or full: nothing to do
  }
}

export function readDraft(): string | null {
  return read(DRAFT_KEY);
}

export function writeDraft(sql: string): void {
  write(DRAFT_KEY, sql);
}

export function readMaxRows(fallback: number): number {
  const value = Number(read(MAX_ROWS_KEY));
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

export function writeMaxRows(maxRows: number): void {
  write(MAX_ROWS_KEY, String(maxRows));
}

// --- history (an external store, so every menu shows the same list) ---

const listeners = new Set<() => void>();
let memoryHistory: HistoryEntry[] = []; // used when storage is blocked
let cachedRaw: string | null | undefined;
let cachedHistory: HistoryEntry[] = [];
const EMPTY: HistoryEntry[] = [];

function getSnapshot(): HistoryEntry[] {
  const raw = read(HISTORY_KEY);
  if (raw === null) {
    return memoryHistory;
  }
  if (raw !== cachedRaw) {
    cachedRaw = raw;
    try {
      const parsed = JSON.parse(raw);
      cachedHistory = Array.isArray(parsed) ? (parsed as HistoryEntry[]).filter((entry) => typeof entry?.sql === "string") : [];
    } catch {
      cachedHistory = [];
    }
  }
  return cachedHistory;
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  window.addEventListener("storage", listener);
  return () => {
    listeners.delete(listener);
    window.removeEventListener("storage", listener);
  };
}

export function useSqlHistory(): HistoryEntry[] {
  return useSyncExternalStore(subscribe, getSnapshot, () => EMPTY);
}

function saveHistory(entries: HistoryEntry[]): void {
  memoryHistory = entries;
  write(HISTORY_KEY, JSON.stringify(entries));
  listeners.forEach((listener) => listener());
}

// Newest first; running the same SQL again moves it to the top.
export function addToHistory(entry: HistoryEntry): void {
  const sql = entry.sql.trim();
  const rest = getSnapshot().filter((old) => old.sql.trim() !== sql);
  saveHistory([{ ...entry, sql }, ...rest].slice(0, HISTORY_SIZE));
}

export function clearHistory(): void {
  saveHistory([]);
}
