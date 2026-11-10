"use client";
// The Requests tab's filter row (one row of the requests table = one HTTP
// request to the log server: its IP, browser and the batch of events).
// Text boxes apply on Enter or "Apply"; the method applies at once.
import { useState } from "react";
import { RotateCcw } from "lucide-react";
import { buttonClass, inputClass, primaryButtonClass } from "@/components/ui";
import { Field, TextFilter, TimeFilter } from "./FilterParts";
import { EMPTY_REQUEST_DRAFT, type RequestDraft } from "./explorerUrl";

const METHODS = ["GET", "POST", "OPTIONS", "HEAD", "PUT"];

type Props = {
  applied: RequestDraft;
  levelIgnored: boolean; // the top bar's level filter has no meaning for requests
  onApply: (draft: RequestDraft) => void;
  onReset: () => void;
};

export default function RequestFilterBar({ applied, levelIgnored, onApply, onReset }: Props) {
  const [draft, setDraft] = useState<RequestDraft>(applied);
  const dirty = JSON.stringify(draft) !== JSON.stringify(applied);
  const filtered = JSON.stringify(applied) !== JSON.stringify(EMPTY_REQUEST_DRAFT);

  const set = (patch: Partial<RequestDraft>) => setDraft((current) => ({ ...current, ...patch }));
  const commit = (patch: Partial<RequestDraft> = {}) => {
    const next = { ...draft, ...patch };
    setDraft(next);
    onApply(next);
  };

  return (
    <div className="space-y-2 rounded-xl border border-black/10 bg-[#fcfcfb] p-4">
      <div className="flex flex-wrap items-end gap-3">
        <TextFilter
          label="client_ip"
          value={draft.clientIp}
          onChange={(clientIp) => set({ clientIp })}
          onSubmit={() => commit()}
          placeholder="exact IP"
          width="w-40"
          mono
        />
        <Field label="Method">
          <select value={draft.method} onChange={(event) => commit({ method: event.target.value })} className={inputClass}>
            <option value="">Any</option>
            {METHODS.map((method) => (
              <option key={method} value={method}>
                {method}
              </option>
            ))}
          </select>
        </Field>
        <TextFilter label="Path contains" value={draft.path} onChange={(path) => set({ path })} onSubmit={() => commit()} width="w-36" mono />
        <TextFilter
          label="Status"
          value={draft.status}
          onChange={(status) => set({ status })}
          onSubmit={() => commit()}
          placeholder="200"
          width="w-20"
        />
        <TimeFilter label="Received from" utc={draft.from} onChange={(from) => set({ from })} onSubmit={() => commit()} />
        <TimeFilter label="Received until" utc={draft.to} onChange={(to) => set({ to })} onSubmit={() => commit()} />
        <TextFilter
          label="Text in body or headers"
          value={draft.search}
          onChange={(search) => set({ search })}
          onSubmit={() => commit()}
          placeholder="e.g. Firefox"
          width="w-44"
        />
        <div className="ml-auto flex items-center gap-2">
          <button type="button" className={buttonClass} onClick={onReset} disabled={!filtered && !dirty}>
            <RotateCcw className="h-3.5 w-3.5" /> Reset
          </button>
          <button type="button" className={primaryButtonClass} onClick={() => commit()} disabled={!dirty}>
            Apply
          </button>
        </div>
      </div>
      {levelIgnored ? (
        <p className="text-xs text-neutral-500">The level chosen in the top bar does not apply here: a request has no level (its events do).</p>
      ) : null}
    </div>
  );
}
