"use client";
// "What was going on at time t": the compact strip above the timeline and
// the full panel below it (player state, powers, zombies on the field, the
// word being typed, and every event within a few seconds).
import type { ReactNode } from "react";
import { ChevronLeft, ChevronRight, MousePointerClick } from "lucide-react";
import { Card, EmptyState, buttonClass } from "@/components/ui";
import type { EventRow } from "@/lib/types";
import {
  dataBool,
  dataNum,
  dataStr,
  describeEvent,
  fmtT,
  markerCategoryOf,
  tOf,
  type Moment,
  type ZombieBar,
} from "@/lib/timelineData";
import { INK, STATUS } from "@/lib/theme";
import { MARKER_STYLE, PHASE_STYLE, WORD_OUTCOMES, WRONG_KEY_STYLE, kindColor, kindLabel, wordOutcomeGroup } from "./timelineStyle";

const PHASE_EVENTS = new Set(["ride_start", "ride_end", "wave_start", "wave_end"]);

// The dot color of an event in lists: the color it has on the timeline.
function eventColor(e: EventRow): string {
  if (e.event === "zombie" || e.event === "zombie_spawn") return kindColor(dataStr(e, "kind") ?? "normal");
  if (e.event === "word") return WORD_OUTCOMES.find((o) => o.id === wordOutcomeGroup(dataStr(e, "outcome") ?? ""))?.color ?? INK.muted;
  if (e.event === "wrong_key") return dataBool(e, "stray") ? WRONG_KEY_STYLE.stray.color : WRONG_KEY_STYLE.wrong.color;
  if (PHASE_EVENTS.has(e.event)) return e.event.startsWith("ride") ? PHASE_STYLE.ride.fill : PHASE_STYLE.fight.fill;
  return MARKER_STYLE[markerCategoryOf(e.event)].color;
}

function Dot({ color }: { color: string }) {
  return <span className="inline-block h-2 w-2 shrink-0 rounded-full" style={{ backgroundColor: color }} />;
}

function Value({ label, value, note }: { label: string; value: ReactNode; note?: ReactNode }) {
  return (
    <div className="min-w-0">
      <div className="text-xs text-neutral-500">{label}</div>
      <div className="truncate text-[15px] font-semibold tabular-nums text-neutral-900">{value}</div>
      {note ? <div className="truncate text-[11px] text-neutral-500">{note}</div> : null}
    </div>
  );
}

function typingText(moment: Moment): string {
  if (moment.typing) {
    return moment.typing.word + " → " + moment.typing.target.replace(/_/g, " ");
  }
  const target = moment.state ? dataStr(moment.state, "typing_target") : null;
  return target && target !== "none" ? target.replace(/_/g, " ") : "nothing";
}

// One line under the timeline toolbar, always visible while scrolling.
export function MomentStrip({ moment, onClear }: { moment: Moment | null; onClear: () => void }) {
  if (!moment) {
    return (
      <div className="flex items-center gap-2 text-xs text-neutral-500">
        <MousePointerClick className="h-3.5 w-3.5" /> Click anywhere on the timeline to see the state of the game at that moment.
      </div>
    );
  }
  const items: [string, string][] = [
    ["Phase", moment.phase?.label ?? "between phases"],
    ["Health", moment.health !== null ? String(moment.health) : "-"],
    ["Combo", moment.combo !== null ? String(moment.combo) : "-"],
    ["Zombies", String(moment.zombiesOnField.length)],
    ["Typing", typingText(moment)],
    ["Speed", moment.wpm !== null ? Math.round(moment.wpm) + " WPM" : "-"],
  ];
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-neutral-600">
      <span className="rounded-md bg-neutral-900 px-1.5 py-0.5 font-medium tabular-nums text-white">{fmtT(moment.t)}</span>
      {items.map(([label, value]) => (
        <span key={label}>
          <span className="text-neutral-500">{label}</span> <span className="font-medium tabular-nums text-neutral-900">{value}</span>
        </span>
      ))}
      <button type="button" onClick={onClear} className="ml-auto text-neutral-500 underline-offset-2 hover:underline">
        clear
      </button>
    </div>
  );
}

