"use client";
// The log server's data health: did every batch arrive and parse, did
// anything bypass Cloudflare, were batches retried or lost.
import type { ReactNode } from "react";
import { Card } from "@/components/ui";
import { fmtInt, fmtPct } from "@/lib/format";
import { STATUS } from "@/lib/theme";
import type { OverviewResponse } from "./types";

type Health = OverviewResponse["health"];
type Verdict = { color: string; text: string } | null; // null = information only

function Row({ label, value, detail, verdict }: { label: string; value: ReactNode; detail: ReactNode; verdict: Verdict }) {
  return (
    <div className="flex items-start justify-between gap-3 border-t border-black/[0.06] py-2.5 first:border-t-0 first:pt-0">
      <div className="min-w-0">
        <div className="text-[13px] text-neutral-700">{label}</div>
        <div className="mt-0.5 text-xs leading-snug text-neutral-500">{detail}</div>
      </div>
      <div className="shrink-0 text-right">
        <div className="text-[15px] font-semibold tabular-nums text-neutral-900">{value}</div>
        {verdict ? (
          <div className="mt-0.5 inline-flex items-center gap-1.5 text-xs text-neutral-600">
            <span className="h-2 w-2 rounded-full" style={{ backgroundColor: verdict.color }} />
            {verdict.text}
          </div>
        ) : null}
      </div>
    </div>
  );
}

const OK: Verdict = { color: STATUS.good, text: "OK" };

export default function DataHealth({ health: h }: { health: Health }) {
  const statusText = h.statuses
    .filter((s) => s.status !== 200)
    .map((s) => s.status + " " + s.method + " ×" + fmtInt(s.requests))
    .join(" · ");
  const refused = h.statuses.filter((s) => s.status === 400).reduce((sum, s) => sum + s.requests, 0);
  return (
    <Card title="Data health" subtitle="The log server's side (the level filter does not apply)">
      <Row
        label="Batches that failed to parse"
        value={fmtInt(h.parseFailed)}
        detail={"of " + fmtInt(h.batches) + " game batches (POST /v1/batch)"}
        verdict={h.parseFailed > 0 ? { color: STATUS.critical, text: "Check" } : OK}
      />
      <Row
        label="Replies other than 200"
        value={fmtInt(h.non200)}
        detail={statusText || "every request got a 200"}
        verdict={refused > 0 ? { color: STATUS.critical, text: "Batches refused" } : OK}
      />
      <Row
        label="Requests without cf_ray (skipped Cloudflare)"
        value={fmtInt(h.withoutCfRay)}
        detail={fmtInt(h.batchesWithoutCfRay) + " of them game batches · of " + fmtInt(h.requests) + " requests"}
        verdict={h.batchesWithoutCfRay > 0 ? { color: STATUS.warning, text: "Direct hits" } : OK}
      />
      <Row
        label="Retried batches (same event_id twice)"
        value={fmtInt(h.duplicatedEventIds)}
        detail={fmtInt(h.duplicateRows) + " extra rows, removed before counting"}
        verdict={null}
      />
      <Row
        label="Missing seq numbers (lost events)"
        value={fmtInt(h.missingSeq)}
        detail={"in " + fmtInt(h.sessionsWithGaps) + " of " + fmtInt(h.sessions) + " sessions (events lost at the very end cannot be seen)"}
        verdict={h.missingSeq > 0 ? { color: STATUS.warning, text: "Some lost" } : OK}
      />
      <Row
        label="Test plays (install id qa-)"
        value={fmtPct(h.allEvents ? h.testEvents / h.allEvents : null)}
        detail={fmtInt(h.testEvents) + " of " + fmtInt(h.allEvents) + " stored events, all filters off"}
        verdict={null}
      />
    </Card>
  );
}
