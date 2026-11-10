"use client";
// Typing: speed, accuracy, and which keys go wrong.
import { useMemo, useState } from "react";
import type { ColDef } from "ag-grid-community";
import ChartCard from "@/components/ChartCard";
import DataGrid from "@/components/DataGrid";
import { Card, EmptyState, StatTile } from "@/components/ui";
import { fmt1, fmtInt, fmtPct } from "@/lib/format";
import { SERIES } from "@/lib/theme";
import { barOption, histogramOption, tipHtml } from "./charts";
import { WORD_OUTCOME_LABELS, keyLabel, labelOf } from "./labels";
import { N, Section, Segmented } from "./parts";
import { binCounts, evenBins, median, share } from "./stats";
import type { InsightsResponse } from "./types";

type Typing = InsightsResponse["typing"];

export default function TypingSection({ data }: { data: Typing }) {
  const { wpm, accuracy } = useMemo(
    () => ({
      wpm: data.runs.map((r) => r.wpm).filter((v): v is number => v !== null),
      // 100% goes in the last bin (95-100) instead of a bin of its own.
      accuracy: data.runs.map((r) => r.accuracy).filter((v): v is number => v !== null).map((a) => Math.min(a, 99.999)),
    }),
    [data.runs],
  );
  const attempts = data.attempts.reduce((sum, a) => sum + a.count, 0);
  const finished = data.attempts.find((a) => a.outcome === "finished")?.count ?? 0;
  const keys = data.correctKeys + data.wrongKeysRunEnd;
  return (
    <Section
      id="typing"
      label="Typing"
      title="How fast and how accurately do players type?"
      intro="Speed and accuracy per run, how speed changes with word length, and which keys go wrong."
    >
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        <StatTile label="Median WPM" value={fmt1(median(wpm))} note={"per run, n = " + fmtInt(wpm.length)} />
        <StatTile label="Median accuracy" value={accuracy.length ? fmt1(median(accuracy)) + "%" : "-"} note="correct / all keys" />
        <StatTile label="Words finished" value={fmtPct(attempts ? share(finished, attempts) : null)} note={"of " + fmtInt(attempts) + " typing attempts"} />
        <StatTile label="Wrong keys per 100" value={keys ? fmt1((data.wrongKeysRunEnd / keys) * 100) : "-"} note="keys typed, ended runs" />
        <StatTile
          label="Case-only mistakes"
          value={fmtPct(data.wrongKeys.total ? share(data.wrongKeys.caseOnly, data.wrongKeys.total) : null)}
          note="right letter, wrong case"
        />
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <RunHistogram
          title="Words per minute, per run"
          values={wpm}
          width={5}
          unit="WPM"
          fileName="wpm-per-run"
          footnote="(correct keys / 5) per minute of play, rides included, so lower than a typing-test WPM. One run = one count."
        />
        <RunHistogram
          title="Accuracy, per run"
          values={accuracy}
          width={5}
          maxBins={20}
          floor={0}
          unit="%"
          fileName="accuracy-per-run"
          footnote="Correct keys / all keys typed in the run, from 0% to 100% in steps of 5 (the last bar includes 100%). Runs where no key was pressed are left out of both charts."
        />
      </div>

      <div className="grid gap-4 xl:grid-cols-5">
        <SpeedByLength rows={data.speedByLength} className="xl:col-span-3" />
        <AttemptOutcomes rows={data.attempts} className="xl:col-span-2" />
      </div>

      <div className="grid gap-4 xl:grid-cols-5">
        <WrongKeyKinds data={data.wrongKeys} className="xl:col-span-2" />
        <WrongPairs rows={data.wrongPairs} className="xl:col-span-3" />
      </div>
    </Section>
  );
}

