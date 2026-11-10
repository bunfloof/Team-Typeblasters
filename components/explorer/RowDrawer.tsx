"use client";
// The side drawer that opens on a row click: every column, the event's data
// (with what each field means) and the HTTP request it arrived in (IP,
// browser, headers, body), plus links to the run, session, player and request.
import { useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { Activity, Check, Copy, ExternalLink, Globe, Inbox, MonitorSmartphone, User, X } from "lucide-react";
import { EVENTS, EVENT_COLUMNS, describeField, fieldsOf, levelName } from "@/lib/catalog";
import { fmtInt, fmtTime } from "@/lib/format";
import { EVENT_TABLE_COLUMNS, REQUEST_COLUMNS } from "@/lib/types";
import { apiUrl, useApi } from "@/lib/useApi";
import { copyText } from "@/lib/clipboard";
import { Badge, ErrorNote, Hold, Spinner, buttonClass } from "@/components/ui";
import { EVENT_COLUMN_SET, type EventDetailResponse, type RequestDetailResponse } from "./explorerShared";
import type { UrlPatch } from "./explorerUrl";
import { useDismiss } from "./useDismiss";

export type DrawerTarget = { table: "events" | "requests"; id: number };

type Props = {
  target: DrawerTarget | null;
  onClose: () => void;
  onOpen: (target: DrawerTarget) => void;
  onShowEvents: (filters: UrlPatch) => void; // the events tab with only these filters
  onShowRequests: (filters: UrlPatch) => void; // the requests tab with only these filters
};

// The request fields people look at first; the rest follow in table order.
const REQUEST_FIRST = [
  "client_ip",
  "user_agent",
  "cf_ipcountry",
  "origin",
  "referer",
  "received_at",
  "method",
  "path",
  "status",
  "event_count",
];

export default function RowDrawer({ target, onClose, onOpen, onShowEvents, onShowRequests }: Props) {
  const panelRef = useRef<HTMLElement>(null);
  // Escape closes it; clicks outside do not (clicking another row swaps it,
  // and the previous row stays on screen, dimmed, until the new one loads).
  useDismiss(panelRef, target !== null, onClose, false);
  if (!target) {
    return null;
  }
  return (
    <aside
      ref={panelRef}
      className="fixed inset-y-0 right-0 z-40 flex w-[min(580px,100vw)] flex-col border-l border-black/10 bg-white shadow-[-12px_0_40px_rgba(0,0,0,0.08)]"
      aria-label="Row details"
    >
      {target.table === "events" ? (
        <EventDetail id={target.id} onClose={onClose} onOpen={onOpen} onShowEvents={onShowEvents} />
      ) : (
        <RequestDetail
          id={target.id}
          onClose={onClose}
          onShowEvents={onShowEvents}
          onShowRequests={onShowRequests}
        />
      )}
    </aside>
  );
}

// ---- Events ----

function EventDetail({
  id,
  onClose,
  onOpen,
  onShowEvents,
}: {
  id: number;
  onClose: () => void;
  onOpen: (target: DrawerTarget) => void;
  onShowEvents: (filters: UrlPatch) => void;
}) {
  const detail = useApi<EventDetailResponse>(apiUrl("/api/rows", { table: "events", id }));
  const row = detail.data?.row;
  const requestId = row && typeof row.request_id === "number" ? row.request_id : null;
  const request = useApi<RequestDetailResponse>(requestId !== null ? apiUrl("/api/rows", { table: "requests", id: requestId }) : null);

  if (!detail.data || !row) {
    return (
      <>
        <DrawerHeader title={"Event #" + id} onClose={onClose} />
        <div className="px-5">{detail.error ? <ErrorNote error={detail.error} /> : <Spinner label="Loading the event" />}</div>
      </>
    );
  }

  const type = String(row.event);
  const doc = EVENTS[type];
  const data = row.data;
  const dataKeys = Object.keys(data).filter((key) => !EVENT_COLUMN_SET.has(key));
  const missing = Object.keys(fieldsOf(type)).filter((key) => !(key in data) && !EVENT_COLUMN_SET.has(key));
  const runId = typeof row.run_id === "string" && row.run_id ? row.run_id : null;
  const sessionId = typeof row.session_id === "string" && row.session_id ? row.session_id : null;
  const installId = typeof row.install_id === "string" && row.install_id ? row.install_id : null;

  return (
    <>
      <DrawerHeader
        title={doc ? doc.title : type}
        subtitle={
          <>
            <span className="font-mono">{type}</span> · row #{fmtInt(Number(row.id))}
            {doc ? <span className="block text-neutral-500">{doc.when}</span> : null}
          </>
        }
        onClose={onClose}
      />
      <div className="flex-1 overflow-y-auto px-5 pb-8">
        <Hold loading={detail.loading}>
          <div className="flex flex-wrap gap-2">
            {runId ? (
              <Link href={"/runs/" + encodeURIComponent(runId)} className={buttonClass}>
                <Activity className="h-3.5 w-3.5" /> Open run timeline
              </Link>
            ) : null}
            {sessionId ? (
              <button type="button" className={buttonClass} onClick={() => onShowEvents({ sessionId })}>
                <MonitorSmartphone className="h-3.5 w-3.5" /> Show this session
              </button>
            ) : null}
            {installId ? (
              <button type="button" className={buttonClass} onClick={() => onShowEvents({ installId })}>
                <User className="h-3.5 w-3.5" /> Show this player
              </button>
            ) : null}
          </div>

          {detail.data.copies > 1 ? (
            <p className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-900">
              This event_id is stored {detail.data.copies} times (the game re-sent its batch).{" "}
              {detail.data.firstCopyId === row.id
                ? "This is the first copy: the one kept when duplicates are hidden."
                : "“Hide duplicates” keeps row #" + detail.data.firstCopyId + " instead of this one."}
            </p>
          ) : null}

          <Section title="Data" note={dataKeys.length + " fields"}>
            <KeyValues
              items={dataKeys.map((key) => ({ key, value: data[key], doc: describeField(key, type), docInline: true }))}
            />
            {missing.length > 0 ? (
              <p className="mt-2 text-xs text-neutral-500">
                In the catalog but not in this row (logged by newer builds): <span className="font-mono">{missing.join(", ")}</span>
              </p>
            ) : null}
            <details className="mt-3">
              <summary className="cursor-pointer text-sm text-neutral-600">Raw data JSON</summary>
              <JsonBlock text={JSON.stringify(data, null, 2)} />
            </details>
          </Section>

          <Section title="Columns">
            <KeyValues
              items={EVENT_TABLE_COLUMNS.map((key) => ({
                key,
                value: row[key],
                doc: EVENT_COLUMNS[key],
                shown:
                  key === "level" && row.level
                    ? String(row.level) + " · " + levelName(String(row.level)) + " level"
                    : key === "received_at" && row.received_at
                      ? String(row.received_at) + " UTC · " + fmtTime(String(row.received_at)) + " local"
                      : undefined,
              }))}
            />
          </Section>

          <Section
            title={"Request #" + (requestId ?? "-")}
            note="the HTTP request (batch) this event arrived in"
            actions={
              requestId !== null ? (
                <>
                  <button
                    type="button"
                    className={buttonClass}
                    onClick={() => onShowEvents({ requestId: String(requestId), dedupe: "0" })}
                  >
                    <Inbox className="h-3.5 w-3.5" /> Its events
                  </button>
                  <button type="button" className={buttonClass} onClick={() => onOpen({ table: "requests", id: requestId })}>
                    <ExternalLink className="h-3.5 w-3.5" /> Full request
                  </button>
                </>
              ) : null
            }
          >
            {request.error ? (
              <ErrorNote error={request.error} />
            ) : request.data ? (
              <Hold loading={request.loading}>
                <RequestFields detail={request.data} />
              </Hold>
            ) : (
              <Spinner label="Loading the request" />
            )}
          </Section>
        </Hold>
      </div>
    </>
  );
}

// ---- Requests ----

function RequestDetail({
  id,
  onClose,
  onShowEvents,
  onShowRequests,
}: {
  id: number;
  onClose: () => void;
  onShowEvents: (filters: UrlPatch) => void;
  onShowRequests: (filters: UrlPatch) => void;
}) {
  const detail = useApi<RequestDetailResponse>(apiUrl("/api/rows", { table: "requests", id }));
  if (!detail.data) {
    return (
      <>
        <DrawerHeader title={"Request #" + id} onClose={onClose} />
        <div className="px-5">{detail.error ? <ErrorNote error={detail.error} /> : <Spinner label="Loading the request" />}</div>
      </>
    );
  }
  const { row, body, bodyTruncated, storedEvents } = detail.data;
  const clientIp = typeof row.client_ip === "string" && row.client_ip ? row.client_ip : null;

  return (
    <>
      <DrawerHeader
        title={"Request #" + fmtInt(Number(row.id))}
        subtitle={
          <span className="font-mono">
            {String(row.method ?? "")} {String(row.path ?? "")} · {String(row.status ?? "")}
          </span>
        }
        onClose={onClose}
      />
      <div className="flex-1 overflow-y-auto px-5 pb-8">
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            className={buttonClass}
            onClick={() => onShowEvents({ requestId: String(row.id), dedupe: "0" })}
            disabled={storedEvents === 0}
          >
            <Inbox className="h-3.5 w-3.5" /> Its {fmtInt(storedEvents)} stored events
          </button>
          {clientIp ? (
            <button type="button" className={buttonClass} onClick={() => onShowRequests({ clientIp })}>
              <Globe className="h-3.5 w-3.5" /> Requests from this IP
            </button>
          ) : null}
        </div>
        <Section title="Request">
          <RequestFields detail={detail.data} withHeaders={false} />
        </Section>
        <Section title="Headers" note={Object.keys(detail.data.headers).length + " headers"}>
          <KeyValues items={Object.entries(detail.data.headers).map(([key, value]) => ({ key, value }))} />
        </Section>
        <Section
          title="Body"
          note={bodyTruncated ? "first 1,000,000 characters" : undefined}
          actions={body ? <CopyButton text={body} /> : null}
        >
          {body ? <JsonBlock text={prettyJson(body)} copy={false} /> : <p className="text-sm text-neutral-500">No body.</p>}
        </Section>
      </div>
    </>
  );
}

// The request's columns (IP and browser first), optionally with its headers.
function RequestFields({ detail, withHeaders = true }: { detail: RequestDetailResponse; withHeaders?: boolean }) {
  const keys = [...REQUEST_FIRST, ...REQUEST_COLUMNS.filter((key) => !REQUEST_FIRST.includes(key))];
  const extra = Object.keys(detail.row).filter((key) => !keys.includes(key));
  const headerCount = Object.keys(detail.headers).length;
  return (
    <>
      <KeyValues
        items={[...keys, ...extra].map((key) => ({
          key,
          value: detail.row[key],
          shown:
            key === "received_at" && detail.row.received_at
              ? String(detail.row.received_at) + " UTC · " + fmtTime(String(detail.row.received_at)) + " local"
              : undefined,
        }))}
      />
      {withHeaders && headerCount > 0 ? (
        <details className="mt-3">
          <summary className="cursor-pointer text-sm text-neutral-600">All {headerCount} headers</summary>
          <div className="mt-2">
            <KeyValues items={Object.entries(detail.headers).map(([key, value]) => ({ key, value }))} />
          </div>
        </details>
      ) : null}
    </>
  );
}

// ---- Building blocks ----

function DrawerHeader({ title, subtitle, onClose }: { title: string; subtitle?: ReactNode; onClose: () => void }) {
  return (
    <div className="flex items-start gap-3 border-b border-black/5 px-5 py-4">
      <div className="min-w-0 flex-1">
        <h2 className="text-[17px] font-semibold text-neutral-900">{title}</h2>
        {subtitle ? <div className="mt-0.5 text-[13px] text-neutral-600">{subtitle}</div> : null}
      </div>
      <button
        type="button"
        onClick={onClose}
        className="rounded-md p-1.5 text-neutral-500 hover:bg-neutral-100 hover:text-neutral-800"
        aria-label="Close (Esc)"
        title="Close (Esc)"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}

function Section({ title, note, actions, children }: { title: string; note?: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <section className="mt-6">
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <h3 className="text-sm font-semibold text-neutral-900">{title}</h3>
        {note ? <span className="text-xs text-neutral-500">{note}</span> : null}
        {actions ? <div className="ml-auto flex items-center gap-2">{actions}</div> : null}
      </div>
      {children}
    </section>
  );
}

type KeyValue = { key: string; value: unknown; doc?: string; docInline?: boolean; shown?: string };

function KeyValues({ items }: { items: KeyValue[] }) {
  if (items.length === 0) {
    return <p className="text-sm text-neutral-500">Nothing here.</p>;
  }
  return (
    <dl className="divide-y divide-black/5 overflow-hidden rounded-lg border border-black/10">
      {items.map((item) => (
        <div key={item.key} className="grid grid-cols-[minmax(110px,170px)_1fr] gap-3 px-3 py-1.5">
          <dt className="break-all font-mono text-[12px] leading-5 text-neutral-500" title={item.doc}>
            {item.key}
          </dt>
          <dd className="min-w-0 text-[13px] leading-5 text-neutral-900">
            <span className="break-words [overflow-wrap:anywhere]">{item.shown ?? <Value value={item.value} />}</span>
            {item.doc && item.docInline ? <span className="block text-xs leading-snug text-neutral-500">{item.doc}</span> : null}
          </dd>
        </div>
      ))}
    </dl>
  );
}

function Value({ value }: { value: unknown }) {
  if (value === null || value === undefined) {
    return <span className="text-neutral-400">null</span>;
  }
  if (typeof value === "boolean") {
    return <Badge>{value ? "true" : "false"}</Badge>;
  }
  if (typeof value === "object") {
    return <code className="font-mono text-[12px]">{JSON.stringify(value)}</code>;
  }
  return <>{String(value)}</>;
}

function prettyJson(text: string): string {
  try {
    return JSON.stringify(JSON.parse(text), null, 2);
  } catch {
    return text;
  }
}

function JsonBlock({ text, copy = true }: { text: string; copy?: boolean }) {
  return (
    <div className="relative mt-2">
      {copy ? (
        <div className="absolute right-2 top-2">
          <CopyButton text={text} />
        </div>
      ) : null}
      <pre className="max-h-[480px] overflow-auto rounded-lg border border-black/5 bg-[#f6f6f3] p-3 font-mono text-[12px] leading-relaxed text-neutral-800">
        {text}
      </pre>
    </div>
  );
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      className={buttonClass + " h-7 px-2 text-xs"}
      onClick={() => {
        copyText(text).then((ok) => {
          setCopied(ok);
          if (ok) setTimeout(() => setCopied(false), 1500);
        });
      }}
    >
      {copied ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
      {copied ? "Copied" : "Copy"}
    </button>
  );
}
