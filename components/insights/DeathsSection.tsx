"use client";
// Question 1: where and why do players die?
import { useMemo, useState } from "react";
import type { EChartsOption } from "echarts";
import ChartCard from "@/components/ChartCard";
import { StatTile } from "@/components/ui";
import { fmt1, fmtInt, fmtPct, shortId } from "@/lib/format";
import { AXIS_STYLE, FONT, INK, LEGEND_STYLE, OUTCOME_COLORS } from "@/lib/theme";
import { levelName } from "@/lib/catalog";
import { barOption, fmtPctAxis, itemTooltip, multiBarOption, rampColors, slotAxis, tipHtml } from "./charts";
import { KILLER_ORDER, killerColor, killerLabel, sourceKiller, targetLabel } from "./labels";
import { N, Section, Segmented } from "./parts";
import { jitter } from "./stats";
import type { FunnelStep, InsightsResponse } from "./types";

type Deaths = InsightsResponse["deaths"];
const NO_STEPS: FunnelStep[] = [];

export default function DeathsSection({ data }: { data: Deaths }) {
  const t = data.tiles;
  // One funnel per level (wave numbers differ between levels).
  const funnels = useMemo(() => {
    const levels = [...new Set(data.funnel.map((step) => step.level))];
    return levels.map((level) => ({ level, steps: data.funnel.filter((step) => step.level === level) }));
  }, [data.funnel]);
  return (
    <Section
      id="deaths"
      label="Question 1"
      title="Where and why do players die?"
      intro="How far runs get, which fights and which zombies end them, and what the player was doing when the fatal hit landed."
    >
      <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        <StatTile label="Runs" value={fmtInt(t.runs)} />
        <StatTile label="Deaths" value={fmtInt(t.deaths)} note="runs that ended in a death" />
        <StatTile label="Death rate" value={fmtPct(t.deathRate)} note="deaths / runs" />
        <StatTile label="Median wave reached" value={fmt1(t.medianWave)} note="highest wave of each run" />
        <StatTile
          label="Deaths while typing the attacker"
          value={fmtPct(t.typingAttacker)}
          note={"of " + fmtInt(t.typingAttackerN) + " fatal hits"}
        />
      </div>

      <div className={"grid gap-4 " + (funnels.length > 1 ? "xl:grid-cols-2" : "")}>
        {funnels.length === 0 ? (
          <FunnelCard level="" steps={NO_STEPS} />
        ) : (
          funnels.map((funnel) => <FunnelCard key={funnel.level} level={funnel.level} steps={funnel.steps} />)
        )}
      </div>

      <div className="grid gap-4 xl:grid-cols-5">
        <DeathsByStage rows={data.byStage} className="xl:col-span-3" />
        <TypingAtDeath rows={data.typingAtDeath} className="xl:col-span-2" />
      </div>

      <HealthAtFightStart points={data.healthPoints} summary={data.healthSummary} />

      <div className="grid gap-4 xl:grid-cols-2">
        <DamageSources rows={data.damageSources} />
        <DeathMap rows={data.deathPositions} />
      </div>
    </Section>
  );
}

function FunnelCard({ level, steps }: { level: string; steps: FunnelStep[] }) {
  const levelRuns = steps[0]?.level_runs ?? 0;
  const resumed = steps[0]?.resumed_runs ?? 0;
  const option = useMemo(
    () =>
      barOption({
        categories: steps.map((s) => s.step),
        values: steps.map((s) => s.share),
        colors: rampColors(steps.length),
        horizontal: true,
        format: fmtPctAxis,
        max: 1,
        valueName: "Share of runs that got this far",
        barLabel: (i) => fmtPct(steps[i].share),
        tooltip: (i) =>
          tipHtml(steps[i].step, [
            ["Runs that got here", fmtInt(steps[i].runs)],
            ["Share of runs", fmtPct(steps[i].share, 1)],
            ["Stopped just before", fmtInt(steps[i].lost_since_previous)],
          ]),
      }),
    [steps],
  );
  const rows = steps.map((s) => ({
    level: s.level_name,
    step: s.step,
    runs: s.runs,
    share_pct: Math.round(s.share * 1000) / 10,
    stopped_just_before: s.lost_since_previous,
  }));
  return (
    <ChartCard
      title={"How far runs get" + (level ? " · " + levelName(level) : "")}
      subtitle={
        <>
          <N n={levelRuns} noun="runs" />
          {resumed > 0 ? " · " + fmtInt(resumed) + " went on from a checkpoint, not counted" : ""}
        </>
      }
      option={option}
      rows={rows}
      height={Math.max(220, steps.length * 26 + 56)}
      fileName={"progress-funnel-" + levelName(level).toLowerCase()}
      empty={steps.length === 0}
      footnote="Share of runs that started each ride and each fight (ride N leads to wave N; the last fight is the boss). The biggest drop between two bars is where most runs end, by death, Restart or a closed tab. A run that went on from a checkpoint (Restart after a death) starts at a later wave, so it is left out here; its deaths still count in the other charts."
    />
  );
}

