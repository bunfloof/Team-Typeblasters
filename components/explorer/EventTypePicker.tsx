"use client";
// Multi-select of event types (grouped as in the catalog) with the number of
// rows of each. The choice is applied when the panel closes.
import { useMemo, useRef, useState } from "react";
import { ChevronDown, Search } from "lucide-react";
import { EVENTS } from "@/lib/catalog";
import { fmtInt } from "@/lib/format";
import { buttonClass, inputClass, primaryButtonClass } from "@/components/ui";
import { groupEventTypes } from "./eventGroups";
import { useDismiss } from "./useDismiss";

type Props = {
  selected: string[];
  counts: Record<string, number> | null; // null while /api/meta loads
  onApply: (events: string[]) => void;
};

function sameSet(a: string[], b: string[]): boolean {
  return a.length === b.length && a.every((value) => b.includes(value));
}

export default function EventTypePicker({ selected, counts, onApply }: Props) {
  const boxRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<string[]>(selected);
  const [search, setSearch] = useState("");

  const groups = useMemo(() => groupEventTypes(counts ? Object.keys(counts) : []), [counts]);
  const needle = search.trim().toLowerCase();
  const matches = (type: string) =>
    !needle || type.toLowerCase().includes(needle) || (EVENTS[type]?.title || "").toLowerCase().includes(needle);

  const openPanel = () => {
    setDraft(selected);
    setSearch("");
    setOpen(true);
  };
  const finish = () => {
    setOpen(false);
    if (!sameSet(draft, selected)) {
      onApply(draft);
    }
  };
  useDismiss(boxRef, open, finish);

  const toggle = (type: string) =>
    setDraft((current) => (current.includes(type) ? current.filter((t) => t !== type) : [...current, type]));

  const label =
    selected.length === 0 ? "All event types" : selected.length === 1 ? selected[0] : selected.length + " event types";

  return (
    <div ref={boxRef} className="relative">
      <button
        type="button"
        onClick={() => (open ? finish() : openPanel())}
        className={buttonClass + " min-w-44 justify-between" + (selected.length > 0 ? " border-blue-300 bg-blue-50/40" : "")}
        aria-expanded={open}
      >
        <span className="truncate font-mono text-[13px]">{label}</span>
        <ChevronDown className="h-3.5 w-3.5 text-neutral-400" />
      </button>
      {open ? (
        <div className="absolute left-0 top-9 z-30 w-[340px] rounded-lg border border-black/10 bg-white shadow-[0_8px_30px_rgba(0,0,0,0.10)]">
          <div className="border-b border-black/5 p-2">
            <label className="relative block">
              <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-neutral-400" />
              <input
                autoFocus
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") finish();
                }}
                placeholder="Find an event type"
                className={inputClass + " w-full pl-7"}
              />
            </label>
          </div>
          <div className="max-h-[420px] overflow-y-auto px-1 py-1">
            {groups.map((group) => {
              const types = group.types.filter(matches);
              if (types.length === 0) {
                return null;
              }
              return (
                <div key={group.key} className="py-1">
                  <div className="px-2 pb-0.5 pt-1 text-[11px] font-medium uppercase tracking-wide text-neutral-400">{group.label}</div>
                  {types.map((type) => {
                    const count = counts ? counts[type] ?? 0 : null;
                    return (
                      <label
                        key={type}
                        className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1 text-sm hover:bg-neutral-50"
                      >
                        <input
                          type="checkbox"
                          checked={draft.includes(type)}
                          onChange={() => toggle(type)}
                          className="h-3.5 w-3.5 accent-[#2a78d6]"
                        />
                        <span className="min-w-0 flex-1 truncate">
                          <span className="font-mono text-[12.5px] text-neutral-800">{type}</span>
                          {EVENTS[type] ? <span className="ml-1.5 text-xs text-neutral-500">{EVENTS[type].title}</span> : null}
                        </span>
                        <span className={"text-xs tabular-nums " + (count === 0 ? "text-neutral-300" : "text-neutral-500")}>
                          {count === null ? "" : fmtInt(count)}
                        </span>
                      </label>
                    );
                  })}
                </div>
              );
            })}
          </div>
          <div className="flex items-center gap-2 border-t border-black/5 p-2">
            <button type="button" className={buttonClass} onClick={() => setDraft([])} disabled={draft.length === 0}>
              Clear
            </button>
            <span className="text-xs text-neutral-500">{draft.length === 0 ? "All types" : draft.length + " chosen"}</span>
            <button type="button" className={primaryButtonClass + " ml-auto"} onClick={finish}>
              Apply
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
