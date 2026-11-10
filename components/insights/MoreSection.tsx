"use client";
// More: the boss quiz, the tip boxes, pauses and hidden tabs.
import { useMemo } from "react";
import type { ColDef } from "ag-grid-community";
import type { EChartsOption } from "echarts";
import ChartCard from "@/components/ChartCard";
import DataGrid from "@/components/DataGrid";
import { Card, EmptyState } from "@/components/ui";
import { fmt1, fmtInt, fmtPct } from "@/lib/format";
import { AXIS_STYLE, BLUE_RAMP, INK, SERIES } from "@/lib/theme";
import { barOption, histogramOption, itemTooltip, slotAxis, tipHtml } from "./charts";
import { PAUSE_LABELS, labelOf, positionLabel } from "./labels";
import { N, Section } from "./parts";
import { binCounts, edgeBins, jitter, median } from "./stats";
import type { InsightsResponse } from "./types";

type More = InsightsResponse["more"];

export default function MoreSection({ data }: { data: More }) {
  return (
    <Section
      id="more"
      label="More"
      title="Boss quiz, tip boxes and pauses"
      intro="Which quiz questions are hard, whether tip boxes are read or skipped, and how often players step away."
    >
      <div className="grid gap-4 xl:grid-cols-5">
        <QuizTable rows={data.quiz} className="xl:col-span-3" />
        <QuizPositions rows={data.quizPositions} className="xl:col-span-2" />
      </div>
      <TipStrip tips={data.tips} points={data.tipPoints} />
      <div className="grid gap-4 xl:grid-cols-2">
        <Pauses rows={data.pauses} />
        <HiddenTabs seconds={data.hiddenSeconds} />
      </div>
    </Section>
  );
}

const QUIZ_COLUMNS: ColDef[] = [
  { field: "question_index", headerName: "#", minWidth: 70, maxWidth: 80 },
  { field: "question", headerName: "Question", tooltipField: "question", minWidth: 260, flex: 1 },
  { field: "asked", headerName: "Asked", minWidth: 90 },
  { field: "right", headerName: "Right", minWidth: 90 },
  { field: "wrong", headerName: "Wrong", minWidth: 90 },
  { field: "timeout", headerName: "Too slow", minWidth: 100 },
  { field: "right_pct", headerName: "% right", sort: "asc", minWidth: 100 },
  { field: "mean_seconds", headerName: "Mean seconds", minWidth: 130 },
];

function QuizTable({ rows, className }: { rows: More["quiz"]; className?: string }) {
  const tableRows = useMemo(
    () =>
      rows.map((q) => ({
        question_index: q.question_index,
        question: q.question,
        asked: q.asked,
        right: q.right,
        wrong: q.wrong,
        timeout: q.timeout,
        right_pct: Math.round(q.share_right * 1000) / 10,
        mean_seconds: q.mean_seconds === null ? null : Math.round(q.mean_seconds * 10) / 10,
      })),
    [rows],
  );
  const asked = rows.reduce((sum, q) => sum + q.asked, 0);
  return (
    <Card className={className} title="Boss quiz: right, wrong and too slow, by question" subtitle={<N n={asked} noun="answers" />}>
      {rows.length === 0 ? (
        <EmptyState>No quiz answers yet for these filters (players must reach the boss).</EmptyState>
      ) : (
        <DataGrid rows={tableRows} columnDefs={QUIZ_COLUMNS} height={300} fileName="boss-quiz" />
      )}
      <p className="mt-3 text-xs leading-relaxed text-neutral-500">
        Sorted with the hardest question first. A right answer hurts the boss; a wrong one heals it. Mean seconds = time to answer (the timer stops while
        frozen).
      </p>
    </Card>
  );
}

