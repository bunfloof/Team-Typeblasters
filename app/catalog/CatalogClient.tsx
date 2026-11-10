"use client";
// The event dictionary (lib/catalog.ts) as a page: every event type by
// group, when the game logs it and what each field means, with live row
// counts from /api/meta and a link to the Explorer filtered to that type.
import { useMemo, useState } from "react";
import Link from "next/link";
import { ArrowRight, Search } from "lucide-react";
import { EVENTS, EVENT_COLUMNS, ENVELOPE_FIELDS, PLAYER_STATE_FIELDS, type FieldDoc } from "@/lib/catalog";
import { fmtInt } from "@/lib/format";
import { globalParams, useGlobalFilters } from "@/lib/globalFilters";
import type { MetaResponse } from "@/lib/types";
import { apiUrl, useApi } from "@/lib/useApi";
import { Card, ErrorNote, inputClass } from "@/components/ui";
import { groupEventTypes } from "@/components/explorer/eventGroups";

// Keys every event's data repeats from the table columns, or carries anyway:
// not worth listing as "also seen" under each event.
const COMMON_KEYS = new Set([...Object.keys(EVENT_COLUMNS), ...Object.keys(ENVELOPE_FIELDS)]);

function matches(needle: string, ...texts: (string | undefined)[]): boolean {
  return texts.some((text) => !!text && text.toLowerCase().includes(needle));
}

function fieldsMatch(needle: string, fields: FieldDoc): boolean {
  return Object.entries(fields).some(([name, doc]) => matches(needle, name, doc));
}

export default function CatalogClient() {
  const global = useGlobalFilters();
  const meta = useApi<MetaResponse>(apiUrl("/api/meta", globalParams(global)));
  const [search, setSearch] = useState("");
  const needle = search.trim().toLowerCase();

  const counts = useMemo(
    () => (meta.data ? Object.fromEntries(meta.data.eventCounts.map((row) => [row.event, row.count])) : null),
    [meta.data],
  );
  const groups = useMemo(() => groupEventTypes(counts ? Object.keys(counts) : []), [counts]);

  // An event is shown when the search matches its name, title, "when" or any of its fields.
  const shownGroups = groups
    .map((group) => ({
      ...group,
      types: group.types.filter((type) => {
        const doc = EVENTS[type];
        if (!needle) return true;
        if (!doc) return matches(needle, type, ...(meta.data?.dataFields[type] ?? []));
        return matches(needle, type, doc.title, doc.when) || fieldsMatch(needle, doc.fields);
      }),
    }))
    .filter((group) => group.types.length > 0);
  const shownCount = shownGroups.reduce((sum, group) => sum + group.types.length, 0);

  const references = [
    { id: "columns", title: "Columns of every event", note: "Real columns of the events table (most also repeat inside data).", fields: EVENT_COLUMNS },
    { id: "envelope", title: "In every event's data", note: "Added to every event by the game.", fields: ENVELOPE_FIELDS },
    {
      id: "player-state",
      title: "Player state",
      note: "Added to the events marked “+ player state”: how things stood at that moment.",
      fields: PLAYER_STATE_FIELDS,
    },
  ].filter((reference) => !needle || matches(needle, reference.title) || fieldsMatch(needle, reference.fields));

  return (
    <div className="flex items-start gap-6">
      <nav className="sticky top-16 hidden max-h-[calc(100vh-5rem)] w-52 shrink-0 overflow-y-auto pb-6 lg:block" aria-label="Event types">
        {shownGroups.map((group) => (
          <div key={group.key} className="mb-3">
            <div className="px-2 pb-1 text-[11px] font-medium uppercase tracking-wide text-neutral-400">{group.label}</div>
            {group.types.map((type) => (
              <a
                key={type}
                href={"#event-" + type}
                className="flex items-center justify-between gap-2 rounded-md px-2 py-0.5 text-[13px] text-neutral-700 hover:bg-black/[0.04]"
              >
                <span className="truncate font-mono text-[12.5px]">{type}</span>
                <span className="text-xs tabular-nums text-neutral-400">{counts ? fmtInt(counts[type] ?? 0) : ""}</span>
              </a>
            ))}
          </div>
        ))}
        {references.length > 0 ? (
          <div className="mb-3">
            <div className="px-2 pb-1 text-[11px] font-medium uppercase tracking-wide text-neutral-400">Shared fields</div>
            {references.map((reference) => (
              <a
                key={reference.id}
                href={"#" + reference.id}
                className="block rounded-md px-2 py-0.5 text-[13px] text-neutral-700 hover:bg-black/[0.04]"
              >
                {reference.title}
              </a>
            ))}
          </div>
        ) : null}
      </nav>

      <div className="min-w-0 flex-1 space-y-6">
        <div className="flex flex-wrap items-center gap-3">
          <label className="relative">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-neutral-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search events and fields, e.g. combo or ambusher"
              className={inputClass + " h-9 w-[360px] pl-8"}
            />
          </label>
          <span className="text-sm text-neutral-500">
            {shownCount} event types
            {meta.data ? " · " + fmtInt(meta.data.totals.events) + " rows" + (global.includeTest ? "" : " (test plays hidden)") : ""}
          </span>
        </div>
        {meta.error ? (
          <ErrorNote
            error={meta.error}
            hint={
              meta.error.includes(".env.local")
                ? "The catalog below still works; the row counts need DB_PASSWORD in .env.local (then restart the dashboard: pnpm dev, or pnpm start on the server)."
                : "The catalog below still works; only the row counts are missing."
            }
          />
        ) : null}

        {shownGroups.map((group) => (
          <section key={group.key} className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-neutral-500">{group.label}</h2>
            {group.types.map((type) => (
              <EventCard
                key={type}
                type={type}
                count={counts ? counts[type] ?? 0 : null}
                seenKeys={meta.data?.dataFields[type] ?? null}
                needle={needle}
              />
            ))}
          </section>
        ))}

        {references.length > 0 ? (
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-neutral-500">Shared fields</h2>
            {references.map((reference) => (
              <div key={reference.id} id={reference.id} className="scroll-mt-20">
                <Card title={reference.title} subtitle={reference.note}>
                  <FieldTable fields={reference.fields} needle={needle} />
                </Card>
              </div>
            ))}
          </section>
        ) : null}

        {shownCount === 0 && references.length === 0 ? (
          <p className="py-10 text-center text-sm text-neutral-500">Nothing in the catalog matches “{search}”.</p>
        ) : null}
      </div>
    </div>
  );
}