function DeathsByStage({ rows, className }: { rows: Deaths["byStage"]; className?: string }) {
  const option = useMemo(() => {
    const stages = [...new Map(rows.map((r) => [r.stage, r.order])).entries()].sort((a, b) => a[1] - b[1]).map(([stage]) => stage);
    const killers = KILLER_ORDER.filter((k) => rows.some((r) => r.killer === k));
    return multiBarOption({
      categories: stages,
      horizontal: true,
      stacked: true,
      valueName: "Deaths",
      series: killers.map((k) => ({
        name: killerLabel(k),
        color: killerColor(k),
        values: stages.map((stage) => rows.find((r) => r.stage === stage && r.killer === k)?.deaths ?? 0),
      })),
    });
  }, [rows]);
  const stageCount = new Set(rows.map((r) => r.stage)).size;
  const total = rows.reduce((sum, r) => sum + r.deaths, 0);
  return (
    <ChartCard
      className={className}
      title="Where deaths happen, and who dealt the fatal hit"
      subtitle={<N n={total} noun="deaths" />}
      option={option}
      rows={rows.map((r) => ({ stage: r.stage, killer: killerLabel(r.killer), deaths: r.deaths }))}
      height={Math.max(240, stageCount * 30 + 80)}
      fileName="deaths-by-stage"
      empty={rows.length === 0}
      emptyText="No deaths for these filters."
      footnote="Each bar is one part of the level (Ride N = on the rails towards wave N). Segments: the kind of zombie (or the boss's energy orb) whose hit was fatal."
    />
  );
}

function TypingAtDeath({ rows, className }: { rows: Deaths["typingAtDeath"]; className?: string }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => targetLabel(r.target)),
        values: rows.map((r) => r.deaths),
        horizontal: true,
        valueName: "Deaths",
        barLabel: (i) => fmtInt(rows[i].deaths),
        tooltip: (i) =>
          tipHtml(targetLabel(rows[i].target), [
            ["Deaths", fmtInt(rows[i].deaths)],
            ["...while typing the attacker itself", fmtInt(rows[i].typing_attacker)],
          ]),
      }),
    [rows],
  );
  const total = rows.reduce((sum, r) => sum + r.deaths, 0);
  return (
    <ChartCard
      className={className}
      title="What the player was typing at the fatal hit"
      subtitle={<N n={total} noun="deaths" />}
      option={option}
      rows={rows.map((r) => ({ typing: targetLabel(r.target), target: r.target, deaths: r.deaths, typing_the_attacker: r.typing_attacker }))}
      height={Math.max(220, rows.length * 30 + 60)}
      fileName="typing-at-death"
      empty={rows.length === 0}
      emptyText="No deaths for these filters."
      footnote="“Nothing” = no word was started: the player was not typing when the hit landed. Hover a bar to see how often the player was typing the very zombie that bit them."
    />
  );
}

const RESULTS = [
  { key: "cleared" as const, title: "Fights the run cleared", color: OUTCOME_COLORS.won },
  { key: "died" as const, title: "Fights the run died in", color: OUTCOME_COLORS.lost },
];

// "Normal · Wave 3" -> "N·W3" when many fights share a narrow axis.
function shortFight(label: string): string {
  return label.replace(/^(\w)\w* · /, "$1·").replace("Wave ", "W").replace("Ride ", "R");
}

