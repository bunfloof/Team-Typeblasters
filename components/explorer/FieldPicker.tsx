"use client";
// Chooses which fields of the event data are shown as columns (and whether
// the raw data JSON column is shown). Applied when the panel closes.
import { useRef, useState } from "react";
import { Columns3, Search } from "lucide-react";
import { buttonClass, inputClass, primaryButtonClass } from "@/components/ui";
import { useDismiss } from "./useDismiss";

export type FieldCandidate = {
  key: string;
  seen: boolean; // found in the stored data (else only described by the catalog)
  doc?: string;
};

type Props = {
  candidates: FieldCandidate[];
  selected: string[];
  raw: boolean;
  onApply: (fields: string[], raw: boolean) => void;
  onReset: () => void; // back to the automatic choice
};

export default function FieldPicker({ candidates, selected, raw, onApply, onReset }: Props) {
  const boxRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<string[]>(selected);
  const [draftRaw, setDraftRaw] = useState(raw);
  const [search, setSearch] = useState("");

  const openPanel = () => {
    setDraft(selected);
    setDraftRaw(raw);
    setSearch("");
    setOpen(true);
  };
  const finish = () => {
    setOpen(false);
    const changed = draftRaw !== raw || draft.length !== selected.length || draft.some((field, i) => field !== selected[i]);
    if (changed) {
      onApply(draft, draftRaw);
    }
  };
  useDismiss(boxRef, open, finish);

  // Selected fields keep their order; a newly ticked one goes last.
  const toggle = (key: string) =>
    setDraft((current) => (current.includes(key) ? current.filter((k) => k !== key) : [...current, key]));

  const needle = search.trim().toLowerCase();
  const shown = candidates.filter(
    (candidate) => !needle || candidate.key.toLowerCase().includes(needle) || (candidate.doc || "").toLowerCase().includes(needle),
  );
  const sections = [
    { title: "Seen in the data", items: shown.filter((candidate) => candidate.seen) },
    { title: "In the catalog, not logged yet", items: shown.filter((candidate) => !candidate.seen) },
  ];

  return (
    <div ref={boxRef} className="relative">
      <button type="button" onClick={() => (open ? finish() : openPanel())} className={buttonClass} aria-expanded={open}>
        <Columns3 className="h-3.5 w-3.5" /> Data columns
        <span className="rounded bg-neutral-100 px-1 text-xs tabular-nums text-neutral-600">{selected.length + (raw ? 1 : 0)}</span>
      </button>
      {open ? (
        <div className="absolute right-0 top-9 z-30 w-[380px] rounded-lg border border-black/10 bg-white shadow-[0_8px_30px_rgba(0,0,0,0.10)]">
          <div className="space-y-2 border-b border-black/5 p-2">
            <label className="relative block">
              <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-neutral-400" />
              <input
                autoFocus
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") finish();
                }}
                placeholder="Find a field"
                className={inputClass + " w-full pl-7"}
              />
            </label>
            <label className="flex cursor-pointer items-center gap-2 px-1 text-sm text-neutral-700">
              <input
                type="checkbox"
                checked={draftRaw}
                onChange={(event) => setDraftRaw(event.target.checked)}
                className="h-3.5 w-3.5 accent-[#2a78d6]"
              />
              Raw <span className="font-mono text-[12.5px]">data</span> JSON column
            </label>
          </div>
          <div className="max-h-[380px] overflow-y-auto px-1 py-1">
            {sections.map((section) =>
              section.items.length === 0 ? null : (
                <div key={section.title} className="py-1">
                  <div className="px-2 pb-0.5 pt-1 text-[11px] font-medium uppercase tracking-wide text-neutral-400">{section.title}</div>
                  {section.items.map((candidate) => (
                    <label
                      key={candidate.key}
                      title={candidate.doc}
                      className="flex cursor-pointer items-start gap-2 rounded-md px-2 py-1 text-sm hover:bg-neutral-50"
                    >
                      <input
                        type="checkbox"
                        checked={draft.includes(candidate.key)}
                        onChange={() => toggle(candidate.key)}
                        className="mt-0.5 h-3.5 w-3.5 accent-[#2a78d6]"
                      />
                      <span className="min-w-0 flex-1">
                        <span className={"font-mono text-[12.5px] " + (candidate.seen ? "text-neutral-800" : "text-neutral-500")}>
                          {candidate.key}
                        </span>
                        {candidate.doc ? <span className="block truncate text-xs text-neutral-500">{candidate.doc}</span> : null}
                      </span>
                    </label>
                  ))}
                </div>
              ),
            )}
            {shown.length === 0 ? <div className="px-2 py-6 text-center text-sm text-neutral-500">No field matches.</div> : null}
          </div>
          <div className="flex flex-wrap items-center gap-2 border-t border-black/5 p-2">
            <button
              type="button"
              className={buttonClass}
              onClick={() => setDraft((current) => [...current, ...shown.map((c) => c.key).filter((key) => !current.includes(key))])}
            >
              All shown
            </button>
            <button type="button" className={buttonClass} onClick={() => setDraft([])} disabled={draft.length === 0}>
              None
            </button>
            <button
              type="button"
              className={buttonClass}
              onClick={() => {
                setOpen(false);
                onReset();
              }}
              title="Back to the automatic columns for the chosen event types"
            >
              Default
            </button>
            <button type="button" className={primaryButtonClass + " ml-auto"} onClick={finish}>
              Apply
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