function RunHistogram({
  title,
  values,
  width,
  maxBins,
  floor,
  unit,
  fileName,
  footnote,
}: {
  title: string;
  values: number[];
  width: number;
  maxBins?: number;
  floor?: number; // first bin edge (default: just below the smallest value)
  unit: string;
  fileName: string;
  footnote: string;
}) {
  const { labels, counts } = useMemo(() => {
    const bins = evenBins(values, width, maxBins, floor);
    return { labels: bins.map((b) => b.label), counts: binCounts(values, bins) };
  }, [values, width, maxBins, floor]);
  const option = useMemo(() => histogramOption(labels, counts, { noun: "Runs", categoryName: unit, unit }), [labels, counts, unit]);
  return (
    <ChartCard
      title={title}
      subtitle={
        <>
          <N n={values.length} noun="ended runs with keys typed" /> · median {fmt1(median(values))}
        </>
      }
      option={option}
      rows={labels.map((label, i) => ({ [unit]: label, runs: counts[i] }))}
      height={260}
      fileName={fileName}
      empty={values.length === 0}
      emptyText="No ended runs for these filters."
      footnote={footnote}
    />
  );
}

function SpeedByLength({ rows, className }: { rows: Typing["speedByLength"]; className?: string }) {
  const kinds = (["plain", "case-sensitive"] as const).filter((kind) => rows.some((r) => r.case === kind));
  const [picked, setPicked] = useState<"plain" | "case-sensitive">("plain");
  const kind = kinds.includes(picked) ? picked : (kinds[0] ?? "plain");
  const shown = useMemo(() => rows.filter((r) => r.case === kind), [rows, kind]);
  const option = useMemo(
    () =>
      barOption({
        categories: shown.map((r) => r.word_length + "\nn = " + r.words),
        values: shown.map((r) => r.mean),
        colors: SERIES[0],
        valueName: "Letters per second",
        categoryName: "Letters in the word",
        format: (v) => fmt1(v),
        intervals: shown.map((r) => ({ low: Math.max(0, r.mean - (r.ci95 ?? 0)), high: r.mean + (r.ci95 ?? 0) })),
        tooltip: (i) =>
          tipHtml(shown[i].word_length + "-letter words", [
            ["Mean letters per second", fmt1(shown[i].mean)],
            ["95% interval", shown[i].ci95 === null ? "-" : "± " + fmt1(shown[i].ci95)],
            ["Median", fmt1(shown[i].median)],
            ["Words", fmtInt(shown[i].words)],
          ]),
      }),
    [shown],
  );
  const total = shown.reduce((sum, r) => sum + r.words, 0);
  return (
    <ChartCard
      className={className}
      title="Typing speed by word length"
      subtitle={<N n={total} noun="finished words" />}
      option={option}
      rows={rows.map((r) => ({ word_length: r.word_length, case: r.case, words: r.words, mean_letters_per_second: Math.round(r.mean * 100) / 100, median: Math.round(r.median * 100) / 100, ci95: r.ci95 === null ? null : Math.round(r.ci95 * 100) / 100 }))}
      height={280}
      fileName="speed-by-word-length"
      empty={rows.length === 0}
      emptyText="No finished words for these filters."
      actions={
        kinds.length > 1 ? (
          <Segmented
            value={kind}
            options={kinds.map((k) => ({ value: k, label: k === "plain" ? "Any case" : "Case-sensitive" }))}
            onChange={setPicked}
            ariaLabel="Word kind"
          />
        ) : null
      }
      footnote="(correct keys − 1) / seconds from the first to the last key, for finished words of 2+ keys. Whiskers: 95% interval of the mean. Case-sensitive words (Hard level, orbs) are shown separately."
    />
  );
}

