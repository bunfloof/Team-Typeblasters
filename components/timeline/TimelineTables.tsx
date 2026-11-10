"use client";
// The timeline's table twin: the numbers behind each lane (zombies, word
// attempts, wrong keys, phases, state series) as sortable, filterable,
// exportable tables. Row click selects that time on the timeline.
import { useMemo, useState } from "react";
import DataGrid from "@/components/DataGrid";
import { fmtT, type Timeline } from "@/lib/timelineData";

type Props = {
  timeline: Timeline;
  fileName: string;
  onSelectT: (t: number) => void;
};

type TabId = "zombies" | "words" | "wrongKeys" | "phases" | "series";

const round = (value: number | null) => (value === null ? null : Math.round(value * 1000) / 1000);

export default function TimelineTables({ timeline: tl, fileName, onSelectT }: Props) {
  const [tab, setTab] = useState<TabId>("zombies");

  const tables = useMemo(() => {
    const zombies = tl.zombies.map((z) => ({
      zombie_id: z.zombieId,
      kind: z.kind,
      words: z.words.join(" → "),
      appeared: fmtT(z.spawnT),
      t_spawn: round(z.spawnT),
      spawn_source: z.spawnSource,
      gone: z.ended ? fmtT(z.endT) : null,
      t_end: round(z.endT),
      seconds_on_field: round(z.endT - z.spawnT),
      outcome: z.outcome,
      was_targeted: z.wasTargeted,
      typing_starts: z.typingStarts,
      letters_typed: z.lettersTyped,
      word_length: z.wordLength,
      word_attempts: z.attempts.length,
      ambusher: z.ambusher,
      chain_linked: z.chainLinked,
      frenzy_word: z.frenzyWord,
      word_shifts: z.wordShifts,
      blast_radius: z.blastRadius,
      in_ring_at_first_letter: z.ring.atFirstLetter,
      in_ring_at_last_start: z.ring.atLastStart,
      in_ring_max_while_typed: z.ring.maxWhileTyped,
      in_ring_max: z.ring.max,
      spawn_x: z.spawnPos?.x ?? null,
      spawn_z: z.spawnPos?.z ?? null,
      end_x: z.endPos?.x ?? null,
      end_z: z.endPos?.z ?? null,
      wave: z.wave,
    }));
    const words = tl.words.map((w) => ({
      word: w.word,
      target: w.target,
      target_id: w.targetId,
      zombie: w.zombieKey,
      outcome: w.outcome,
      first_key: fmtT(w.start),
      t_start: round(w.start),
      start_source: w.startSource,
      t_end: round(w.end),
      typing_seconds: w.typingSeconds,
      correct_keys: w.correctKeys,
      wrong_keys: w.wrongKeys,
      rockets: w.rockets,
      words_finished: w.wordsFinished,
      candidates: w.candidates,
      case_sensitive: w.caseSensitive,
      word_style: w.wordStyle,
      frenzy_on: w.frenzyOn,
      key_times: w.keyTimes ? w.keyTimes.map((t) => round(t)).join(",") : null,
      seq: w.event.seq,
    }));
    const wrongKeys = tl.wrongKeys.map((k) => ({
      time: fmtT(k.t),
      t_play: k.t,
      typed: k.typed,
      expected: k.expected,
      case_only: k.caseOnly,
      stray: k.stray,
      target: k.target,
      target_id: k.targetId,
      letter_index: k.letterIndex,
      word_length: k.wordLength,
      seq: k.event.seq,
    }));
    const phases = tl.phases.map((p) => ({
      phase: p.label,
      kind: p.kind,
      wave: p.wave,
      from: fmtT(p.start),
      to: fmtT(p.end),
      t_start: round(p.start),
      t_end: round(p.end),
      seconds: round(p.end - p.start),
      ended_by: p.endedBy,
    }));
    // All state series on one time grid (each column holds its last value).
    const times = [...new Set([...tl.health, ...tl.combo, ...tl.alive, ...tl.score].map((p) => p.t))].sort((a, b) => a - b);
    const cursors = { health: 0, combo: 0, alive: 0, score: 0 };
    const current: Record<keyof typeof cursors, number | null> = { health: null, combo: null, alive: null, score: null };
    const lists = { health: tl.health, combo: tl.combo, alive: tl.alive, score: tl.score };
    const series = times.map((t) => {
      for (const name of Object.keys(cursors) as (keyof typeof cursors)[]) {
        const list = lists[name];
        while (cursors[name] < list.length && list[cursors[name]].t <= t) {
          current[name] = list[cursors[name]].v;
          cursors[name]++;
        }
      }
      return { time: fmtT(t), t_play: t, health: current.health, combo: current.combo, zombies_alive: current.alive, score: current.score };
    });
    return { zombies, words, wrongKeys, phases, series };
  }, [tl]);

  const tabs: { id: TabId; label: string; count: number }[] = [
    { id: "zombies", label: "Zombies", count: tables.zombies.length },
    { id: "words", label: "Word attempts", count: tables.words.length },
    { id: "wrongKeys", label: "Wrong keys", count: tables.wrongKeys.length },
    { id: "phases", label: "Phases", count: tables.phases.length },
    { id: "series", label: "Health · combo · zombies alive", count: tables.series.length },
  ];
  const rows: Record<string, unknown>[] = tables[tab];
  const timeColumn: Record<TabId, string> = { zombies: "t_spawn", words: "t_start", wrongKeys: "t_play", phases: "t_start", series: "t_play" };

  return (
    <div>
      <div className="mb-2 flex flex-wrap gap-1">
        {tabs.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            className={
              "rounded-md px-2.5 py-1 text-sm " + (tab === item.id ? "bg-neutral-900 text-white" : "text-neutral-600 hover:bg-black/[0.04]")
            }
          >
            {item.label} <span className="tabular-nums opacity-70">{item.count}</span>
          </button>
        ))}
      </div>
      <DataGrid
        key={tab}
        rows={rows}
        height={420}
        fileName={fileName + "-" + tab}
        onRowClicked={(row) => {
          const t = row[timeColumn[tab]];
          if (typeof t === "number") onSelectT(t);
        }}
      />
    </div>
  );
}
