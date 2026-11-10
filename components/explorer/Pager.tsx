"use client";
// Server-side paging: which rows of the result are on screen, page size,
// and first / previous / next / last page.
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from "lucide-react";
import { fmtInt } from "@/lib/format";
import { inputClass } from "@/components/ui";
import { PAGE_SIZES } from "./explorerShared";

type Props = {
  offset: number;
  limit: number;
  rowCount: number; // rows on this page
  total: number | null; // null = not counted (too many to count quickly)
  onOffset: (offset: number) => void;
  onLimit: (limit: number) => void;
};

const iconButton =
  "inline-flex h-8 w-8 items-center justify-center rounded-md border border-neutral-200 bg-white text-neutral-600 hover:bg-neutral-50 disabled:cursor-not-allowed disabled:opacity-40";

export default function Pager({ offset, limit, rowCount, total, onOffset, onLimit }: Props) {
  const first = rowCount === 0 ? 0 : offset + 1;
  const last = offset + rowCount;
  const hasNext = total === null ? rowCount === limit : last < total;
  const lastOffset = total === null ? null : Math.max(0, Math.floor((total - 1) / limit) * limit);
  const page = Math.floor(offset / limit) + 1;
  const pages = total === null ? null : Math.max(1, Math.ceil(total / limit));

  return (
    <div className="flex flex-wrap items-center gap-3 text-sm text-neutral-600">
      <span className="tabular-nums">
        Rows {fmtInt(first)}–{fmtInt(last)} of {total === null ? "many" : fmtInt(total)}
      </span>
      <label className="flex items-center gap-1.5">
        <span className="text-neutral-500">Page size</span>
        <select value={limit} onChange={(event) => onLimit(Number(event.target.value))} className={inputClass}>
          {PAGE_SIZES.map((size) => (
            <option key={size} value={size}>
              {fmtInt(size)}
            </option>
          ))}
        </select>
      </label>
      <div className="flex items-center gap-1">
        <button type="button" className={iconButton} onClick={() => onOffset(0)} disabled={offset === 0} aria-label="First page">
          <ChevronsLeft className="h-4 w-4" />
        </button>
        <button
          type="button"
          className={iconButton}
          onClick={() => onOffset(Math.max(0, offset - limit))}
          disabled={offset === 0}
          aria-label="Previous page"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        <span className="px-2 tabular-nums">
          Page {fmtInt(page)}
          {pages === null ? "" : " of " + fmtInt(pages)}
        </span>
        <button type="button" className={iconButton} onClick={() => onOffset(offset + limit)} disabled={!hasNext} aria-label="Next page">
          <ChevronRight className="h-4 w-4" />
        </button>
        <button
          type="button"
          className={iconButton}
          onClick={() => lastOffset !== null && onOffset(lastOffset)}
          disabled={lastOffset === null || offset >= lastOffset}
          aria-label="Last page"
        >
          <ChevronsRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
