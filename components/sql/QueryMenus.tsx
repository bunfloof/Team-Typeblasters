"use client";
// The Examples and History menus of the SQL console.
import { BookOpen, History, Trash2 } from "lucide-react";
import { levelName } from "@/lib/catalog";
import { rangeLabel } from "@/lib/globalFilters";
import { fmtInt } from "@/lib/format";
import Popover from "./Popover";
import { EXAMPLES, type ExampleFilters, type SqlExample } from "./examples";
import { clearHistory, useSqlHistory, type HistoryEntry } from "./sqlStorage";

export function ExamplesMenu({ filters, onPick }: { filters: ExampleFilters; onPick: (sql: string) => void }) {
  const groups = [...new Set(EXAMPLES.map((example) => example.group))];
  const following =
    (filters.includeTest ? "test plays included" : "test plays hidden") +
    " · " +
    (filters.level ? levelName(filters.level) + " level" : "all levels") +
    (filters.from || filters.to ? " · " + rangeLabel({ from: filters.from ?? "", to: filters.to ?? "" }) : "");

  return (
    <Popover
      label={
        <>
          <BookOpen className="h-3.5 w-3.5" /> Examples
        </>
      }
      title="Ready-made queries"
      width={480}
    >
      {(close) => (
        <div>
          <p className="px-2.5 pb-1 pt-1.5 text-[11.5px] leading-snug text-neutral-500">
            Click to load and run. They follow the top bar ({following}) and count each event_id once.
          </p>
          {groups.map((group) => (
            <div key={group} className="mt-1">
              <div className="px-2.5 py-1 text-[11px] font-medium uppercase tracking-wide text-neutral-500">{group}</div>
              {EXAMPLES.filter((example) => example.group === group).map((example: SqlExample) => (
                <button
                  key={example.id}
                  type="button"
                  onClick={() => {
                    close();
                    onPick(example.sql(filters));
                  }}
                  className="block w-full rounded-md px-2.5 py-1.5 text-left hover:bg-[#2a78d6]/[0.07]"
                >
                  <span className="block text-[13px] font-medium text-neutral-900">{example.title}</span>
                  <span className="block text-[12px] leading-snug text-neutral-500">{example.description}</span>
                </button>
              ))}
            </div>
          ))}
        </div>
      )}
    </Popover>
  );
}

// The first line worth showing: the first comment, else the first line of SQL.
function headline(sql: string): string {
  const lines = sql.split("\n").map((line) => line.trim()).filter(Boolean);
  const comment = lines.find((line) => line.startsWith("--"));
  return (comment ? comment.replace(/^--\s*/, "") : lines[0] || "").slice(0, 120);
}

function summary(entry: HistoryEntry): string {
  const time = new Date(entry.at).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  if (entry.error) return time + " · failed";
  return time + " · " + fmtInt(entry.rowCount) + (entry.rowCount === 1 ? " row" : " rows") + " · " + fmtInt(entry.ms) + " ms";
}

export function HistoryMenu({ onPick }: { onPick: (sql: string) => void }) {
  const history = useSqlHistory();

  return (
    <Popover
      label={
        <>
          <History className="h-3.5 w-3.5" /> History
          {history.length > 0 ? <span className="text-xs tabular-nums text-neutral-400">{history.length}</span> : null}
        </>
      }
      title="The last 30 queries run in this browser"
      width={480}
    >
      {(close) =>
        history.length === 0 ? (
          <p className="px-2.5 py-3 text-sm text-neutral-500">Queries you run appear here (kept in this browser, last 30).</p>
        ) : (
          <div>
            <ul>
              {history.map((entry) => (
                <li key={entry.at + entry.sql}>
                  <button
                    type="button"
                    onClick={() => {
                      close();
                      onPick(entry.sql);
                    }}
                    title={entry.sql}
                    className="block w-full rounded-md px-2.5 py-1.5 text-left hover:bg-[#2a78d6]/[0.07]"
                  >
                    <span className="block truncate font-mono text-[12px] text-neutral-900">{headline(entry.sql)}</span>
                    <span className={"block text-[11.5px] tabular-nums " + (entry.error ? "text-red-700" : "text-neutral-500")}>
                      {summary(entry)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
            <div className="mt-1 border-t border-neutral-100 px-1 pt-1">
              <button
                type="button"
                onClick={() => {
                  clearHistory();
                  close();
                }}
                className="inline-flex items-center gap-1.5 rounded-md px-1.5 py-1 text-xs text-neutral-500 hover:bg-black/5 hover:text-neutral-800"
              >
                <Trash2 className="h-3.5 w-3.5" /> Clear history
              </button>
            </div>
          </div>
        )
      }
    </Popover>
  );
}
