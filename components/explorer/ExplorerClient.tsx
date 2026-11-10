"use client";
// The Explorer: every row of the events and requests tables, filtered,
// sorted and paged on the server, with row selection, CSV export and a
// detail drawer. All its state lives in the URL (copy the address to share
// a view); the top bar's global filters (test plays, level) apply on top.
import { useCallback, useState, type ReactNode } from "react";
import { useSearchParams } from "next/navigation";
import { Inbox, Table2 } from "lucide-react";
import { fmtCompact } from "@/lib/format";
import { globalParams, useGlobalFilters } from "@/lib/globalFilters";
import type { MetaResponse } from "@/lib/types";
import { apiUrl, useApi } from "@/lib/useApi";
import EventsTab from "./EventsTab";
import RequestsTab from "./RequestsTab";
import RowDrawer, { type DrawerTarget } from "./RowDrawer";
import { parseView, type ExplorerTable } from "./explorerShared";
import { applyPatch, type UrlPatch } from "./explorerUrl";

// Updates the address bar without a server round trip; useSearchParams()
// picks the change up (Next.js integrates pushState with its router).
function go(params: URLSearchParams) {
  const query = params.toString();
  window.history.pushState(null, "", query ? "?" + query : window.location.pathname);
}

export default function ExplorerClient() {
  const searchParams = useSearchParams();
  const global = useGlobalFilters();
  const meta = useApi<MetaResponse>(apiUrl("/api/meta", globalParams(global)));
  const table = parseView(searchParams).table;
  const [drawer, setDrawer] = useState<DrawerTarget | null>(null);

  // A filter change goes back to the first page unless the patch says otherwise.
  const patchUrl = (patch: UrlPatch) => go(applyPatch(searchParams, "offset" in patch ? patch : { ...patch, offset: null }));
  const showEvents = (filters: UrlPatch) => go(applyPatch(new URLSearchParams(), filters));
  const showRequests = (filters: UrlPatch) => go(applyPatch(new URLSearchParams({ table: "requests" }), filters));
  const switchTable = (next: ExplorerTable) => go(new URLSearchParams(next === "requests" ? { table: next } : {}));

  const openEvent = useCallback((id: number) => setDrawer({ table: "events", id }), []);
  const openRequest = useCallback((id: number) => setDrawer({ table: "requests", id }), []);

  return (
    <div className="space-y-4">
      <div role="tablist" aria-label="Table" className="inline-flex rounded-lg border border-black/10 bg-white p-0.5">
        <TabButton active={table === "events"} onClick={() => switchTable("events")} count={meta.data?.totals.events}>
          <Table2 className="h-4 w-4" /> Events
        </TabButton>
        <TabButton active={table === "requests"} onClick={() => switchTable("requests")} count={meta.data?.totals.requests}>
          <Inbox className="h-4 w-4" /> Requests
        </TabButton>
      </div>

      {table === "events" ? (
        <EventsTab page={searchParams} global={global} meta={meta.data} onPatch={patchUrl} onOpenRow={openEvent} />
      ) : (
        <RequestsTab page={searchParams} global={global} onPatch={patchUrl} onOpenRow={openRequest} />
      )}

      <RowDrawer
        target={drawer}
        onClose={() => setDrawer(null)}
        onOpen={setDrawer}
        onShowEvents={showEvents}
        onShowRequests={showRequests}
      />
    </div>
  );
}

function TabButton({
  active,
  onClick,
  count,
  children,
}: {
  active: boolean;
  onClick: () => void;
  count: number | undefined;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={
        "inline-flex h-8 items-center gap-2 rounded-md px-3 text-sm " +
        (active ? "bg-[#2a78d6]/10 font-medium text-[#1c5cab]" : "text-neutral-600 hover:bg-neutral-50")
      }
    >
      {children}
      {count !== undefined ? <span className="text-xs tabular-nums text-neutral-500">{fmtCompact(count)}</span> : null}
    </button>
  );
}