function EventCard({
  type,
  count,
  seenKeys,
  needle,
}: {
  type: string;
  count: number | null; // null while loading
  seenKeys: string[] | null; // data keys seen in stored rows (sample)
  needle: string;
}) {
  const doc = EVENTS[type];
  const seen = seenKeys ? new Set(seenKeys) : null;
  const ownFields = doc ? doc.fields : {};
  const known = new Set([...Object.keys(ownFields), ...(doc?.playerState ? Object.keys(PLAYER_STATE_FIELDS) : [])]);
  const extra = (seenKeys ?? []).filter((key) => !known.has(key) && !COMMON_KEYS.has(key));

  return (
    <div id={"event-" + type} className="scroll-mt-20">
      <Card
        title={
          <span className="flex flex-wrap items-baseline gap-x-2">
            {doc ? doc.title : type}
            <code className="rounded bg-neutral-100 px-1.5 py-0.5 font-mono text-[12px] font-normal text-neutral-700">{type}</code>
          </span>
        }
        subtitle={doc ? doc.when : "Logged by the game but not described in the catalog yet."}
        actions={
          <>
            <span className="text-sm tabular-nums text-neutral-600">
              {count === null ? "" : count === 0 ? <span className="text-neutral-400">no rows yet</span> : fmtInt(count) + " rows"}
            </span>
            <Link
              href={"/explorer?event=" + encodeURIComponent(type)}
              className="inline-flex items-center gap-1 text-sm font-medium text-[#1c5cab] hover:underline"
            >
              Explorer <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </>
        }
      >
        {Object.keys(ownFields).length > 0 ? (
          <FieldTable fields={ownFields} needle={needle} seen={count ? seen : null} />
        ) : (
          <p className="text-sm text-neutral-500">{doc ? "No fields of its own." : null}</p>
        )}
        {doc?.playerState ? (
          <p className="mt-3 text-[13px] text-neutral-500">
            + player state at that moment (health, combo, score, charges, what is being typed, position…):{" "}
            <a href="#player-state" className="text-[#1c5cab] hover:underline">
              see Player state
            </a>
            .
          </p>
        ) : null}
        {extra.length > 0 ? (
          <p className="mt-2 text-[13px] text-neutral-500">
            Also seen in the stored data: <span className="font-mono text-[12.5px] text-neutral-700">{extra.join(", ")}</span>
          </p>
        ) : null}
      </Card>
    </div>
  );
}

// Field name and meaning. With `seen` (keys found in recent rows, from
// /api/meta), fields missing there are marked: the game is adding fields.
function FieldTable({ fields, needle, seen = null }: { fields: FieldDoc; needle: string; seen?: Set<string> | null }) {
  return (
    <table className="w-full text-[13px]">
      <tbody className="divide-y divide-black/5">
        {Object.entries(fields).map(([name, doc]) => {
          const hit = !!needle && matches(needle, name, doc);
          return (
            <tr key={name} className={hit ? "bg-[#2a78d6]/[0.06]" : undefined}>
              <td className="w-56 py-1.5 pr-4 align-top font-mono text-[12.5px] text-neutral-800">{name}</td>
              <td className="py-1.5 align-top leading-snug text-neutral-600">{doc}</td>
              <td
                className="w-32 py-1.5 pl-3 text-right align-top text-xs text-neutral-400"
                title={seen && !seen.has(name) ? "Not in the 200 most recent rows of this event: a new field, or one logged only in some cases." : undefined}
              >
                {seen && !seen.has(name) ? "not in recent rows" : ""}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