function HealthAtFightStart({ points, summary }: { points: Deaths["healthPoints"]; summary: Deaths["healthSummary"] }) {
  const option = useMemo<EChartsOption>(() => {
    const fights = [...new Map([...points, ...summary].map((p) => [p.fight, p.order])).entries()]
      .sort((a, b) => a[1] - b[1])
      .map(([fight]) => fight);
    const axisLabels = fights.length > 6 ? fights.map(shortFight) : fights;
    const indexOf = (fight: string) => fights.indexOf(fight);
    const pointSets = RESULTS.map((r) => points.filter((p) => p.result === r.key));
    const meanSets = RESULTS.map((r) => summary.filter((s) => s.result === r.key));
    return {
      animationDuration: 300,
      title: RESULTS.map((r, i) => ({
        text: r.title + ": health at the start  (n = " + pointSets[i].length + ")",
        left: i === 0 ? 0 : "52%",
        top: 0,
        textStyle: { fontSize: 12, fontWeight: 500, color: INK.secondary, fontFamily: FONT },
      })),
      grid: [
        { left: 8, right: "52%", top: 34, bottom: 8 },
        { left: "52%", right: 8, top: 34, bottom: 8 },
      ],
      xAxis: RESULTS.map((_, i) => ({
        ...slotAxis(fights.length, (slot) => axisLabels[slot] ?? ""),
        gridIndex: i,
        splitLine: { show: false },
      })),
      yAxis: RESULTS.map((_, i) => ({
        type: "value" as const,
        gridIndex: i,
        min: 0,
        max: 100,
        interval: 25,
        ...AXIS_STYLE,
        axisLine: { show: false },
      })),
      tooltip: {
        ...itemTooltip,
        formatter: (params) => {
          const p = Array.isArray(params) ? params[0] : params;
          const resultIndex = p.seriesIndex === undefined ? 0 : p.seriesIndex % 2;
          if ((p.seriesIndex ?? 0) < 2) {
            const point = pointSets[resultIndex][p.dataIndex];
            return tipHtml(point.fight, [
              ["Health at the start", fmtInt(point.health)],
              ["Run", shortId(point.run_id)],
            ]);
          }
          const s = meanSets[resultIndex][p.dataIndex];
          return tipHtml(s.fight + " · " + RESULTS[resultIndex].title.toLowerCase(), [
            ["Mean health at the start", fmt1(s.mean)],
            ["Median", fmt1(s.median)],
            ["Runs", fmtInt(s.runs)],
          ]);
        },
      },
      series: [
        ...RESULTS.map((r, i) => ({
          type: "scatter" as const,
          name: r.title,
          xAxisIndex: i,
          yAxisIndex: i,
          symbolSize: 8,
          itemStyle: { color: r.color, opacity: 0.45, borderColor: INK.surface, borderWidth: 1 },
          data: pointSets[i].map((p, k) => [indexOf(p.fight) + jitter(k, i) * 0.2, p.health]),
        })),
        ...RESULTS.map((r, i) => ({
          type: "scatter" as const,
          name: "Mean · " + r.title,
          xAxisIndex: i,
          yAxisIndex: i,
          symbol: "rect",
          symbolSize: [26, 3],
          itemStyle: { color: INK.primary },
          z: 5,
          data: meanSets[i].map((s) => [indexOf(s.fight), s.mean]),
        })),
      ],
    };
  }, [points, summary]);

  return (
    <ChartCard
      title="Health at the start of each fight: fights cleared vs fights died in"
      subtitle={<N n={points.length} noun="fights" />}
      option={option}
      rows={summary.map((s) => ({ fight: s.fight, result: s.result, runs: s.runs, mean_health: Math.round(s.mean * 10) / 10, median_health: s.median }))}
      height={300}
      fileName="health-at-fight-start"
      empty={points.length === 0}
      footnote="Each dot is one run entering a fight (health when the fight began, 0-100); the dark dash is the mean. Same scale on both sides: if the right side sits lower, players who start a fight hurt die in it more often. Fights left by Restart or a closed tab are not shown."
    />
  );
}

