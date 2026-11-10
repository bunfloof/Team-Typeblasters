"use client";
// The result of the last query: row count and time, the rows in a DataGrid
// (sort, filter, select, CSV of selected / visible rows), and a server-side
// "Download full result as CSV" for results larger than what is shown.
import { useMemo } from "react";
import { CircleCheck, Download, X } from "lucide-react";
import DataGrid from "@/components/DataGrid";
import { EmptyState, ErrorNote, Hold, buttonClass } from "@/components/ui";
import { fmtInt } from "@/lib/format";
import { STATUS } from "@/lib/theme";
import { buildColumnDefs, duplicateNames } from "./columns";
import type { SqlErrorBody, SqlRunResponse } from "./types";
import type { ExportState } from "./useFullExport";

export type RunOutcome = {
  id: number;
  sql: string;
  result: SqlRunResponse | null;
  error: SqlErrorBody | null;
};

type Props = {
  outcome: RunOutcome | null;
  running: boolean;
  onCancel: () => void;
  includeTest: boolean;
  exportState: ExportState;
  onExport: (sql: string) => void;
  onCancelExport: () => void;
};

// A plain-English next step for the common errors.
export function errorHint(error: SqlErrorBody, sql: string) {
  if (error.config) {
    return "The database password is not set: fill DB_PASSWORD in .env.local, then restart the dashboard (pnpm dev, or pnpm start on the server).";
  }
  if (error.readOnly) {
    return "This console only reads: one SELECT, WITH, SHOW, DESCRIBE or EXPLAIN statement at a time.";
  }
  if (error.code === "ER_CANT_EXECUTE_IN_READ_ONLY_TRANSACTION") {
    return "The database refused a change: every query here runs inside a read-only transaction.";
  }
  if (error.code === "PROTOCOL_SEQUENCE_TIMEOUT") {
    return "The query ran longer than 60 seconds. Narrow it with WHERE on an indexed column (event, run_id, install_id, session_id, event_id) or add a LIMIT.";
  }
  if (error.code === "ER_PARSE_ERROR" && sql.includes("->")) {
    return "Write JSON_UNQUOTE(JSON_EXTRACT(data, '$.field')) instead of data->>'$.field': the arrows only work on MySQL, not MariaDB.";
  }
  return undefined;
}

// Grid height: fits a short result (header 41 px + 36 px a row + scrollbar), at most 520 px.
function gridHeight(rowCount: number): number {
  return Math.max(160, Math.min(520, 60 + rowCount * 36));
}

function fmtBytes(bytes: number): string {
  if (bytes < 1024) return bytes + " B";
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(0) + " KB";
  return (bytes / 1024 / 1024).toFixed(1) + " MB";
}

function ExportControls({ sql, state, onExport, onCancel }: { sql: string; state: ExportState; onExport: (sql: string) => void; onCancel: () => void }) {
  if (state.status === "downloading") {
    return (
      <span className="inline-flex items-center gap-2 text-xs text-neutral-600">
        <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-neutral-300 border-t-blue-500" />
        <span className="tabular-nums">Downloading… {fmtBytes(state.bytes)}</span>
        <button type="button" onClick={onCancel} className={buttonClass}>
          <X className="h-3.5 w-3.5" /> Cancel
        </button>
      </span>
    );
  }
  return (
    <>
      {state.status === "done" && state.sql === sql ? (
        <span className="text-xs text-neutral-500" title={state.fileName}>
          Saved {fmtBytes(state.bytes)}
        </span>
      ) : null}
      <button type="button" onClick={() => onExport(sql)} className={buttonClass} title="Runs the query again on the server and downloads every row (no row limit)">
        <Download className="h-3.5 w-3.5" /> Download full result as CSV
      </button>
    </>
  );
}

export default function ResultPanel({ outcome, running, onCancel, includeTest, exportState, onExport, onCancelExport }: Props) {
  const result = outcome?.result ?? null;
  const columnDefs = useMemo(() => (result ? buildColumnDefs(result.columns) : []), [result]);
  const duplicates = useMemo(() => (result ? duplicateNames(result.columns) : []), [result]);

  return (
    <section className="rounded-xl border border-black/10 bg-[#fcfcfb] p-4 shadow-[0_1px_2px_rgba(0,0,0,0.03)]">
      <div className="mb-3 flex min-h-8 flex-wrap items-center gap-x-3 gap-y-1">
        <h2 className="text-[15px] font-semibold text-neutral-900">Result</h2>
        {running ? (
          <span className="inline-flex items-center gap-2 text-sm text-neutral-600">
            <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-neutral-300 border-t-blue-500" />
            Running…
            <button type="button" onClick={onCancel} className={buttonClass}>
              <X className="h-3.5 w-3.5" /> Stop waiting
            </button>
          </span>
        ) : result ? (
          <span className="inline-flex items-center gap-1.5 text-sm text-neutral-600">
            <CircleCheck className="h-4 w-4" style={{ color: STATUS.good }} aria-hidden />
            <span className="tabular-nums">
              {fmtInt(result.rowCount)}
              {result.truncated ? "+" : ""} {result.rowCount === 1 && !result.truncated ? "row" : "rows"} · {fmtInt(result.ms)} ms
            </span>
          </span>
        ) : null}
        {result?.truncated ? (
          <span className="rounded-md bg-amber-50 px-2 py-0.5 text-xs text-amber-900">
            Showing the first {fmtInt(result.rows.length)} rows: the query returns more. Download the full result for every row.
          </span>
        ) : null}
      </div>

      {exportState.status === "error" && outcome && exportState.sql === outcome.sql ? (
        <div className="mb-3">
          <ErrorNote error={exportState.error.error} hint={errorHint(exportState.error, exportState.sql)} />
        </div>
      ) : null}

      <Hold loading={running}>
        {!outcome ? (
          running ? (
            <div className="h-24" />
          ) : (
            <EmptyState>Run a query to see its rows here, or pick one from Examples.</EmptyState>
          )
        ) : outcome.error ? (
          <ErrorNote
            error={(outcome.error.code ? outcome.error.code + (outcome.error.errno ? " (" + outcome.error.errno + ")" : "") + ": " : "") + outcome.error.error}
            hint={errorHint(outcome.error, outcome.sql)}
          />
        ) : result ? (
          <div className="flex flex-col gap-2">
            {duplicates.length > 0 ? (
              <p className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-900">
                More than one column is named {duplicates.map((name) => '"' + name + '"').join(", ")}: the table shows the last one in each place.
                Give them different names with AS (the full CSV download keeps both).
              </p>
            ) : null}
            {result.rowCount === 0 && !includeTest && outcome.sql.includes("qa-") ? (
              <p className="rounded-md bg-neutral-100 px-3 py-2 text-xs text-neutral-700">
                No rows. This query hides the team&apos;s test plays (install_id &apos;qa-...&apos;): tick &ldquo;Include test plays&rdquo; in the top
                bar and pick the example again, or delete that line.
              </p>
            ) : null}
            <DataGrid
              key={outcome.id}
              rows={result.rows}
              columnDefs={columnDefs}
              height={gridHeight(result.rows.length)}
              fileName="typeblasters-sql"
              toolbarExtra={<ExportControls sql={outcome.sql} state={exportState} onExport={onExport} onCancel={onCancelExport} />}
            />
          </div>
        ) : null}
      </Hold>
    </section>
  );
}
