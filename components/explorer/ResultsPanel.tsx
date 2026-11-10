"use client";
// One page of Explorer rows: the grid (with its selection and CSV buttons),
// the server-side pager under it, and the loading / empty / error states.
// Also the pieces both tabs put in the grid toolbar: sort and "export all".
import type { ReactNode } from "react";
import type { CellClickedEvent, ColDef, ValueFormatterParams } from "ag-grid-community";
import { ArrowDownUp, Download } from "lucide-react";
import DataGrid from "@/components/DataGrid";
import { EmptyState, ErrorNote, Hold, Spinner, buttonClass, inputClass } from "@/components/ui";
import { fmtInt } from "@/lib/format";
import type { ApiState } from "@/lib/useApi";
import Pager from "./Pager";
import type { RowsResponse } from "./explorerShared";

type Row = Record<string, unknown>;

type Props = {
  result: ApiState<RowsResponse>;
  columnDefs: ColDef[];
  fileName: string;
  toolbar: ReactNode;
  emptyHint?: ReactNode;
  onOffset: (offset: number) => void;
  onLimit: (limit: number) => void;
};

export default function ResultsPanel({ result, columnDefs, fileName, toolbar, emptyHint, onOffset, onLimit }: Props) {
  const data = result.data;

  if (result.error && !result.loading) {
    return (
      <ErrorNote
        error={result.error}
        hint={
          result.error.includes(".env.local")
            ? "Paste the database password into DB_PASSWORD in .env.local (in the dashboard folder), then restart the dashboard (pnpm dev, or pnpm start on the server)."
            : "Check the filters (a data condition with a text value and > or < compares numbers), or try again."
        }
      />
    );
  }
  if (!data) {
    return <Spinner label="Loading rows" />;
  }
  if (data.rows.length === 0) {
    return (
      <Hold loading={result.loading}>
        <EmptyState>
          {data.offset > 0 ? (
            <>
              No rows on this page.{" "}
              <button type="button" className="text-[#1c5cab] underline" onClick={() => onOffset(0)}>
                Go to the first page
              </button>
            </>
          ) : (
            <>
              No rows match these filters.
              {emptyHint ? <div className="mt-2 text-xs">{emptyHint}</div> : null}
            </>
          )}
        </EmptyState>
      </Hold>
    );
  }

  return (
    <div className="space-y-3">
      <Hold loading={result.loading}>
        <DataGrid
          rows={data.rows}
          columnDefs={columnDefs}
          height="max(440px, calc(100vh - 380px))"
          fileName={fileName}
          toolbarExtra={toolbar}
        />
      </Hold>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Pager
          offset={data.offset}
          limit={data.limit}
          rowCount={data.rows.length}
          total={data.total}
          onOffset={onOffset}
          onLimit={onLimit}
        />
        <span className="text-xs text-neutral-500">
          Click a row for every detail · header click sorts this page only · query {fmtInt(data.ms)} ms
        </span>
      </div>
    </div>
  );
}

// ---- Toolbar pieces ----

export function SortControl({
  options,
  sort,
  dir,
  onChange,
}: {
  options: string[];
  sort: string;
  dir: "asc" | "desc";
  onChange: (sort: string, dir: "asc" | "desc") => void;
}) {
  return (
    <div className="flex items-center gap-1" title="Sorts every matching row on the server">
      <span className="text-xs text-neutral-500">Sort by</span>
      <select value={sort} onChange={(event) => onChange(event.target.value, dir)} className={inputClass + " max-w-44 font-mono text-[12.5px]"}>
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
      <button
        type="button"
        className={buttonClass + " px-2"}
        onClick={() => onChange(sort, dir === "asc" ? "desc" : "asc")}
        aria-label={dir === "asc" ? "Ascending (click for descending)" : "Descending (click for ascending)"}
      >
        <ArrowDownUp className="h-3.5 w-3.5" />
        {dir === "asc" ? "Asc" : "Desc"}
      </button>
    </div>
  );
}

// A plain link: the browser downloads the streamed CSV itself.
export function ExportAllLink({ href, total }: { href: string; total: number | null }) {
  return (
    <a
      href={href}
      download
      className={buttonClass + " border-blue-200 text-[#1c5cab]"}
      title={
        "Every row matching these filters (" +
        (total === null ? "many" : fmtInt(total)) +
        " rows), streamed as CSV in id order, with the columns shown here"
      }
    >
      <Download className="h-3.5 w-3.5" /> All matching rows CSV
    </a>
  );
}

// ---- Columns ----

// Objects (JSON) as compact JSON text, everything else as text.
export function cellText(value: unknown): string {
  if (value === null || value === undefined) {
    return "";
  }
  return typeof value === "object" ? JSON.stringify(value) : String(value);
}

function formatCell(params: ValueFormatterParams): string {
  return cellText(params.value);
}

// Numbers only (at least one) -> "number"; anything else is filtered as text.
function isNumberColumn(rows: Row[], column: string): boolean {
  let numbers = 0;
  for (const row of rows) {
    const value = row[column];
    if (value === null || value === undefined) {
      continue;
    }
    if (typeof value !== "number") {
      return false;
    }
    numbers++;
  }
  return numbers > 0;
}

// Grid columns for the rows of one page. header/doc give the header text and
// its tooltip; a click on any cell opens the row (checkbox clicks do not).
export function buildColumnDefs(
  columns: string[],
  rows: Row[],
  options: { header: (column: string) => string; doc: (column: string) => string | undefined; onOpen: (row: Row) => void },
): ColDef[] {
  return columns.map((column) => {
    const numeric = isNumberColumn(rows, column);
    const def: ColDef = {
      field: column,
      headerName: options.header(column),
      headerTooltip: options.doc(column),
      valueFormatter: formatCell,
      onCellClicked: (event: CellClickedEvent) => {
        if (event.data) options.onOpen(event.data as Row);
      },
      cellStyle: { cursor: "pointer" },
    };
    if (numeric) {
      def.cellDataType = "number";
    } else {
      def.cellDataType = false;
      def.filter = "agTextColumnFilter";
      def.filterValueGetter = (params) => cellText(params.data?.[column]);
      def.getQuickFilterText = (params) => cellText(params.value);
      def.tooltip = true;
    }
    if (column === "id") {
      def.pinned = "left";
      def.width = 96;
    }
    return def;
  });
}
