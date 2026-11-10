"use client";
// The Events tab's filter row. Text boxes apply on Enter or "Apply"; the
// event-type picker, build and "hide duplicates" apply at once. The parent
// re-mounts it (key) whenever the URL changes, so it always starts from
// the applied filters.
import { useMemo, useState } from "react";
import { Plus, RotateCcw, X } from "lucide-react";
import type { MetaResponse } from "@/lib/types";
import { buttonClass, inputClass, primaryButtonClass } from "@/components/ui";
import EventTypePicker from "./EventTypePicker";
import JsonConditions from "./JsonConditions";
import { Field, TextFilter, TimeFilter } from "./FilterParts";
import { EMPTY_EVENT_DRAFT, fieldCandidates, type EventDraft } from "./explorerUrl";

type Props = {
  applied: EventDraft;
  meta: MetaResponse | null;
  onApply: (draft: EventDraft) => void;
  onReset: () => void;
};

export default function EventFilterBar({ applied, meta, onApply, onReset }: Props) {
  const [draft, setDraft] = useState<EventDraft>(applied);
  const dirty = JSON.stringify(draft) !== JSON.stringify(applied);
  const filtered = JSON.stringify(applied) !== JSON.stringify(EMPTY_EVENT_DRAFT);

  const set = (patch: Partial<EventDraft>) => setDraft((current) => ({ ...current, ...patch }));
  const commit = (patch: Partial<EventDraft> = {}) => {
    const next = { ...draft, ...patch };
    setDraft(next);
    onApply(next);
  };

  const counts = useMemo(
    () => (meta ? Object.fromEntries(meta.eventCounts.map((row) => [row.event, row.count])) : null),
    [meta],
  );
  const fieldOptions = useMemo(() => fieldCandidates(draft.events, meta).map((c) => c.key), [draft.events, meta]);

  return (
    <div className="space-y-3 rounded-xl border border-black/10 bg-[#fcfcfb] p-4">
      <div className="flex flex-wrap items-end gap-3">
        <Field label="Event types">
          <EventTypePicker selected={draft.events} counts={counts} onApply={(events) => commit({ events })} />
        </Field>
        <TextFilter
          label="install_id (player)"
          value={draft.installId}
          onChange={(installId) => set({ installId })}
          onSubmit={() => commit()}
          placeholder="any"
          width="w-[140px]"
          mono
        />
        <TextFilter
          label="session_id (page load)"
          value={draft.sessionId}
          onChange={(sessionId) => set({ sessionId })}
          onSubmit={() => commit()}
          placeholder="any"
          width="w-[140px]"
          mono
        />
        <TextFilter
          label="run_id"
          value={draft.runId}
          onChange={(runId) => set({ runId })}
          onSubmit={() => commit()}
          placeholder="any"
          width="w-[140px]"
          mono
        />
        <Field label="Build">
          <select value={draft.build} onChange={(event) => commit({ build: event.target.value })} className={inputClass + " w-[140px]"}>
            <option value="">All builds</option>
            {(meta?.builds ?? []).map((build) => (
              <option key={build} value={build}>
                {build}
              </option>
            ))}
            {draft.build && !(meta?.builds ?? []).includes(draft.build) ? <option value={draft.build}>{draft.build}</option> : null}
          </select>
        </Field>
        <Field label="Wave">
          <div className="flex items-center gap-1">
            {(["waveMin", "waveMax"] as const).map((key, i) => (
              <span key={key} className="flex items-center gap-1">
                {i === 1 ? <span className="text-neutral-400">–</span> : null}
                <input
                  type="number"
                  min={0}
                  value={draft[key]}
                  onChange={(event) => set({ [key]: event.target.value })}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") commit();
                  }}
                  placeholder={i === 0 ? "min" : "max"}
                  aria-label={i === 0 ? "Lowest wave" : "Highest wave"}
                  className={inputClass + " w-16 tabular-nums"}
                />
              </span>
            ))}
          </div>
        </Field>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <TimeFilter label="Received from" utc={draft.from} onChange={(from) => set({ from })} onSubmit={() => commit()} />
        <TimeFilter label="Received until" utc={draft.to} onChange={(to) => set({ to })} onSubmit={() => commit()} />
        <TextFilter
          label="Text anywhere in data"
          value={draft.search}
          onChange={(search) => set({ search })}
          onSubmit={() => commit()}
          placeholder="e.g. zombie_runner"
          width="w-48"
        />
        <button
          type="button"
          className={buttonClass}
          onClick={() => set({ json: [...draft.json, { field: "", op: "=", value: "" }] })}
          title="Filter on a field inside the event's data, e.g. kind = runner"
        >
          <Plus className="h-3.5 w-3.5" /> Data condition
        </button>
        <label
          className="flex h-8 cursor-pointer items-center gap-2 text-sm text-neutral-700"
          title="A batch the game re-sent is stored twice with the same event_id. On: show only the first stored copy."
        >
          <input
            type="checkbox"
            checked={draft.dedupe}
            onChange={(event) => commit({ dedupe: event.target.checked })}
            className="h-4 w-4 accent-[#2a78d6]"
          />
          Hide duplicates
        </label>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <JsonConditions
          conditions={draft.json}
          fieldOptions={fieldOptions}
          onChange={(json) => set({ json })}
          onSubmit={() => commit()}
        />
        {draft.requestId ? (
          <span className="inline-flex items-center gap-1.5 rounded-full border border-blue-200 bg-blue-50/60 py-0.5 pl-2.5 pr-1 text-xs text-neutral-700">
            Only events of request #{draft.requestId}
            <button
              type="button"
              onClick={() => commit({ requestId: "" })}
              className="rounded-full p-0.5 text-neutral-500 hover:bg-white hover:text-neutral-800"
              aria-label="Remove the request filter"
            >
              <X className="h-3 w-3" />
            </button>
          </span>
        ) : null}
        {draft.json.length === 0 && !draft.requestId ? (
          <span className="text-xs text-neutral-500">Text boxes apply on Enter. Times are in your local time zone.</span>
        ) : null}
        <div className="ml-auto flex items-center gap-2">
          <button type="button" className={buttonClass} onClick={onReset} disabled={!filtered && !dirty}>
            <RotateCcw className="h-3.5 w-3.5" /> Reset
          </button>
          <button type="button" className={primaryButtonClass} onClick={() => commit()} disabled={!dirty}>
            Apply
          </button>
        </div>
      </div>
    </div>
  );
}