function DamageSources({ rows }: { rows: Deaths["damageSources"] }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => killerLabel(sourceKiller(r.source))),
        values: rows.map((r) => r.hits),
        colors: rows.map((r) => killerColor(sourceKiller(r.source))),
        horizontal: true,
        valueName: "Hits on the player",
        barLabel: (i) => fmtInt(rows[i].hits),
        tooltip: (i) =>
          tipHtml(killerLabel(sourceKiller(rows[i].source)), [
            ["Hits", fmtInt(rows[i].hits)],
            ["Damage dealt", fmtInt(rows[i].damage)],
            ["Fatal hits", fmtInt(rows[i].fatal)],
          ]),
      }),
    [rows],
  );
  const total = rows.reduce((sum, r) => sum + r.hits, 0);
  return (
    <ChartCard
      title="Every hit the player took, by source"
      subtitle={<N n={total} noun="hits" />}
      option={option}
      rows={rows.map((r) => ({ source: r.source, label: killerLabel(sourceKiller(r.source)), hits: r.hits, damage: r.damage, fatal_hits: r.fatal }))}
      height={Math.max(220, rows.length * 32 + 60)}
      fileName="damage-sources"
      empty={rows.length === 0}
      emptyText="Nobody got hurt with these filters."
      footnote="All hits, fatal or not (a bite costs 10 health, an orb 15). Compare with the fatal-hit chart: a kind that hurts often but rarely kills is a nuisance, not a killer."
    />
  );
}

function DeathMap({ rows }: { rows: Deaths["deathPositions"] }) {
  const levels = useMemo(() => [...new Set(rows.map((r) => r.level))], [rows]);
  const [picked, setPicked] = useState("");
  const level = levels.includes(picked) ? picked : (levels[0] ?? "");
  const shown = useMemo(() => rows.filter((r) => r.level === level), [rows, level]);
  const option = useMemo<EChartsOption>(() => {
    const killers = KILLER_ORDER.filter((k) => shown.some((r) => r.killer === k));
    const groups = killers.map((k) => shown.filter((r) => r.killer === k));
    return {
      animationDuration: 300,
      legend: { ...LEGEND_STYLE, top: 0, left: 0, data: killers.map(killerLabel) },
      grid: { left: 36, right: 16, top: 40, bottom: 34 },
      xAxis: { type: "value", name: "x (metres east)", nameLocation: "middle", nameGap: 24, scale: true, ...AXIS_STYLE },
      yAxis: { type: "value", name: "z (metres north)", nameLocation: "middle", nameGap: 30, scale: true, ...AXIS_STYLE },
      tooltip: {
        ...itemTooltip,
        formatter: (params) => {
          const p = Array.isArray(params) ? params[0] : params;
          const r = groups[p.seriesIndex ?? 0]?.[p.dataIndex];
          return r
            ? tipHtml(r.stage, [
                ["Killed by", killerLabel(r.killer)],
                ["Position", fmt1(r.x) + ", " + fmt1(r.z)],
                ["Run", shortId(r.run_id)],
              ])
            : "";
        },
      },
      series: killers.map((k, i) => ({
        type: "scatter" as const,
        name: killerLabel(k),
        symbolSize: 10,
        itemStyle: { color: killerColor(k), opacity: 0.85, borderColor: INK.surface, borderWidth: 2 },
        data: groups[i].map((r) => [r.x, r.z]),
      })),
    };
  }, [shown]);
  return (
    <ChartCard
      title="Where players died on the map"
      subtitle={<N n={shown.length} noun="deaths with a position" />}
      option={option}
      rows={shown.map((r) => ({ run_id: r.run_id, level: levelName(r.level), stage: r.stage, killer: killerLabel(r.killer), x: r.x, z: r.z }))}
      height={340}
      fileName="death-positions"
      empty={rows.length === 0}
      emptyText="No positions yet: the game build that logs player_x / player_z has not sent data for these filters."
      actions={
        levels.length > 1 ? (
          <Segmented value={level} options={levels.map((l) => ({ value: l, label: levelName(l) }))} onChange={setPicked} ariaLabel="Level" />
        ) : null
      }
      footnote="Each dot is a fatal hit at the player's position (top-down map, metres). Clusters show the spots of the level that kill."
    />
  );
}