function AttemptOutcomes({ rows, className }: { rows: Typing["attempts"]; className?: string }) {
  const total = rows.reduce((sum, r) => sum + r.count, 0);
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => labelOf(WORD_OUTCOME_LABELS, r.outcome)),
        values: rows.map((r) => r.count),
        colors: SERIES[0],
        horizontal: true,
        valueName: "Typing attempts",
        barLabel: (i) => fmtPct(total ? rows[i].count / total : 0),
        tooltip: (i) =>
          tipHtml(labelOf(WORD_OUTCOME_LABELS, rows[i].outcome), [
            ["Attempts", fmtInt(rows[i].count)],
            ["Share", fmtPct(total ? rows[i].count / total : 0, 1)],
          ]),
      }),
    [rows, total],
  );
  return (
    <ChartCard
      className={className}
      title="How typing attempts end"
      subtitle={<N n={total} noun="attempts" />}
      option={option}
      rows={rows.map((r) => ({ outcome: labelOf(WORD_OUTCOME_LABELS, r.outcome), key: r.outcome, attempts: r.count }))}
      height={Math.max(200, rows.length * 34 + 60)}
      fileName="typing-attempt-outcomes"
      empty={rows.length === 0}
      footnote="An attempt runs from the first letter of a word until it is finished, dropped with Backspace, or its target disappears (blown up, bit the player...)."
    />
  );
}

function WrongKeyKinds({ data, className }: { data: Typing["wrongKeys"]; className?: string }) {
  const rows = useMemo(
    () => [
      { kind: "Stray key (matches no word)", count: data.stray },
      { kind: "Wrong letter inside a word", count: data.midWord },
      { kind: "Right letter, wrong case", count: data.caseOnly },
    ],
    [data],
  );
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => r.kind),
        values: rows.map((r) => r.count),
        colors: SERIES[0],
        horizontal: true,
        valueName: "Wrong keys",
        barLabel: (i) => fmtPct(data.total ? rows[i].count / data.total : 0),
        tooltip: (i) =>
          tipHtml(rows[i].kind, [
            ["Wrong keys", fmtInt(rows[i].count)],
            ["Share", fmtPct(data.total ? rows[i].count / data.total : 0, 1)],
          ]),
      }),
    [rows, data.total],
  );
  return (
    <ChartCard
      className={className}
      title="Kinds of wrong keys"
      subtitle={<N n={data.total} noun="wrong keys" />}
      option={option}
      rows={rows}
      height={200}
      fileName="wrong-key-kinds"
      empty={data.total === 0}
      emptyText="No wrong keys for these filters."
      footnote="Stray = typed while no word was started, and no word starts with that letter even ignoring case (often aiming at a word that is not there). Right letter, wrong case: inside a case-sensitive word, or its first letter (e.g. h for Hunter)."
    />
  );
}

const PAIR_COLUMNS: ColDef[] = [
  { field: "expected", headerName: "Needed", valueFormatter: (p) => keyLabel(String(p.value ?? "")), minWidth: 110 },
  { field: "typed", headerName: "Typed", valueFormatter: (p) => keyLabel(String(p.value ?? "")), minWidth: 110 },
  { field: "count", headerName: "Times", sort: "desc", minWidth: 90 },
  { field: "share_pct", headerName: "% of these slips", minWidth: 150 },
  { field: "case_only", headerName: "Case only", minWidth: 110 },
];

function WrongPairs({ rows, className }: { rows: Typing["wrongPairs"]; className?: string }) {
  const tableRows = useMemo(
    () => rows.map((r) => ({ expected: r.expected, typed: r.typed, count: r.count, share_pct: Math.round(r.share * 1000) / 10, case_only: r.case_only })),
    [rows],
  );
  return (
    <Card
      className={className}
      title="Most common slips: the key needed vs the key typed"
      subtitle={<N n={rows.reduce((sum, r) => sum + r.count, 0)} noun="wrong keys inside words (top 60 pairs)" />}
    >
      {rows.length === 0 ? (
        <EmptyState>No wrong keys inside words for these filters.</EmptyState>
      ) : (
        <DataGrid rows={tableRows} columnDefs={PAIR_COLUMNS} height={300} fileName="wrong-key-pairs" />
      )}
      <p className="mt-3 text-xs leading-relaxed text-neutral-500">
        Letters are shown in capitals when case did not matter. Neighbouring keys (e.g. T→Y) point to fat fingers; similar shapes or sounds to misreading the
        word.
      </p>
    </Card>
  );
}
