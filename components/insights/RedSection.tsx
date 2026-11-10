"use client";
// Question 2: do players use the red-zombie blast ring well?
import { useMemo } from "react";
import type { EChartsOption } from "echarts";
import ChartCard from "@/components/ChartCard";
import { StatTile } from "@/components/ui";
import { fmt1, fmtInt, fmtPct, shortId } from "@/lib/format";
import { INK, KIND_COLORS, SERIES } from "@/lib/theme";
import { barOption, histogramOption, itemTooltip, slotAxis, tipHtml } from "./charts";
import { BLAST_SOURCE_LABELS, RED_OUTCOME_LABELS, labelOf } from "./labels";
import { N, Section } from "./parts";
import { jitter, mean, share } from "./stats";
import type { InsightsResponse } from "./types";

type Red = InsightsResponse["red"];
const RED = KIND_COLORS.explosive;

export default function RedSection({ data }: { data: Red }) {
  const word = data.outcomes.find((o) => o.outcome === "word");
  const grew = data.blasts.filter((b) => b.at_blast > b.at_start).length;
  const alone = data.killsPerRedBlast.filter((k) => k <= 1).length;
  return (
    <Section
      id="red"
      label="Question 2"
      title="Do players use the red-zombie blast ring well?"
      intro="A red zombie's word blows it up, killing every zombie inside its orange ring. Playing it well means waiting until the ring is full before finishing the word."
    >
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        <StatTile label="Red zombies met" value={fmtInt(data.redZombies)} />
        <StatTile label="Blown up by their word" value={fmtPct(word?.share ?? null)} note="the rest were chained, bit, or got away" />
        <StatTile label="Kills per red blast" value={fmt1(mean(data.killsPerRedBlast))} note={"mean of " + fmtInt(data.redBlasts) + " blasts (itself included)"} />
        <StatTile label="Ring filled up while typing" value={fmtPct(data.blasts.length ? share(grew, data.blasts.length) : null)} note="more zombies at the blast than at the start" />
        <StatTile label="Blasts that killed only itself" value={fmtPct(data.killsPerRedBlast.length ? share(alone, data.killsPerRedBlast.length) : null)} />
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <RedOutcomes rows={data.outcomes} total={data.redZombies} />
        <RingScatter blasts={data.blasts} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
        <KillsHistogram kills={data.killsPerRedBlast} />
        <ByRunNumber rows={data.byRunNumber} />
        <BySource rows={data.bySource} />
      </div>
    </Section>
  );
}

function RedOutcomes({ rows, total }: { rows: Red["outcomes"]; total: number }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => labelOf(RED_OUTCOME_LABELS, r.outcome)),
        values: rows.map((r) => r.zombies),
        colors: RED,
        horizontal: true,
        valueName: "Red zombies",
        barLabel: (i) => fmtPct(rows[i].share),
        tooltip: (i) =>
          tipHtml(labelOf(RED_OUTCOME_LABELS, rows[i].outcome), [
            ["Red zombies", fmtInt(rows[i].zombies)],
            ["Share", fmtPct(rows[i].share, 1)],
          ]),
      }),
    [rows],
  );
  return (
    <ChartCard
      title="What happened to red zombies"
      subtitle={<N n={total} noun="red zombies" />}
      option={option}
      rows={rows.map((r) => ({ outcome: labelOf(RED_OUTCOME_LABELS, r.outcome), key: r.outcome, zombies: r.zombies, share_pct: Math.round(r.share * 1000) / 10 }))}
      height={Math.max(220, rows.length * 34 + 60)}
      fileName="red-zombie-outcomes"
      empty={rows.length === 0}
      emptyText="No red zombies for these filters."
      footnote="“Blown up by its word” = the player finished its word (the intended play). “Chained” = another explosion set it off. Bitten players let it walk all the way in."
    />
  );
}

function RingScatter({ blasts }: { blasts: Red["blasts"] }) {
  const option = useMemo<EChartsOption>(() => {
    const top = Math.max(3, ...blasts.map((b) => Math.max(b.at_start, b.at_blast))) + 1;
    return {
      animationDuration: 300,
      grid: { left: 40, right: 16, top: 16, bottom: 36 },
      xAxis: { ...slotAxis(top, String), name: "Zombies in the ring when the word was started", nameLocation: "middle", nameGap: 24 },
      yAxis: { ...slotAxis(top, String), name: "...when it blew up", nameLocation: "middle", nameGap: 28 },
      tooltip: {
        ...itemTooltip,
        formatter: (params) => {
          const p = Array.isArray(params) ? params[0] : params;
          const b = blasts[p.dataIndex];
          return b
            ? tipHtml("Red blast", [
                ["In the ring at the start", fmtInt(b.at_start)],
                ["In the ring at the blast", fmtInt(b.at_blast)],
                ["Kills (itself included)", fmtInt(b.kills)],
                ["Typing time", b.typing_seconds === null ? "-" : fmt1(b.typing_seconds) + " s"],
                ["Run", shortId(b.run_id) + (b.run_number ? " (run #" + b.run_number + ")" : "")],
              ])
            : "";
        },
      },
      series: [
        {
          type: "scatter",
          name: "Red blasts",
          symbolSize: 9,
          itemStyle: { color: RED, opacity: 0.7, borderColor: INK.surface, borderWidth: 1.5 },
          data: blasts.map((b, i) => [b.at_start + jitter(i, 1) * 0.22, b.at_blast + jitter(i, 2) * 0.22]),
          markLine: {
            silent: true,
            symbol: "none",
            lineStyle: { color: INK.muted, width: 1, type: "solid" },
            label: { formatter: "no change", color: INK.muted, fontSize: 11, position: "insideEndTop" },
            data: [[{ coord: [-0.5, -0.5] }, { coord: [top - 0.5, top - 0.5] }]],
          },
        },
      ],
    };
  }, [blasts]);
  return (
    <ChartCard
      title="Did players wait for the ring to fill?"
      subtitle={<N n={blasts.length} noun="red blasts" />}
      option={option}
      rows={blasts.map((b) => ({ run_id: b.run_id, run_number: b.run_number, in_ring_at_start: b.at_start, in_ring_at_blast: b.at_blast, kills: b.kills, typing_seconds: b.typing_seconds }))}
      height={320}
      fileName="red-blast-ring"
      empty={blasts.length === 0}
      emptyText="No red blasts with ring counts for these filters."
      footnote="One dot per red zombie the player blew up (dots are nudged apart so equal values stay visible). Above the line: more zombies walked into the ring while the word was typed (good timing). On or below it: the word was finished without waiting."
    />
  );
}

