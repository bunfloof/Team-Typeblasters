"use client";
// The SQL console's left column: the database's tables and columns, and the
// fields inside `data` for each event type (from the event catalog). A click
// inserts the name (or the JSON expression) at the editor's cursor.
import { useState } from "react";
import { ChevronDown, ChevronRight, KeyRound, Table2 } from "lucide-react";
import { ENVELOPE_FIELDS, EVENT_COLUMNS, EVENTS, PLAYER_STATE_FIELDS, fieldsOf } from "@/lib/catalog";
import { fmtInt } from "@/lib/format";
import { globalParams, useGlobalFilters } from "@/lib/globalFilters";
import { apiUrl, useApi } from "@/lib/useApi";
import type { MetaResponse } from "@/lib/types";
import { Spinner } from "@/components/ui";
import type { SchemaResponse, SchemaTable } from "./types";

type Props = { onInsert: (text: string) => void };

export default function SchemaSidebar({ onInsert }: Props) {
  return (
    <div className="flex flex-col gap-4">
      <TablesPanel onInsert={onInsert} />
      <EventFieldsPanel onInsert={onInsert} />
    </div>
  );
}

function PanelTitle({ children, note }: { children: string; note?: string }) {
  return (
    <div className="mb-2 flex items-baseline justify-between gap-2 px-1">
      <h2 className="text-[13px] font-semibold text-neutral-900">{children}</h2>
      {note ? <span className="truncate text-[11px] text-neutral-500">{note}</span> : null}
    </div>
  );
}

// --- tables ---------------------------------------------------------------

function TablesPanel({ onInsert }: Props) {
  const schema = useApi<SchemaResponse>("/api/sql/schema");
  const [open, setOpen] = useState<Record<string, boolean>>({ events: true });

  return (
    <section className="rounded-xl border border-black/10 bg-[#fcfcfb] p-3">
      <PanelTitle note={schema.data ? shortVersion(schema.data.version) : undefined}>Tables</PanelTitle>
      {schema.error && !schema.data ? (
        <p className="px-1 text-xs text-red-700">{schema.error}</p>
      ) : !schema.data ? (
        <Spinner label="Reading the schema" />
      ) : (
        <div className="flex flex-col gap-1">
          {schema.data.tables.map((table) => (
            <TableTree
              key={table.name}
              table={table}
              open={!!open[table.name]}
              onToggle={() => setOpen((state) => ({ ...state, [table.name]: !state[table.name] }))}
              onInsert={onInsert}
            />
          ))}
          <p className="mt-1 px-1 text-[11px] leading-snug text-neutral-500">
            Click a name to insert it. <KeyRound className="inline h-3 w-3" /> = indexed: filtering on it is fast.
          </p>
        </div>
      )}
    </section>
  );
}

// "8.0.36" or "10.11.6-MariaDB-log" -> "MySQL 8.0.36" / "MariaDB 10.11.6"
function shortVersion(version: string): string {
  const number = version.match(/^[\d.]+/)?.[0] || version;
  return (/mariadb/i.test(version) ? "MariaDB " : "MySQL ") + number;
}

function TableTree({
  table,
  open,
  onToggle,
  onInsert,
}: {
  table: SchemaTable;
  open: boolean;
  onToggle: () => void;
  onInsert: (text: string) => void;
}) {
  const Chevron = open ? ChevronDown : ChevronRight;
  return (
    <div>
      <div className="flex items-center gap-1">
        <button
          type="button"
          onClick={onToggle}
          aria-expanded={open}
          aria-label={(open ? "Hide" : "Show") + " columns of " + table.name}
          className="rounded p-0.5 text-neutral-400 hover:bg-black/5 hover:text-neutral-700"
        >
          <Chevron className="h-3.5 w-3.5" />
        </button>
        <button
          type="button"
          onClick={() => onInsert(table.name)}
          title={"Insert " + table.name}
          className="flex min-w-0 flex-1 items-center gap-1.5 rounded px-1 py-0.5 text-left hover:bg-black/5"
        >
          <Table2 className="h-3.5 w-3.5 shrink-0 text-neutral-400" />
          <span className="truncate font-mono text-[12.5px] font-medium text-neutral-800">{table.name}</span>
          <span className="ml-auto shrink-0 text-[11px] tabular-nums text-neutral-500">
            {table.rows !== null ? fmtInt(table.rows) + " rows" : table.kind === "VIEW" ? "view" : ""}
          </span>
        </button>
      </div>
      {open ? (
        <ul className="mb-1 ml-[22px] border-l border-neutral-200 pl-1">
          {table.columns.map((column) => {
            const doc = table.name === "events" ? EVENT_COLUMNS[column.name] : undefined;
            const tip = [
              column.name + " " + column.type + (column.nullable ? "" : " NOT NULL"),
              column.comment || doc,
              column.key === "PRI" ? "Primary key." : column.indexed ? "Indexed: filtering on it is fast." : "",
            ]
              .filter(Boolean)
              .join("\n");
            return (
              <li key={column.name}>
                <button
                  type="button"
                  onClick={() => onInsert(column.name)}
                  title={tip}
                  className="flex w-full items-center gap-1.5 rounded px-1.5 py-[3px] text-left hover:bg-black/5"
                >
                  <span className="shrink-0 font-mono text-[12px] text-neutral-800">{column.name}</span>
                  {column.indexed ? <KeyRound className="h-3 w-3 shrink-0 text-neutral-400" aria-label="indexed" /> : null}
                  <span className="ml-auto min-w-0 truncate pl-1 font-mono text-[11px] text-neutral-400">{column.type}</span>
                </button>
              </li>
            );
          })}
          {table.indexes.length > 0 ? (
            <li
              className="px-1.5 pb-1 pt-1.5 text-[11px] leading-snug text-neutral-500"
              title={table.indexes.map((index) => index.name + (index.unique ? " (unique)" : "") + ": " + index.columns.join(", ")).join("\n")}
            >
              Indexes: {table.indexes.map((index) => index.columns.join(" + ")).join(" · ")}
            </li>
          ) : null}
        </ul>
      ) : null}
    </div>
  );
}

