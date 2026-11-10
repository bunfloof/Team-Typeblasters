"use client";
// Top of the run page: what happened (outcome, play time, wave, score...),
// who played it (install, session, IP, browser, device - shown in full, on
// purpose) and links to the player's previous / next run.
import Link from "next/link";
import type { ReactNode } from "react";
import { ArrowLeft, ArrowRight, Users } from "lucide-react";
import { Badge, Card, PageHeader, StatTile, buttonClass } from "@/components/ui";
import { levelName } from "@/lib/catalog";
import { fmt1, fmtDuration, fmtInt, fmtTime, shortId } from "@/lib/format";
import { KIND_LABELS, OUTCOME_COLORS } from "@/lib/theme";
import type { RunDetailResponse } from "@/lib/timelineData";

const OUTCOME_LABELS: Record<string, string> = {
  won: "Won",
  lost: "Died",
  restart: "Restarted",
  abandoned: "Abandoned (no run end)",
  playing: "Still playing",
};

function text(value: unknown): string | null {
  return value === null || value === undefined || value === "" ? null : String(value);
}

function Field({ label, children, mono }: { label: string; children: ReactNode; mono?: boolean }) {
  if (children === null || children === undefined || children === "") return null;
  return (
    <div className="grid grid-cols-[8.5rem_minmax(0,1fr)] gap-2 py-1 text-[13px]">
      <dt className="text-neutral-500">{label}</dt>
      <dd className={"min-w-0 break-words text-neutral-900 " + (mono ? "font-mono text-xs leading-5" : "")}>{children}</dd>
    </div>
  );
}

function RunLink({ runId, label, dir }: { runId: string | null; label: string; dir: "prev" | "next" }) {
  const icon = dir === "prev" ? <ArrowLeft className="h-3.5 w-3.5" /> : <ArrowRight className="h-3.5 w-3.5" />;
  if (!runId) {
    return (
      <span className={buttonClass + " pointer-events-none opacity-40"} aria-disabled="true">
        {dir === "prev" ? icon : null}
        {label}
        {dir === "next" ? icon : null}
      </span>
    );
  }
  return (
    <Link href={"/runs/" + encodeURIComponent(runId)} className={buttonClass} title={runId}>
      {dir === "prev" ? icon : null}
      {label}
      {dir === "next" ? icon : null}
    </Link>
  );
}