function QuizPositions({ rows, className }: { rows: More["quizPositions"]; className?: string }) {
  const option = useMemo<EChartsOption>(() => {
    const correct = [0, 1, 2];
    const chosen = [-1, 0, 1, 2];
    const countOf = (c: number, h: number) => rows.find((r) => r.correct_position === c && r.chosen_position === h)?.count ?? 0;
    const max = Math.max(1, ...rows.map((r) => r.count));
    const cells = correct.flatMap((c, x) =>
      chosen.map((h, y) => {
        const count = countOf(c, h);
        // Dark cells get white text, light cells ink.
        return { value: [x, y, count], label: { show: count > 0, color: count / max > 0.55 ? "#ffffff" : INK.primary } };
      }),
    );
    return {
      animationDuration: 300,
      grid: { left: 24, right: 8, top: 8, bottom: 34 },
      xAxis: {
        type: "category",
        data: correct.map(positionLabel),
        name: "Where the right answer was",
        nameLocation: "middle",
        nameGap: 24,
        ...AXIS_STYLE,
        splitLine: { show: false },
      },
      yAxis: {
        type: "category",
        data: chosen.map(positionLabel),
        name: "Player chose",
        nameLocation: "middle",
        nameGap: 58,
        inverse: true,
        ...AXIS_STYLE,
        splitLine: { show: false },
      },
      visualMap: { show: false, min: 0, max, inRange: { color: [BLUE_RAMP[0], BLUE_RAMP[10]] } },
      tooltip: {
        ...itemTooltip,
        formatter: (params) => {
          const p = Array.isArray(params) ? params[0] : params;
          const [x, y, count] = (p.value as number[]) ?? [0, 0, 0];
          return tipHtml("Right answer: " + positionLabel(correct[x]), [
            ["Player chose", positionLabel(chosen[y])],
            ["Answers", fmtInt(count)],
          ]);
        },
      },
      series: [
        {
          type: "heatmap",
          data: cells,
          label: { show: true, fontSize: 12 },
          itemStyle: { borderColor: INK.surface, borderWidth: 2, borderRadius: 4 },
        },
      ],
    };
  }, [rows]);
  const total = rows.reduce((sum, r) => sum + r.count, 0);
  return (
    <ChartCard
      className={className}
      title="Answer chosen vs where the right one was"
      subtitle={<N n={total} noun="answers" />}
      option={option}
      rows={rows.map((r) => ({ right_answer_at: positionLabel(r.correct_position), chosen: positionLabel(r.chosen_position), answers: r.count }))}
      height={300}
      fileName="quiz-positions"
      empty={rows.length === 0}
      emptyText="No quiz answers yet."
      footnote="Cells on the diagonal are right answers. A dark row (e.g. always Left) means players guess by position, not by reading."
    />
  );
}

const TIP_CAP = 30; // seconds; longer openings are drawn at the cap

function TipStrip({ tips, points }: { tips: More["tips"]; points: More["tipPoints"] }) {
  const option = useMemo<EChartsOption>(() => {
    const keys = tips.map((t) => t.key);
    const shown = points.filter((p) => keys.includes(p.key));
    return {
      animationDuration: 300,
      grid: { left: 8, right: 24, top: 8, bottom: 34 },
      xAxis: {
        type: "value",
        name: "Seconds the tip box was open (" + TIP_CAP + "+ drawn at " + TIP_CAP + ")",
        nameLocation: "middle",
        nameGap: 24,
        min: 0,
        max: TIP_CAP,
        ...AXIS_STYLE,
        axisLine: { ...AXIS_STYLE.axisLine, onZero: false },
      },
      yAxis: { ...slotAxis(keys.length, (slot) => keys[slot] ?? ""), inverse: true, splitLine: { show: false }, axisLine: { show: false } },
      tooltip: {
        ...itemTooltip,
        formatter: (params) => {
          const p = Array.isArray(params) ? params[0] : params;
          const point = shown[p.dataIndex];
          const tip = tips.find((t) => t.key === point?.key);
          return point && tip
            ? tipHtml("Tip: " + point.key, [
                ["Open for", fmt1(point.seconds) + " s"],
                ["Median for this tip", fmt1(tip.median_seconds) + " s"],
                ["Closed within 2 s", fmtPct(tip.share_within_2s)],
                ["Times closed", fmtInt(tip.closed)],
              ])
            : "";
        },
      },
      series: [
        {
          type: "scatter",
          symbolSize: 8,
          itemStyle: { color: SERIES[0], opacity: 0.55, borderColor: INK.surface, borderWidth: 1 },
          data: shown.map((p, i) => [Math.min(p.seconds, TIP_CAP), keys.indexOf(p.key) + jitter(i, 3) * 0.22]),
          markLine: {
            silent: true,
            symbol: "none",
            lineStyle: { color: INK.muted, width: 1, type: "solid" },
            label: { formatter: "2 s", color: INK.muted, fontSize: 11 },
            data: [{ xAxis: 2 }],
          },
        },
      ],
    };
  }, [tips, points]);
  const closed = tips.reduce((sum, t) => sum + t.closed, 0);
  const quick = tips.reduce((sum, t) => sum + t.share_within_2s * t.closed, 0);
  return (
    <ChartCard
      title="Tip boxes: read or skipped?"
      subtitle={
        <>
          <N n={closed} noun="tip boxes closed" /> · {fmtPct(closed ? quick / closed : null)} closed within 2 s
        </>
      }
      option={option}
      rows={tips.map((t) => ({ tip: t.key, closed: t.closed, median_seconds: Math.round(t.median_seconds * 10) / 10, closed_within_2s_pct: Math.round(t.share_within_2s * 1000) / 10 }))}
      height={Math.max(200, tips.length * 34 + 60)}
      fileName="tip-boxes"
      empty={tips.length === 0}
      emptyText="No tip boxes closed for these filters."
      footnote="One dot per tip box closed (real seconds it was open). Dots left of the 2 s line were almost certainly skipped without reading. Table view: median and share closed within 2 s per tip."
    />
  );
}

