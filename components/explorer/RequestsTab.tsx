"use client";
// The Requests tab: one row per HTTP request the log server received, with
// the sender's IP and browser (shown as stored, nothing hidden).
import { useMemo, useState } from "react";
import type { GlobalFilters } from "@/lib/globalFilters";
import { REQUEST_COLUMNS } from "@/lib/types";
import { apiUrl, useApi } from "@/lib/useApi";
import RequestFilterBar from "./RequestFilterBar";
import ResultsPanel, { ExportAllLink, SortControl, buildColumnDefs } from "./ResultsPanel";
import { parseView, type RowsResponse } from "./explorerShared";
import { EMPTY_REQUEST_DRAFT, requestApiParams, requestDraftFromUrl, requestDraftPatch, type UrlPatch } from "./explorerUrl";

// Header tooltips for the columns that are not self-explanatory.
const REQUEST_DOCS: Record<string, string> = {
  received_at: "When the server received it (UTC).",
  client_ip: "Best guess of the player's IP: CF-Connecting-IP, else X-Real-IP, else the first X-Forwarded-For address, else peer_ip.",
  peer_ip: "The address that opened the connection (nginx, or a direct visitor).",
  cf_connecting_ip: "CF-Connecting-IP header (the visitor's IP, set by Cloudflare).",
  x_real_ip: "X-Real-IP header (set by nginx).",
  x_forwarded_for: "X-Forwarded-For header (proxy chain).",
  cf_ray: "Cloudflare request id.",
  cf_ipcountry: "Country Cloudflare located the IP in.",
  user_agent: "The browser's User-Agent.",
  origin: "Origin header: the site the game was played on.",
  referer: "Referer header: the page that sent the request.",
  sec_ch_ua: "Browser brand and version (client hint).",
  sec_ch_ua_platform: "Operating system (client hint).",
  sec_ch_ua_mobile: "?1 = mobile device (client hint).",
  body_bytes: "Size of the body in bytes.",
  body_encoding: "utf-8, or base64 when the body was not valid UTF-8.",
  parse_ok: "1 = a valid POST /v1/batch (its events were stored), 0 = rejected (see parse_error), empty = not a batch request.",
  parse_error: "Why the batch was rejected.",
  event_count: "Events stored from this request.",
  raw_id: "Id of the request in the log server's raw log file.",
  source: "live (received by the server) or replay (re-imported from the raw log).",
};

type Props = {
  page: URLSearchParams;
  global: GlobalFilters;
  onPatch: (patch: UrlPatch) => void;
  onOpenRow: (id: number) => void;
};

export default function RequestsTab({ page, global, onPatch, onOpenRow }: Props) {
  const view = parseView(page);
  const applied = requestDraftFromUrl(page);
  const [withBody, setWithBody] = useState(false);

  const result = useApi<RowsResponse>(apiUrl("/api/rows", requestApiParams(page, global)));
  const data = result.data?.table === "requests" ? result.data : null;

  const columnDefs = useMemo(
    () =>
      data
        ? buildColumnDefs(data.columns, data.rows, {
            header: (column) => column,
            doc: (column) => REQUEST_DOCS[column],
            onOpen: (row) => onOpenRow(Number(row.id)),
          })
        : [],
    [data, onOpenRow],
  );

  const exportParams = requestApiParams(page, global);
  exportParams.delete("offset");
  exportParams.delete("limit");
  if (withBody) exportParams.set("detail", "1");

  return (
    <div className="space-y-4">
      <RequestFilterBar
        key={JSON.stringify(applied)}
        applied={applied}
        levelIgnored={!!global.level}
        onApply={(draft) => onPatch(requestDraftPatch(draft))}
        onReset={() => onPatch(requestDraftPatch(EMPTY_REQUEST_DRAFT))}
      />
      <ResultsPanel
        result={result}
        columnDefs={columnDefs}
        fileName="typeblasters-requests"
        onOffset={(offset) => onPatch({ offset: offset > 0 ? String(offset) : null })}
        onLimit={(limit) => onPatch({ limit: String(limit) })}
        emptyHint={
          global.includeTest ? null : "Requests carrying test plays (install_id qa-…) are hidden: tick “Include test plays” in the top bar."
        }
        toolbar={
          <>
            <SortControl
              options={[...REQUEST_COLUMNS]}
              sort={view.sort}
              dir={view.dir}
              onChange={(sort, dir) => onPatch({ sort: sort === "id" ? null : sort, dir: dir === "desc" ? null : dir })}
            />
            <label className="flex items-center gap-1.5 text-xs text-neutral-600" title="Adds the headers JSON and the raw body to the CSV">
              <input
                type="checkbox"
                checked={withBody}
                onChange={(event) => setWithBody(event.target.checked)}
                className="h-3.5 w-3.5 accent-[#2a78d6]"
              />
              with headers & body
            </label>
            <ExportAllLink href={apiUrl("/api/export", exportParams)} total={data?.total ?? null} />
          </>
        }
      />
    </div>
  );
}