export default function RunHeader({ data }: { data: RunDetailResponse }) {
  const { summary: s, session, request, siblings, quality } = data;
  const outcomeColor = OUTCOME_COLORS[s.outcome] ?? OUTCOME_COLORS.abandoned;
  const screen = session && session.screen_width ? session.screen_width + " × " + session.screen_height + (session.screen_dpi ? " @ " + session.screen_dpi + " dpi" : "") : null;
  const otherIps = data.ips.filter((ip) => ip !== request?.client_ip);
  // "zombie_explosive" -> "Red (explosive) zombie", "orb" -> "Energy orb".
  const killerKind = s.killer?.startsWith("zombie_") ? s.killer.slice("zombie_".length) : null;
  const killer = killerKind ? (KIND_LABELS[killerKind] ?? killerKind) + " zombie" : s.killer === "orb" ? "Energy orb" : s.killer;

  return (
    <>
      <PageHeader
        title={"Run " + shortId(s.run_id)}
        description={
          <>
            {levelName(s.level)} level · {s.run_number !== null ? "run #" + s.run_number + " in this browser" : "run number unknown"} · started{" "}
            {fmtTime(s.started_at)}
            {s.resumed_from_wave !== null ? " · went on from a checkpoint at wave " + s.resumed_from_wave : ""}
            {s.test ? " · test play" : ""}
          </>
        }
        actions={
          <>
            <RunLink runId={siblings.prev} label="Previous run" dir="prev" />
            <span className="text-xs tabular-nums text-neutral-500">
              {siblings.position !== null ? siblings.position + " of " + siblings.total : siblings.total + " runs"}
            </span>
            <RunLink runId={siblings.next} label="Next run" dir="next" />
            {s.install_id ? (
              <Link href={"/runs?installId=" + encodeURIComponent(s.install_id)} className={buttonClass}>
                <Users className="h-3.5 w-3.5" /> All runs of this player
              </Link>
            ) : null}
          </>
        }
      />

      <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5 xl:grid-cols-9">
        <StatTile label="Outcome" value={<Badge color={outcomeColor}>{OUTCOME_LABELS[s.outcome] ?? s.outcome}</Badge>} note={s.tips_enabled === null ? undefined : s.tips_enabled ? "tips on" : "tips off"} />
        <StatTile label="Play time" value={fmtDuration(s.seconds)} note="pauses excluded" />
        <StatTile label="Wave reached" value={fmtInt(s.wave_reached)} note={s.death_phase ? "died in a " + s.death_phase : undefined} />
        <StatTile label="Score" value={fmtInt(s.score)} />
        <StatTile label="Kills" value={fmtInt(s.kills)} />
        <StatTile label="Accuracy" value={s.accuracy !== null ? fmt1(s.accuracy) + "%" : "-"} />
        <StatTile label="WPM" value={fmt1(s.wpm)} note="rides included" />
        <StatTile label="Best combo" value={fmtInt(s.best_combo)} />
        <StatTile label="Killed by" value={<span className="text-base leading-tight">{killer ?? "-"}</span>} note={s.death_wave !== null ? "wave " + s.death_wave : undefined} />
      </div>

      <Card title="Player and device" subtitle="Everything the game and the log server saw about this play, unfiltered." className="mb-4">
        <div className="grid gap-x-8 lg:grid-cols-2">
          <dl>
            <Field label="Install id" mono>
              {s.install_id ? (
                <Link href={"/runs?installId=" + encodeURIComponent(s.install_id)} className="text-[#1c5cab] hover:underline">
                  {s.install_id}
                </Link>
              ) : null}
            </Field>
            <Field label="Session" mono>
              {s.session_id}
            </Field>
            <Field label="Run id" mono>
              {s.run_id}
            </Field>
            <Field label="IP address" mono>
              {request?.client_ip}
              {otherIps.length > 0 ? " (also " + otherIps.join(", ") + ")" : ""}
            </Field>
            <Field label="Forwarded for" mono>
              {request?.x_forwarded_for !== request?.client_ip ? request?.x_forwarded_for : null}
            </Field>
            <Field label="Country">{request?.cf_ipcountry}</Field>
            <Field label="Language">{request?.accept_language ?? text(session?.language)}</Field>
            <Field label="Opened from" mono>
              {text(session?.page_url) ?? request?.origin ?? request?.referer}
            </Field>
            <Field label="Build" mono>
              {s.build}
            </Field>
          </dl>
          <dl>
            <Field label="Browser" mono>
              {request?.user_agent}
            </Field>
            <Field label="Platform">
              {[request?.sec_ch_ua_platform, request?.sec_ch_ua_mobile === "?1" ? "mobile" : null].filter(Boolean).join(" · ") || null}
            </Field>
            <Field label="OS (Unity)">{text(session?.os)}</Field>
            <Field label="Device">{text(session?.device_model)}</Field>
            <Field label="GPU">{text(session?.gpu)}</Field>
            <Field label="CPU / memory">
              {session?.cpu_count ? session.cpu_count + " cores" + (session.memory_mb ? " · " + fmtInt(Number(session.memory_mb)) + " MB" : "") : null}
            </Field>
            <Field label="Canvas">{screen}</Field>
            <Field label="Unity / game">
              {[text(session?.unity_version), text(session?.app_version)].filter(Boolean).join(" · ") || null}
            </Field>
            <Field label="This browser">
              {session
                ? (session.install_new === true ? "first visit" : "returning") +
                  (session.runs_before !== undefined ? " · " + session.runs_before + " runs before this page load" : "") +
                  (session.storage_ok === false ? " · storage blocked (private mode?)" : "")
                : null}
            </Field>
          </dl>
        </div>
        <p className="mt-3 border-t border-black/5 pt-2 text-xs text-neutral-500">
          {fmtInt(quality.rawEvents)} rows stored · {fmtInt(s.events)} events after removing {fmtInt(quality.duplicates)} duplicates (retried batches) ·{" "}
          {fmtInt(quality.batches)} batches · {quality.seqGaps > 0 ? fmtInt(quality.seqGaps) + " missing seq numbers (lost batches?)" : "no seq gaps"} · last event{" "}
          {fmtTime(s.last_event_at)}
          {session ? "" : " · no session_start found for this page load"}
        </p>
      </Card>
    </>
  );
}
