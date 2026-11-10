"use client";
// The SQL console: editor + menus on top, the result below, the schema on
// the side. Runs go to POST /api/sql (read-only, one statement).
import { useCallback, useEffect, useRef, useState } from "react";
import { Check, Link2, Lock, Play } from "lucide-react";
import { levelName } from "@/lib/catalog";
import { rangeLabel, useGlobalFilters } from "@/lib/globalFilters";
import { copyText } from "@/lib/clipboard";
import { inputClass, primaryButtonClass, buttonClass } from "@/components/ui";
import SqlEditor, { type SqlEditorHandle } from "./SqlEditor";
import SchemaSidebar from "./SchemaSidebar";
import ResultPanel, { type RunOutcome } from "./ResultPanel";
import { ExamplesMenu, HistoryMenu } from "./QueryMenus";
import { addToHistory, readDraft, readMaxRows, writeDraft, writeMaxRows } from "./sqlStorage";
import { useFullExport } from "./useFullExport";
import type { SqlErrorBody, SqlRunResponse } from "./types";

const DEFAULT_SQL = "-- The latest events. Ctrl+Enter runs.\nSELECT *\nFROM events\nORDER BY id DESC\nLIMIT 100;";
const ROW_LIMITS = [1000, 10000, 50000, 200000];
const DEFAULT_MAX_ROWS = 50000;

type RunRequest = { id: number; sql: string; maxRows: number };

class RunFailure extends Error {
  body: SqlErrorBody;
  constructor(body: SqlErrorBody) {
    super(body.error);
    this.body = body;
  }
}

