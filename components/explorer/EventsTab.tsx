"use client";
// The Events tab: filter row, then one page of the events table with the
// chosen data fields flattened into columns.
import { useMemo } from "react";
import { EVENT_COLUMNS } from "@/lib/catalog";
import { fmtInt } from "@/lib/format";
import type { GlobalFilters } from "@/lib/globalFilters";
import { EVENT_TABLE_COLUMNS, type MetaResponse } from "@/lib/types";
import { apiUrl, useApi } from "@/lib/useApi";
import EventFilterBar from "./EventFilterBar";
import FieldPicker from "./FieldPicker";
import ResultsPanel, { ExportAllLink, SortControl, buildColumnDefs } from "./ResultsPanel";
import { DATA_PREFIX, parseView, type RowsResponse } from "./explorerShared";
import {
  EMPTY_EVENT_DRAFT,
  eventApiParams,
  eventDraftFromUrl,
  eventDraftPatch,
  fieldCandidates,
  fieldDoc,
  type UrlPatch,
} from "./explorerUrl";

type Props = {
  page: URLSearchParams; // the page URL's parameters
  global: GlobalFilters;
  meta: MetaResponse | null;
  onPatch: (patch: UrlPatch) => void;
  onOpenRow: (id: number) => void;
};

export default function EventsTab({ page, global, meta, onPatch, onOpenRow }: Props) {
  const view = parseView(page);
  const applied = eventDraftFromUrl(page);
  const appliedKey = JSON.stringify(applied);
  const types = applied.events;
  const typesKey = types.join(",");

  const result = useApi<RowsResponse>(apiUrl("/api/rows", eventApiParams(page, global)));
  const data = result.data?.table === "events" ? result.data : null;
  const fields = useMemo(() => data?.fields ?? [], [data]);
  const raw = data ? data.columns.includes("data") : false;

  const columnDefs = useMemo(
    () =>
      data
        ? buildColumnDefs(data.columns, data.rows, {
            header: (column) => (column.startsWith(DATA_PREFIX) ? column.slice(DATA_PREFIX.length) : column),
            doc: (column) => {
              if (column.startsWith(DATA_PREFIX)) {
                const key = column.slice(DATA_PREFIX.length);
                const doc = fieldDoc(key, typesKey ? typesKey.split(",") : []);
                return column + (doc ? ": " + doc : "");
              }
              return column === "data" ? "The whole data JSON of the event." : EVENT_COLUMNS[column];
            },
            onOpen: (row) => onOpenRow(Number(row.id)),
          })
        : [],
    [data, typesKey, onOpenRow],
  );

  const candidates = useMemo(() => fieldCandidates(typesKey ? typesKey.split(",") : [], meta, fields), [typesKey, meta, fields]);
  const sortOptions = [
    ...EVENT_TABLE_COLUMNS,
    ...fields.map((field) => DATA_PREFIX + field),
    ...(view.sort.startsWith(DATA_PREFIX) && !fields.includes(view.sort.slice(DATA_PREFIX.length)) ? [view.sort] : []),
  ];

  // "All matching rows" exports exactly the columns on screen.
  const exportParams = eventApiParams(page, global, { fields, raw });
  exportParams.delete("offset");
  exportParams.delete("limit");

  // Other columns, same rows: stay on this page.
  const keepOffset = view.offset > 0 ? String(view.offset) : null;
  const testHidden = !global.includeTest && (meta?.totals.testEvents ?? 0) > 0;

  return (
    <div className="space-y-4">
      <EventFilterBar
        key={appliedKey}
        applied={applied}
        meta={meta}
        onApply={(draft) => onPatch(eventDraftPatch(draft, applied, view.sort))}
        onReset={() => onPatch(eventDraftPatch(EMPTY_EVENT_DRAFT, applied, view.sort))}
      />
      <ResultsPanel
        result={result}
        columnDefs={columnDefs}
        fileName={"typeblasters-events" + (types.length === 1 ? "-" + types[0] : "")}
        onOffset={(offset) => onPatch({ offset: offset > 0 ? String(offset) : null })}
        onLimit={(limit) => onPatch({ limit: String(limit) })}
        emptyHint={
          testHidden ? (
            <>
              {fmtInt(meta?.totals.testEvents)} test events (install_id qa-…) are hidden: tick “Include test plays” in the top bar to
              see them.
            </>
          ) : global.level ? (
            "Only the level chosen in the top bar is shown."
          ) : null
        }
        toolbar={
          <>
            <SortControl
              options={sortOptions}
              sort={view.sort}
              dir={view.dir}
              onChange={(sort, dir) => onPatch({ sort: sort === "id" ? null : sort, dir: dir === "desc" ? null : dir })}
            />
            <FieldPicker
              candidates={candidates}
              selected={fields}
              raw={raw}
              onApply={(nextFields, nextRaw) => onPatch({ fields: nextFields.join(","), raw: nextRaw ? "1" : "0", offset: keepOffset })}
              onReset={() => onPatch({ fields: null, raw: null, offset: keepOffset })}
            />
            <ExportAllLink href={apiUrl("/api/export", exportParams)} total={data?.total ?? null} />
          </>
        }
      />
    </div>
  );
}