function KillsHistogram({ kills }: { kills: number[] }) {
  const { labels, counts } = useMemo(() => {
    const min = kills.length ? Math.min(...kills) : 0;
    const max = Math.max(min, ...kills);
    const values = Array.from({ length: max - min + 1 }, (_, k) => min + k);
    return { labels: values.map(String), counts: values.map((k) => kills.filter((v) => v === k).length) };
  }, [kills]);
  const option = useMemo(
    () => histogramOption(labels, counts, { color: RED, noun: "Red blasts", categoryName: "Kills in one blast", unit: "kills" }),
    [labels, counts],
  );
  return (
    <ChartCard
      title="Kills per red blast"
      subtitle={<N n={kills.length} noun="red blasts" />}
      option={option}
      rows={labels.map((label, i) => ({ kills: Number(label), blasts: counts[i] }))}
      height={260}
      fileName="kills-per-red-blast"
      empty={kills.length === 0}
      footnote="A red zombie counts itself, so 1 kill = the blast caught nobody else."
    />
  );
}

function ByRunNumber({ rows }: { rows: Red["byRunNumber"] }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => "Run " + r.run_number + "\nn = " + r.blasts),
        values: rows.map((r) => r.mean_kills),
        colors: SERIES[0],
        valueName: "Mean kills per red blast",
        format: (v) => fmt1(v),
        intervals: rows.map((r) => ({ low: Math.max(0, r.mean_kills - (r.ci95 ?? 0)), high: r.mean_kills + (r.ci95 ?? 0) })),
        tooltip: (i) =>
          tipHtml("Run " + rows[i].run_number + " of the player", [
            ["Mean kills per red blast", fmt1(rows[i].mean_kills)],
            ["95% interval", rows[i].ci95 === null ? "-" : "± " + fmt1(rows[i].ci95)],
            ["Red blasts", fmtInt(rows[i].blasts)],
          ]),
      }),
    [rows],
  );
  return (
    <ChartCard
      title="Do players learn it? Kills per red blast by run number"
      subtitle="1 = the player's first run in this browser"
      option={option}
      rows={rows.map((r) => ({ run_number: r.run_number, red_blasts: r.blasts, mean_kills: Math.round(r.mean_kills * 100) / 100, ci95: r.ci95 === null ? null : Math.round(r.ci95 * 100) / 100 }))}
      height={260}
      fileName="red-kills-by-run-number"
      empty={rows.length === 0}
      footnote="Whiskers: 95% interval of the mean (wide = few blasts, read with care). A rise from run 1 to 4+ suggests players learn to wait."
    />
  );
}

function BySource({ rows }: { rows: Red["bySource"] }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => labelOf(BLAST_SOURCE_LABELS, r.source) + "\nn = " + r.blasts),
        values: rows.map((r) => r.mean_kills),
        colors: SERIES[0],
        valueName: "Mean kills per blast",
        format: (v) => fmt1(v),
        intervals: rows.map((r) => ({ low: Math.max(0, r.mean_kills - (r.ci95 ?? 0)), high: r.mean_kills + (r.ci95 ?? 0) })),
        tooltip: (i) =>
          tipHtml(labelOf(BLAST_SOURCE_LABELS, rows[i].source), [
            ["Mean kills per blast", fmt1(rows[i].mean_kills)],
            ["95% interval", rows[i].ci95 === null ? "-" : "± " + fmt1(rows[i].ci95)],
            ["Blasts", fmtInt(rows[i].blasts)],
            ["Total kills", fmtInt(rows[i].total_kills)],
          ]),
      }),
    [rows],
  );
  return (
    <ChartCard
      title="Kills per blast, by what set it off"
      subtitle={<N n={rows.reduce((sum, r) => sum + r.blasts, 0)} noun="blasts" />}
      option={option}
      rows={rows.map((r) => ({ source: labelOf(BLAST_SOURCE_LABELS, r.source), blasts: r.blasts, mean_kills: Math.round(r.mean_kills * 100) / 100, ci95: r.ci95 === null ? null : Math.round(r.ci95 * 100) / 100, total_kills: r.total_kills }))}
      height={260}
      fileName="kills-by-blast-source"
      empty={rows.length === 0}
      footnote="Kills of the whole chain reaction. A red zombie's own death counts as one of its kills."
    />
  );
}