// --- event data fields --------------------------------------------------------

// Keys of `data` that are also table columns (no need to list them twice).
const COLUMN_NAMES = new Set(Object.keys(EVENT_COLUMNS));
const UNDOCUMENTED = "In the data, not in the catalog";

function jsonExpression(name: string): string {
  return "JSON_UNQUOTE(JSON_EXTRACT(data, '$." + name + "'))";
}

function EventFieldsPanel({ onInsert }: Props) {
  const filters = useGlobalFilters();
  const meta = useApi<MetaResponse>(apiUrl("/api/meta", globalParams(filters)));
  const [eventType, setEventType] = useState("word");

  const counts = new Map((meta.data?.eventCounts || []).map((row) => [row.event, row.count]));
  // Every catalog event, plus any type the database has that the catalog lacks.
  const types = [...new Set([...Object.keys(EVENTS), ...counts.keys()])].sort();
  const doc = EVENTS[eventType];
  const seen = meta.data?.dataFields[eventType];
  const seenSet = new Set(seen || []);
  const own = doc ? doc.fields : {};
  const playerState = doc?.playerState ? PLAYER_STATE_FIELDS : {};
  const documented = new Set(Object.keys(fieldsOf(eventType)).concat(Object.keys(ENVELOPE_FIELDS)));
  const undocumented = (seen || []).filter((name) => !documented.has(name) && !COLUMN_NAMES.has(name));
  // Only claim "not logged yet" when the database has rows of this type.
  const canTellMissing = (counts.get(eventType) || 0) > 0 && !!seen && seen.length > 0;

  const groups: { title: string; fields: [string, string][] }[] = [
    { title: "Fields", fields: Object.entries(own) },
    { title: "Player state", fields: Object.entries(playerState) },
    { title: "On every event", fields: Object.entries(ENVELOPE_FIELDS) },
    { title: UNDOCUMENTED, fields: undocumented.map((name): [string, string] => [name, ""]) },
  ].filter((group) => group.fields.length > 0);

  return (
    <section className="rounded-xl border border-black/10 bg-[#fcfcfb] p-3">
      <PanelTitle>Event data fields</PanelTitle>
      <div className="flex items-center gap-1.5 px-1">
        <select
          value={eventType}
          onChange={(event) => setEventType(event.target.value)}
          aria-label="Event type"
          className="h-8 min-w-0 flex-1 rounded-md border border-neutral-200 bg-white px-2 font-mono text-[12.5px] text-neutral-800 outline-none focus:border-blue-400"
        >
          {types.map((type) => (
            <option key={type} value={type}>
              {type}
              {counts.has(type) ? " (" + fmtInt(counts.get(type) || 0) + ")" : ""}
            </option>
          ))}
        </select>
        <button
          type="button"
          onClick={() => onInsert("event = '" + eventType + "'")}
          title={"Insert event = '" + eventType + "'"}
          className="h-8 shrink-0 rounded-md border border-neutral-200 bg-white px-2 font-mono text-[11.5px] text-neutral-700 hover:bg-neutral-50"
        >
          event =
        </button>
      </div>
      {doc ? (
        <p className="mt-2 px-1 text-[12px] leading-snug text-neutral-600">
          <span className="font-medium text-neutral-800">{doc.title}.</span> {doc.when}
        </p>
      ) : null}
      <p className="mt-1 px-1 text-[11px] leading-snug text-neutral-500">
        Click a field to insert <span className="font-mono">JSON_UNQUOTE(JSON_EXTRACT(data, &apos;$.field&apos;))</span>.
      </p>
      {groups.map((group) => (
        <div key={group.title} className="mt-3">
          <div className="mb-1 px-1 text-[11px] font-medium uppercase tracking-wide text-neutral-500">{group.title}</div>
          <ul>
            {group.fields.map(([name, text]) => (
              <li key={name}>
                <button
                  type="button"
                  onClick={() => onInsert(jsonExpression(name))}
                  title={"Insert " + jsonExpression(name)}
                  className="w-full rounded px-1.5 py-1 text-left hover:bg-black/5"
                >
                  <span className="flex items-center gap-1.5">
                    <span className="font-mono text-[12px] text-neutral-800">{name}</span>
                    {canTellMissing && !seenSet.has(name) && group.title !== UNDOCUMENTED ? (
                      <span className="rounded border border-neutral-200 px-1 text-[10px] text-neutral-500" title="Not found in the latest rows: the game may not log it yet.">
                        not in data yet
                      </span>
                    ) : null}
                  </span>
                  {text ? <span className="mt-0.5 block text-[11.5px] leading-snug text-neutral-500">{text}</span> : null}
                </button>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}