// The top-bar filters as a condition to paste into a WHERE ("" when none apply).
function filterCondition(includeTest: boolean, level: string, from: string, to: string): string {
  const parts: string[] = [];
  if (!includeTest) parts.push("install_id NOT LIKE 'qa-%'");
  if (level) parts.push("level = '" + level.replace(/'/g, "''") + "'");
  if (from) parts.push("received_at >= '" + from.replace(/'/g, "") + "'");
  if (to) parts.push("received_at <= '" + to.replace(/'/g, "") + "'");
  return parts.join(" AND ");
}

export default function SqlWorkbench({ initialSql }: { initialSql: string | null }) {
  const filters = useGlobalFilters();
  const editorRef = useRef<SqlEditorHandle>(null);
  const runCount = useRef(initialSql ? 1 : 0);
  const [sql, setSql] = useState(() => initialSql ?? readDraft() ?? DEFAULT_SQL);
  const [maxRows, setMaxRows] = useState(() => readMaxRows(DEFAULT_MAX_ROWS));
  const [hasSelection, setHasSelection] = useState(false);
  const [copied, setCopied] = useState(false);
  // A link to /sql?q=... runs its query as soon as the page opens.
  const [request, setRequest] = useState<RunRequest | null>(() =>
    initialSql ? { id: 1, sql: initialSql, maxRows: readMaxRows(DEFAULT_MAX_ROWS) } : null,
  );
  const [outcome, setOutcome] = useState<RunOutcome | null>(null);
  const exporter = useFullExport();

  const running = request !== null && outcome?.id !== request.id;

  // Sends the current request; a newer request (or Stop) aborts the older one.
  useEffect(() => {
    if (!request) return;
    const controller = new AbortController();
    fetch("/api/sql", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ sql: request.sql, maxRows: request.maxRows }),
      signal: controller.signal,
    })
      .then(async (response) => {
        const body = await response.json().catch(() => null);
        if (!response.ok) {
          throw new RunFailure(body?.error ? (body as SqlErrorBody) : { error: response.status + " " + response.statusText });
        }
        return body as SqlRunResponse;
      })
      .then((result) => {
        setOutcome({ id: request.id, sql: request.sql, result, error: null });
        addToHistory({ sql: request.sql, at: new Date().toISOString(), rowCount: result.rowCount, ms: result.ms, error: null });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const body: SqlErrorBody = error instanceof RunFailure ? error.body : { error: error instanceof Error ? error.message : String(error) };
        setOutcome({ id: request.id, sql: request.sql, result: null, error: body });
        addToHistory({ sql: request.sql, at: new Date().toISOString(), rowCount: null, ms: null, error: body.error });
      });
    return () => controller.abort();
  }, [request]);

  const onChange = useCallback((text: string) => {
    setSql(text);
    writeDraft(text);
  }, []);

  // Runs `text`, else the editor's selection, else the whole editor.
  const run = useCallback(
    (text?: string) => {
      const statement = text ?? (editorRef.current?.selectedText() || sql);
      if (!statement.trim()) return;
      runCount.current += 1;
      setRequest({ id: runCount.current, sql: statement, maxRows });
    },
    [sql, maxRows],
  );

  const loadAndRun = (text: string) => {
    editorRef.current?.replaceAll(text);
    run(text);
  };

  const copyLink = async () => {
    const link = window.location.origin + "/sql?q=" + encodeURIComponent(sql);
    if (await copyText(link)) {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } else {
      window.prompt("Copy this link:", link);
    }
  };

  const condition = filterCondition(filters.includeTest, filters.level, filters.from, filters.to);

  return (
    <div className="grid items-start gap-4 xl:grid-cols-[250px_minmax(0,1fr)]">
      <aside className="order-2 xl:sticky xl:top-[64px] xl:order-1 xl:max-h-[calc(100vh-80px)] xl:overflow-y-auto xl:pr-1">
        <SchemaSidebar onInsert={(text) => editorRef.current?.insert(text)} />
      </aside>

      <div className="order-1 flex min-w-0 flex-col gap-4 xl:order-2">
        <section className="rounded-xl border border-black/10 bg-[#fcfcfb] p-4 shadow-[0_1px_2px_rgba(0,0,0,0.03)]">
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => run()}
              disabled={running}
              className={primaryButtonClass}
              title="Ctrl+Enter (Cmd+Enter on a Mac). With text selected, runs only the selection."
            >
              <Play className="h-3.5 w-3.5" /> {hasSelection ? "Run selection" : "Run"}
            </button>
            <ExamplesMenu filters={filters} onPick={loadAndRun} />
            <HistoryMenu onPick={loadAndRun} />
            <div className="ml-auto flex flex-wrap items-center gap-2">
              <label className="flex items-center gap-1.5 text-sm text-neutral-600">
                Show up to
                <select
                  value={maxRows}
                  onChange={(event) => {
                    const value = Number(event.target.value);
                    setMaxRows(value);
                    writeMaxRows(value);
                  }}
                  className={inputClass}
                >
                  {ROW_LIMITS.map((limit) => (
                    <option key={limit} value={limit}>
                      {limit.toLocaleString("en-US")} rows
                    </option>
                  ))}
                </select>
              </label>
              <button type="button" onClick={copyLink} className={buttonClass} title="Copy a link that opens and runs this query">
                {copied ? <Check className="h-3.5 w-3.5" /> : <Link2 className="h-3.5 w-3.5" />}
                {copied ? "Copied" : "Copy link"}
              </button>
            </div>
          </div>

          <SqlEditor ref={editorRef} value={sql} onChange={onChange} onRun={() => run()} onSelectionChange={setHasSelection} />

          <div className="mt-2 flex flex-wrap items-start justify-between gap-x-4 gap-y-1 text-xs text-neutral-500">
            <p className="flex items-center gap-1.5 text-neutral-700">
              <Lock className="h-3.5 w-3.5 shrink-0 text-neutral-500" aria-hidden />
              Read-only: only SELECT / WITH / SHOW / DESCRIBE / EXPLAIN; runs inside a read-only transaction.
            </p>
            <p>Ctrl+Enter runs · Tab indents · Esc then Tab leaves the editor</p>
          </div>
          <p className="mt-1.5 text-xs leading-relaxed text-neutral-500">
            The top-bar filters ({filters.includeTest ? "test plays included" : "test plays hidden"},{" "}
            {filters.level ? levelName(filters.level) + " level" : "all levels"}, {filters.from || filters.to ? rangeLabel(filters) : "all time"}) do not
            change what you write; the examples follow them.
            {condition ? (
              <>
                {" "}
                <button
                  type="button"
                  onClick={() => editorRef.current?.insert(condition)}
                  className="font-mono text-[11.5px] text-[#1c5cab] underline decoration-[#1c5cab]/30 underline-offset-2 hover:decoration-[#1c5cab]"
                  title="Insert at the cursor"
                >
                  {condition}
                </button>
              </>
            ) : null}
          </p>
        </section>

        <ResultPanel
          outcome={outcome}
          running={running}
          onCancel={() => setRequest(null)}
          includeTest={filters.includeTest}
          exportState={exporter.state}
          onExport={exporter.start}
          onCancelExport={exporter.cancel}
        />
      </div>
    </div>
  );
}
