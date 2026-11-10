"use client";
// The top bar's time range: every page then only counts the events received
// between From and To. Typed in local time (the browser's time zone), kept
// as UTC. The panel also lists the play sessions found in the data, so the
// class playtest can be picked without the team's own plays before it.
import { useState } from "react";
import { CalendarClock, X } from "lucide-react";
import Popover from "@/components/sql/Popover";
import { buttonClass, inputClass, primaryButtonClass } from "@/components/ui";
import { levelName } from "@/lib/catalog";
import { fmtInt } from "@/lib/format";
import {
  fromUtcText,
  localInputToUtc,
  localTimeZone,
  rangeLabel,
  setGlobalFilters,
  shortLocalTime,
  toUtcText,
  useGlobalFilters,
  utcToLocalInput,
  type GlobalFilters,
} from "@/lib/globalFilters";
import { apiUrl, useApi } from "@/lib/useApi";
import type { ActivityResponse, PlaySession } from "@/lib/activityServer";

export default function TimeRangeControl() {
  const filters = useGlobalFilters();
  const active = !!(filters.from || filters.to);
  return (
    <div className="flex items-center gap-1">
      <Popover
        label={
          <>
            <CalendarClock className={"h-3.5 w-3.5 " + (active ? "text-[#2a78d6]" : "text-neutral-400")} />
            <span className={active ? "font-medium text-[#1c5cab]" : ""}>{rangeLabel(filters)}</span>
          </>
        }
        title="Only count the events received in this time range"
        width={480}
      >
        {(close) => <RangePanel filters={filters} close={close} />}
      </Popover>
      {active ? (
        <button
          type="button"
          onClick={() => setGlobalFilters({ from: "", to: "" })}
          className="inline-flex h-8 w-8 items-center justify-center rounded-md text-neutral-500 hover:bg-black/[0.05] hover:text-neutral-800"
          aria-label="Clear the time range (all time)"
          title="Clear the time range (all time)"
        >
          <X className="h-4 w-4" />
        </button>
      ) : null}
    </div>
  );
}

// The minute a UTC time falls in: its start, or its end (inclusive).
function minuteStart(utc: string): string {
  const date = fromUtcText(utc);
  if (!date) return utc;
  date.setUTCSeconds(0, 0);
  return toUtcText(date);
}

function minuteEnd(utc: string): string {
  return minuteStart(utc).slice(0, 16) + ":59.999999";
}

function quickRange(kind: "hour" | "3hours" | "today" | "week"): { from: string; to: string } {
  const now = new Date();
  const start = new Date(now);
  if (kind === "hour") start.setHours(now.getHours() - 1);
  if (kind === "3hours") start.setHours(now.getHours() - 3);
  if (kind === "today") start.setHours(0, 0, 0, 0);
  if (kind === "week") start.setDate(now.getDate() - 7);
  start.setSeconds(0, 0);
  return { from: toUtcText(start), to: "" };
}

const QUICK: { kind: "hour" | "3hours" | "today" | "week"; label: string }[] = [
  { kind: "hour", label: "Last hour" },
  { kind: "3hours", label: "Last 3 hours" },
  { kind: "today", label: "Today" },
  { kind: "week", label: "Last 7 days" },
];

function RangePanel({ filters, close }: { filters: GlobalFilters; close: () => void }) {
  const [from, setFrom] = useState(utcToLocalInput(filters.from));
  const [to, setTo] = useState(utcToLocalInput(filters.to));
  const apply = (range: { from: string; to: string }) => {
    setGlobalFilters(range);
    close();
  };
  const fromUtc = localInputToUtc(from, false);
  const toUtc = localInputToUtc(to, true);
  const backwards = !!fromUtc && !!toUtc && fromUtc > toUtc;

  return (
    <div className="p-2 text-sm">
      <div className="mb-2 flex items-baseline justify-between gap-2">
        <span className="font-medium text-neutral-900">Time range</span>
        <span className="text-xs text-neutral-500">Times in {localTimeZone()}</span>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1 text-xs text-neutral-600">
          From
          <input type="datetime-local" value={from} onChange={(event) => setFrom(event.target.value)} className={inputClass} />
        </label>
        <label className="flex flex-col gap-1 text-xs text-neutral-600">
          To (empty = until now)
          <input type="datetime-local" value={to} onChange={(event) => setTo(event.target.value)} className={inputClass} />
        </label>
      </div>
      {backwards ? <p className="mt-1.5 text-xs text-red-700">“From” is after “To”: nothing would match.</p> : null}
      <div className="mt-2 flex flex-wrap items-center gap-1.5">
        <button type="button" className={primaryButtonClass} disabled={backwards} onClick={() => apply({ from: fromUtc, to: toUtc })}>
          Apply
        </button>
        <button type="button" className={buttonClass} onClick={() => apply({ from: "", to: "" })}>
          All time
        </button>
        <span className="mx-1 h-5 w-px bg-black/10" aria-hidden />
        {QUICK.map((q) => (
          <button key={q.kind} type="button" className="rounded-md px-2 py-1 text-xs text-[#1c5cab] hover:bg-[#2a78d6]/10" onClick={() => apply(quickRange(q.kind))}>
            {q.label}
          </button>
        ))}
      </div>
      <PlaySessions filters={filters} onPick={apply} />
    </div>
  );
}

