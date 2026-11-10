"use client";
// A data table (AG Grid Community): sort, filter, resize and reorder columns,
// select rows (checkboxes, shift-click), quick search, and CSV export of the
// selected rows or of every loaded row.
//   <DataGrid rows={rows} fileName="deaths" />                 (columns from the rows)
//   <DataGrid rows={rows} columns={["id", "event", ...]} />   (column order)
//   <DataGrid rows={rows} columnDefs={[...]} />                (full AG Grid ColDefs)
// getRowId: a stable id per row, so ticks survive a refresh of the same rows
// (pass a function defined outside the component: AG Grid reads it once).
// toolbarExtra: more buttons on the toolbar (e.g. a server-side "export all").
import { useCallback, useMemo, useRef, useState, type ReactNode } from "react";
import { AgGridReact } from "ag-grid-react";
import {
  AllCommunityModule,
  ModuleRegistry,
  themeQuartz,
  type ColDef,
  type FirstDataRenderedEvent,
  type GetRowIdParams,
  type RowDataUpdatedEvent,
  type RowClickedEvent,
  type SelectionChangedEvent,
  type ValueFormatterParams,
} from "ag-grid-community";
import { Download, Search } from "lucide-react";
import { INK } from "@/lib/theme";
import { columnsOf } from "@/lib/csv";

ModuleRegistry.registerModules([AllCommunityModule]);

const gridTheme = themeQuartz.withParams({
  accentColor: "#2a78d6",
  backgroundColor: INK.surface,
  foregroundColor: INK.primary,
  borderColor: "#e6e5df",
  headerBackgroundColor: "#f3f2ee",
  headerTextColor: INK.secondary,
  headerFontWeight: 600,
  fontFamily: "var(--font-geist-sans), system-ui, sans-serif",
  fontSize: 12.5,
  spacing: 6,
  wrapperBorderRadius: 10,
  oddRowBackgroundColor: "#f8f8f5",
});

type Row = Record<string, unknown>;

const NO_PINNED: string[] = [];

type Props = {
  rows: Row[];
  columns?: string[];
  columnDefs?: ColDef[];
  height?: number | string;
  fileName?: string;
  onRowClicked?: (row: Row) => void;
  onSelectionChanged?: (rows: Row[]) => void;
  toolbarExtra?: ReactNode;
  title?: ReactNode;
  loading?: boolean;
  pinnedLeft?: string[];
  getRowId?: (row: Row) => string;
};

// Objects (JSON) are shown as compact JSON text.
function formatValue(params: ValueFormatterParams): string {
  const value = params.value;
  if (value === null || value === undefined) {
    return "";
  }
  if (typeof value === "object") {
    return JSON.stringify(value);
  }
  return String(value);
}