type PanelProps = {
  moment: Moment | null;
  duration: number;
  onSelectT: (t: number) => void;
  selectedZombie: string | null;
  onSelectZombie: (key: string | null) => void;
  windowSeconds: number;
};

export function MomentPanel({ moment, duration, onSelectT, selectedZombie, onSelectZombie, windowSeconds }: PanelProps) {
  if (!moment) {
    return (
      <Card title="Moment" subtitle="The state of the game at one instant.">
        <EmptyState>Click the timeline, a point on the map or a row of the events table to inspect that moment.</EmptyState>
      </Card>
    );
  }
  const s = moment.state;
  const n = (field: string) => (s ? dataNum(s, field) : null);
  const step = (delta: number) => onSelectT(Math.max(0, Math.min(duration, Math.round((moment.t + delta) * 10) / 10)));
  const powers: [string, string | null][] = [
    ["Lure bombs", n("lure_charges") !== null ? String(n("lure_charges")) : null],
    ["Freezes", n("freeze_charges") !== null ? String(n("freeze_charges")) : null],
    ["FRENZY kept", n("frenzy_kept") !== null ? String(n("frenzy_kept")) : null],
    ["RPG kept", n("rpg_kept") !== null ? n("rpg_kept") + (s && dataBool(s, "rpg_loaded") ? " (loaded)" : "") : null],
  ];
  const flags = s
    ? [
        dataBool(s, "frenzy_on") && "FRENZY running",
        dataBool(s, "freeze_on") && "Freeze running",
        dataBool(s, "aiming_lure") && "Aiming a lure",
        dataBool(s, "can_buy_frenzy") && "Can buy FRENZY",
        dataBool(s, "can_buy_rpg") && "Can buy RPG",
      ].filter((flag): flag is string => !!flag)
    : [];
  const letters = s ? dataNum(s, "typing_letters_done") : null;
  const length = s ? dataNum(s, "typing_word_length") : null;
  const stateAge = s ? moment.t - tOf(s) : null;

  return (
    <Card
      title={"Moment at " + fmtT(moment.t)}
      subtitle={moment.phase ? moment.phase.label + " · " + fmtT(moment.phase.start) + " – " + fmtT(moment.phase.end) : "Between phases"}
      actions={
        <div className="flex gap-1">
          <button type="button" className={buttonClass} onClick={() => step(-1)} aria-label="One second earlier">
            <ChevronLeft className="h-3.5 w-3.5" /> 1 s
          </button>
          <button type="button" className={buttonClass} onClick={() => step(1)} aria-label="One second later">
            1 s <ChevronRight className="h-3.5 w-3.5" />
          </button>
        </div>
      }
    >
      <div className="grid grid-cols-3 gap-x-4 gap-y-3 sm:grid-cols-6">
        <Value label="Health" value={moment.health ?? "-"} />
        <Value label="Combo" value={moment.combo ?? "-"} />
        <Value label="Score" value={moment.score ?? "-"} />
        <Value label="Zombies" value={moment.zombiesOnField.length} note={moment.alive !== null ? moment.alive + " per the game" : undefined} />
        <Value label="Speed" value={moment.wpm !== null ? Math.round(moment.wpm) + " WPM" : "-"} note="last 10 s" />
        <Value label="Position" value={moment.position ? Math.round(moment.position.x) + ", " + Math.round(moment.position.z) : "-"} note={moment.position ? "x, z (m)" : "not logged"} />
      </div>

      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <div>
          <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-neutral-500">Typing</h3>
          {moment.typing ? (
            <p className="text-sm text-neutral-800">
              <span className="font-mono font-semibold">{moment.typing.word}</span> at {moment.typing.target.replace(/_/g, " ")} ·{" "}
              {fmtT(moment.typing.start)} – {fmtT(moment.typing.end)} · {moment.typing.outcome.replace(/_/g, " ")}
            </p>
          ) : (
            <p className="text-sm text-neutral-500">No word attempt at this instant.</p>
          )}
          {s && length ? (
            <p className="mt-0.5 text-xs text-neutral-500">
              Game state: {letters ?? 0} of {length} letters of {dataStr(s, "typing_target")?.replace(/_/g, " ")}
            </p>
          ) : null}
        </div>
        <div>
          <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-neutral-500">Powers</h3>
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-neutral-800">
            {powers.map(([label, value]) => (
              <span key={label}>
                <span className="text-neutral-500">{label}</span> <span className="font-medium tabular-nums">{value ?? "-"}</span>
              </span>
            ))}
          </div>
          {flags.length > 0 ? <p className="mt-1 text-xs text-neutral-600">{flags.join(" · ")}</p> : null}
        </div>
      </div>

      <div className="mt-4">
        <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-neutral-500">
          Zombies on the field ({moment.zombiesOnField.length})
        </h3>
        {moment.zombiesOnField.length === 0 ? (
          <p className="text-sm text-neutral-500">None.</p>
        ) : (
          <div className="flex max-h-28 flex-wrap gap-1.5 overflow-y-auto">
            {moment.zombiesOnField.map((z: ZombieBar) => (
              <button
                key={z.key}
                type="button"
                onClick={() => onSelectZombie(z.key === selectedZombie ? null : z.key)}
                title={kindLabel(z.kind) + " · appeared " + fmtT(z.spawnT) + " · " + z.outcome + " at " + fmtT(z.endT)}
                className={
                  "inline-flex items-center gap-1.5 rounded-full border bg-white px-2 py-0.5 text-xs text-neutral-800 " +
                  (z.key === selectedZombie ? "border-neutral-900" : "border-black/10 hover:border-black/30")
                }
              >
                <Dot color={kindColor(z.kind)} />
                <span className="font-mono">{z.word || "?"}</span>
                {z.wasTargeted ? null : <span className="text-neutral-400">untyped</span>}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="mt-4">
        <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-neutral-500">
          Events within ±{windowSeconds} s ({moment.nearby.length})
        </h3>
        {moment.nearby.length === 0 ? (
          <p className="text-sm text-neutral-500">No events.</p>
        ) : (
          <ul className="max-h-64 overflow-y-auto pr-1">
            {moment.nearby.map((e) => {
              const delta = tOf(e) - moment.t;
              const fatal = e.event === "damage" && dataBool(e, "fatal");
              return (
                <li key={e.id}>
                  <button
                    type="button"
                    onClick={() => onSelectT(tOf(e))}
                    className="flex w-full items-center gap-2 rounded px-1 py-0.5 text-left text-[13px] text-neutral-800 hover:bg-black/[0.04]"
                  >
                    <span className="w-12 shrink-0 text-right text-xs tabular-nums text-neutral-500">
                      {delta >= 0 ? "+" : "−"}
                      {Math.abs(delta).toFixed(1)} s
                    </span>
                    <Dot color={eventColor(e)} />
                    <span className={"min-w-0 truncate " + (fatal ? "font-semibold" : "")}>{describeEvent(e)}</span>
                    {fatal ? (
                      <span className="ml-auto shrink-0 rounded px-1 text-[11px] font-medium text-white" style={{ backgroundColor: STATUS.critical }}>
                        fatal
                      </span>
                    ) : null}
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {s ? (
        <p className="mt-3 text-xs text-neutral-500">
          Powers and typing state as of the last event carrying player state: {s.event} at {fmtT(tOf(s))}
          {stateAge !== null && stateAge > 0.05 ? " (" + stateAge.toFixed(1) + " s earlier)" : ""}.
        </p>
      ) : null}
    </Card>
  );
}