// The play sessions in the data (same test-play and level filters), newest first.
function PlaySessions({ filters, onPick }: { filters: GlobalFilters; onPick: (range: { from: string; to: string }) => void }) {
  const { data, error, loading } = useApi<ActivityResponse>(apiUrl("/api/activity", { includeTest: filters.includeTest, level: filters.level }));
  return (
    <div className="mt-3 border-t border-black/10 pt-2">
      <div className="mb-1 font-medium text-neutral-900">Play sessions</div>
      <p className="mb-2 text-xs leading-relaxed text-neutral-500">
        Stretches of play{data ? " with no gap over " + data.gapMinutes + " minutes" : ""}. Pick one to see only its events: a session with many players is
        likely the class playtest, one with 1 to 3 players the team testing.
      </p>
      {error ? <p className="text-xs text-red-700">Could not load the play sessions: {error}</p> : null}
      {!data && loading ? <p className="py-2 text-xs text-neutral-500">Loading…</p> : null}
      {data && data.sessions.length === 0 ? <p className="py-2 text-xs text-neutral-500">No events for these filters.</p> : null}
      {data ? (
        <ul className="flex flex-col gap-0.5">
          {data.sessions.map((session) => (
            <SessionRow key={session.start} session={session} filters={filters} onPick={onPick} />
          ))}
        </ul>
      ) : null}
      {data?.truncated ? <p className="mt-1 text-xs text-neutral-500">Only the {fmtInt(data.sessions.length)} most recent are listed.</p> : null}
    </div>
  );
}

function SessionRow({ session, filters, onPick }: { session: PlaySession; filters: GlobalFilters; onPick: (range: { from: string; to: string }) => void }) {
  const range = { from: minuteStart(session.start), to: minuteEnd(session.end) };
  const selected = filters.from === range.from && filters.to === range.to;
  const start = fromUtcText(session.start);
  const end = fromUtcText(session.end);
  const minutes = start && end ? Math.max(1, Math.round((end.getTime() - start.getTime()) / 60000)) : null;
  const day = start ? start.toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" }) : "";
  return (
    <li>
      <button
        type="button"
        onClick={() => onPick(range)}
        aria-pressed={selected}
        className={
          "flex w-full flex-wrap items-baseline gap-x-2 rounded-md px-2 py-1.5 text-left hover:bg-neutral-50 " +
          (selected ? "bg-[#2a78d6]/10 ring-1 ring-[#2a78d6]/30 " : "") +
          (session.runs === 0 ? "opacity-60" : "") // the menu was opened, nothing was played
        }
      >
        <span className="w-[92px] shrink-0 text-xs text-neutral-500">{day}</span>
        <span className="tabular-nums text-neutral-800">
          {shortLocalTime(session.start, false)} – {shortLocalTime(session.end, false)}
        </span>
        {minutes !== null ? <span className="text-xs text-neutral-400">{minutes < 90 ? minutes + " min" : Math.round(minutes / 6) / 10 + " h"}</span> : null}
        <span className="ml-auto text-xs tabular-nums text-neutral-700">
          <span className="font-medium">{fmtInt(session.players)}</span> {session.players === 1 ? "player" : "players"} · {fmtInt(session.runs)}{" "}
          {session.runs === 1 ? "run" : "runs"}
          {session.test_players > 0 ? <span className="text-neutral-500"> · {fmtInt(session.test_players)} test</span> : null}
        </span>
        <span className="basis-full pl-[100px] text-[11px] text-neutral-400">{session.levels.map(levelName).join(", ")}</span>
      </button>
    </li>
  );
}