export default function DataGrid({
  rows,
  columns,
  columnDefs,
  height = 560,
  fileName = "export",
  onRowClicked,
  onSelectionChanged,
  toolbarExtra,
  title,
  loading,
  pinnedLeft = NO_PINNED,
  getRowId,
}: Props) {
  const gridRef = useRef<AgGridReact<Row>>(null);
  const [quickFilter, setQuickFilter] = useState("");
  const [selectedCount, setSelectedCount] = useState(0);
  const [shownCount, setShownCount] = useState(rows.length);

  const defs = useMemo<ColDef[]>(() => {
    if (columnDefs) {
      return columnDefs;
    }
    const sample = rows.slice(0, 500);
    const names = columns || columnsOf(sample);
    return names.map((name) => ({
      field: name,
      headerName: name,
      pinned: pinnedLeft.includes(name) ? ("left" as const) : undefined,
      // AG Grid draws true/false columns as greyed-out checkboxes (and empty
      // cells as a dash); show them as plain text instead.
      cellDataType: sample.some((row) => typeof row[name] === "boolean") ? false : undefined,
      valueFormatter: formatValue,
      tooltipValueGetter: (params) => formatValue(params as unknown as ValueFormatterParams),
    }));
  }, [columnDefs, columns, rows, pinnedLeft]);

  const defaultColDef = useMemo<ColDef>(
    () => ({ sortable: true, filter: true, resizable: true, minWidth: 80, maxWidth: 520 }),
    [],
  );

  const rowId = useMemo(() => (getRowId ? (params: GetRowIdParams<Row>) => getRowId(params.data) : undefined), [getRowId]);

  // Size the columns to their content once the first rows are drawn (and
  // again when a new set of rows arrives).
  const autoSize = useCallback((event: FirstDataRenderedEvent | RowDataUpdatedEvent) => {
    event.api.autoSizeAllColumns();
  }, []);

  const exportCsv = (onlySelected: boolean) => {
    gridRef.current?.api.exportDataAsCsv({
      fileName: fileName + (onlySelected ? "-selected" : "") + ".csv",
      onlySelected,
      // every ticked row, also ones the search box or a column filter now hides
      onlySelectedAllPages: onlySelected,
      processCellCallback: (params) => {
        const value = params.value;
        return value !== null && typeof value === "object" ? JSON.stringify(value) : (value ?? "");
      },
    });
  };

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        {title ? <div className="mr-2 text-sm font-semibold text-neutral-800">{title}</div> : null}
        <label className="relative">
          <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-neutral-400" />
          <input
            value={quickFilter}
            onChange={(event) => setQuickFilter(event.target.value)}
            placeholder="Search these rows"
            className="h-8 w-56 rounded-md border border-neutral-200 bg-white pl-7 pr-2 text-sm outline-none focus:border-blue-400"
          />
        </label>
        <span className="text-xs tabular-nums text-neutral-500">
          {shownCount.toLocaleString()} of {rows.length.toLocaleString()} rows
          {selectedCount > 0 ? " · " + selectedCount.toLocaleString() + " selected" : ""}
        </span>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          {toolbarExtra}
          <button
            type="button"
            onClick={() => exportCsv(true)}
            disabled={selectedCount === 0}
            className="inline-flex h-8 items-center gap-1.5 rounded-md border border-neutral-200 bg-white px-3 text-sm text-neutral-700 hover:bg-neutral-50 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <Download className="h-3.5 w-3.5" /> Selected CSV
          </button>
          <button
            type="button"
            onClick={() => exportCsv(false)}
            disabled={rows.length === 0}
            className="inline-flex h-8 items-center gap-1.5 rounded-md border border-neutral-200 bg-white px-3 text-sm text-neutral-700 hover:bg-neutral-50 disabled:cursor-not-allowed disabled:opacity-40"
          >
            <Download className="h-3.5 w-3.5" /> These rows CSV
          </button>
        </div>
      </div>
      <div style={{ height }}>
        <AgGridReact<Row>
          ref={gridRef}
          theme={gridTheme}
          rowData={rows}
          columnDefs={defs}
          defaultColDef={defaultColDef}
          quickFilterText={quickFilter}
          suppressFieldDotNotation
          maintainColumnOrder
          getRowId={rowId}
          rowSelection={{ mode: "multiRow", enableClickSelection: false }}
          loading={loading}
          tooltipShowDelay={400}
          enableCellTextSelection
          selectionColumnDef={{ pinned: "left" }}
          onFirstDataRendered={autoSize}
          onRowDataUpdated={autoSize}
          onModelUpdated={(event) => setShownCount(event.api.getDisplayedRowCount())}
          onSelectionChanged={(event: SelectionChangedEvent<Row>) => {
            const selected = event.api.getSelectedRows();
            setSelectedCount(selected.length);
            onSelectionChanged?.(selected);
          }}
          onRowClicked={(event: RowClickedEvent<Row>) => {
            if (event.data && onRowClicked) {
              onRowClicked(event.data);
            }
          }}
        />
      </div>
    </div>
  );
}