function Pauses({ rows }: { rows: More["pauses"] }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => labelOf(PAUSE_LABELS, r.reason)),
        values: rows.map((r) => r.pauses),
        colors: SERIES[0],
        horizontal: true,
        valueName: "Pauses",
        barLabel: (i) => fmtInt(rows[i].pauses),
        tooltip: (i) =>
          tipHtml(labelOf(PAUSE_LABELS, rows[i].reason), [
            ["Pauses", fmtInt(rows[i].pauses)],
            ["Median time paused", rows[i].median_paused_seconds === null ? "-" : fmt1(rows[i].median_paused_seconds) + " s"],
          ]),
      }),
    [rows],
  );
  return (
    <ChartCard
      title="Pauses, by reason"
      subtitle={<N n={rows.reduce((sum, r) => sum + r.pauses, 0)} noun="pauses" />}
      option={option}
      rows={rows.map((r) => ({ reason: labelOf(PAUSE_LABELS, r.reason), pauses: r.pauses, median_paused_seconds: r.median_paused_seconds, pauses_with_a_resume: r.with_resume }))}
      height={Math.max(180, rows.length * 34 + 60)}
      fileName="pauses"
      empty={rows.length === 0}
      emptyText="No pauses for these filters."
      footnote="Esc, the pause button, or the browser tab being hidden (switching tabs pauses the game). Hover for the median time paused, 3-2-1 countdown included."
    />
  );
}

const HIDDEN_BINS = edgeBins([0, 5, 15, 30, 60, 120, 300]);

function HiddenTabs({ seconds }: { seconds: number[] }) {
  const counts = useMemo(() => binCounts(seconds, HIDDEN_BINS), [seconds]);
  const option = useMemo(
    () => histogramOption(HIDDEN_BINS.map((b) => b.label), counts, { noun: "Times", categoryName: "Seconds away from the tab", unit: "s" }),
    [counts],
  );
  return (
    <ChartCard
      title="How long players left the tab"
      subtitle={
        <>
          <N n={seconds.length} noun="returns to the tab" /> · median {fmt1(median(seconds))} s
        </>
      }
      option={option}
      rows={HIDDEN_BINS.map((b, i) => ({ seconds_away: b.label, times: counts[i] }))}
      height={240}
      fileName="tab-hidden-seconds"
      empty={seconds.length === 0}
      emptyText="Nobody switched away from the tab with these filters."
      footnote="Seconds between the tab being hidden and coming back (page_visible). Long absences during a run usually mean the player got distracted, not stuck."
    />
  );
}
