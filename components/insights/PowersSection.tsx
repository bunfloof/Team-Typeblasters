"use client";
// Question 3: do players leave powers unused, and where does each power's funnel break?
import { useMemo, useState } from "react";
import ChartCard from "@/components/ChartCard";
import { fmt1, fmtInt, fmtPct } from "@/lib/format";
import { BLUE_RAMP, INK, SERIES } from "@/lib/theme";
import { barOption, fmtPctAxis, multiBarOption, rampColors, tipHtml } from "./charts";
import { ITEM_LABELS, REFUSAL_LABELS, labelOf } from "./labels";
import { N, Section, Segmented } from "./parts";
import { binCounts, edgeBins, median } from "./stats";
import type { InsightsResponse, PowerItem } from "./types";

type Powers = InsightsResponse["powers"];

const POWER_ITEMS: PowerItem[] = ["lure", "freeze", "frenzy", "rpg"];
const POWER_KEYS: Record<PowerItem, string> = { lure: "1", freeze: "2", frenzy: "3", rpg: "4" };
const ENERGY_COST: Record<PowerItem, number> = { lure: 6, freeze: 8, frenzy: 12, rpg: 18 };

// Fixed colors per power (FRENZY and RPG keep the colors they always had).
const ITEM_COLORS: Record<string, string> = { frenzy: SERIES[0], rpg: SERIES[1], lure: SERIES[2], freeze: SERIES[3], shield: SERIES[4] };

// How each power's last step ("Got kills with it") is measured.
const KILL_NOTES: Record<PowerItem, string> = {
  lure: "its blast killed a zombie",
  freeze: "a zombie was killed while it was on (4 s; 8 s in Normal's guided wave 1)",
  frenzy: "a zombie was killed during it",
  rpg: "its rocket killed a zombie",
};

export default function PowersSection({ data }: { data: Powers }) {
  const funnels = useMemo(
    () => ({
      lure: data.powerFunnel.filter((s) => s.item === "lure"),
      freeze: data.powerFunnel.filter((s) => s.item === "freeze"),
      frenzy: data.powerFunnel.filter((s) => s.item === "frenzy"),
      rpg: data.powerFunnel.filter((s) => s.item === "rpg"),
      oldFrenzy: data.weaponFunnel.filter((s) => s.weapon === "frenzy"),
      oldRpg: data.weaponFunnel.filter((s) => s.weapon === "rpg"),
    }),
    [data.powerFunnel, data.weaponFunnel],
  );
  // Runs of older builds get their own charts (only shown when they are in the filters).
  const showOld = data.legacyRuns > 0;
  const showEnergy = data.energyRuns > 0 || !showOld;
  return (
    <Section
      id="powers"
      label="Question 3"
      title="Do players leave powers unused, and where does each power's funnel break?"
      intro="Every power costs skill energy: lure 6, freeze 8, FRENZY 12, RPG 18 (keys 1 to 4; at most 20 energy is held). Energy comes from kills (+1 each) and energy crates, and an Endless wave reward can give a free use. If players die with enough energy for a power, or never get from “could afford it” to “got kills with it”, the powers are not doing their job."
    >
      <div className="grid gap-4 xl:grid-cols-2">
        <UnusedAtDeath rows={data.unusedAtDeath} deaths={data.deaths} />
        {showEnergy ? <EnergyUse rows={data.energyUse} energyIn={data.energyIn} runs={data.energyRuns} /> : <GainedVsUsed rows={data.gainedUsed} />}
      </div>
      {showEnergy ? (
        <div className="grid gap-4 xl:grid-cols-2">
          {POWER_ITEMS.map((item) => (
            <StepFunnel
              key={item}
              title={labelOf(ITEM_LABELS, item) + " funnel"}
              subtitle={"Share of runs reaching each step (key " + POWER_KEYS[item] + ", " + ENERGY_COST[item] + " energy)"}
              steps={funnels[item]}
              fileName={item + "-funnel"}
              emptyText={"No run could afford or tried a " + labelOf(ITEM_LABELS, item) + " with these filters."}
              footnote={
                "Could afford it = the run held " +
                ENERGY_COST[item] +
                " energy at some moment, or got a free use from a wave reward. Pressed = key " +
                POWER_KEYS[item] +
                " was pressed, also when it was refused. Got kills = " +
                KILL_NOTES[item] +
                ". Each step counts runs on its own, so a step can exceed the one above it."
              }
            />
          ))}
        </div>
      ) : null}
      <div className="grid gap-4 xl:grid-cols-2">
        <Refusals rows={data.refusals} />
        <GainToUse rows={data.gainToUse} />
      </div>
      {showOld ? (
        <>
          <div className="mt-2">
            <h3 className="text-base font-semibold text-neutral-900">Older builds: crate charges and combo-bought weapons</h3>
            <p className="mt-1 max-w-3xl text-sm leading-relaxed text-neutral-500">
              {fmtInt(data.legacyRuns)} runs in these filters come from builds before the energy update of 2 November 2026: lures and freezes came from supply
              crates, and FRENZY / RPG were bought with combo (hold 3 or 4). Pick a date range from 2 November on in the top bar to hide them.
            </p>
          </div>
          <div className="grid gap-4 xl:grid-cols-2">
            {showEnergy ? <GainedVsUsed rows={data.gainedUsed} /> : null}
            <StepFunnel
              title="FRENZY funnel (bought with combo)"
              subtitle="Share of older-build runs reaching each step (key 3)"
              steps={funnels.oldFrenzy}
              fileName="frenzy-funnel-older-builds"
              emptyText="No older-build run could afford or tried a FRENZY with these filters."
              footnote="Could afford it = at some moment a full hold of 3 would have bought it. Pressed = any tap or hold of the key. Each step counts runs on its own, so a step can exceed the one above it."
            />
            <StepFunnel
              title="RPG funnel (bought with combo)"
              subtitle="Share of older-build runs reaching each step (key 4)"
              steps={funnels.oldRpg}
              fileName="rpg-funnel-older-builds"
              emptyText="No older-build run could afford or tried an RPG with these filters."
              footnote="Could afford it = at some moment a full hold of 4 would have bought it. Pressed = any tap or hold of the key. Each step counts runs on its own, so a step can exceed the one above it."
            />
          </div>
        </>
      ) : null}
    </Section>
  );
}

function UnusedAtDeath({ rows, deaths }: { rows: Powers["unusedAtDeath"]; deaths: number }) {
  const option = useMemo(
    () =>
      barOption({
        categories: rows.map((r) => (r.item === "any" ? "Any of them" : labelOf(ITEM_LABELS, r.item) + " usable")),
        values: rows.map((r) => r.share),
        colors: SERIES[0],
        horizontal: true,
        max: 1,
        format: fmtPctAxis,
        valueName: "Share of deaths",
        barLabel: (i) => fmtPct(rows[i].share),
        tooltip: (i) =>
          tipHtml(labelOf(ITEM_LABELS, rows[i].item), [
            ["Deaths while it was usable", fmtInt(rows[i].deaths)],
            ["Share of deaths", fmtPct(rows[i].share, 1)],
          ]),
      }),
    [rows],
  );
  return (
    <ChartCard
      title="Deaths with a power still usable"
      subtitle={<N n={deaths} noun="deaths" />}
      option={option}
      rows={rows.map((r) => ({ item: labelOf(ITEM_LABELS, r.item), deaths_while_usable: r.deaths, share_pct: Math.round(r.share * 1000) / 10 }))}
      height={240}
      fileName="usable-powers-at-death"
      empty={deaths === 0}
      emptyText="No deaths for these filters."
      footnote="What the player could still have used when the fatal hit landed: enough energy for it, a free use kept from a wave reward, or a loaded RPG (older builds: a crate charge or a bought weapon). A freeze or FRENZY already running does not count. High bars = powers that could have saved the run."
    />
  );
}

const PAID_COLOR = SERIES[0];
const FREE_COLOR = BLUE_RAMP[4];

function EnergyUse({ rows, energyIn, runs }: { rows: Powers["energyUse"]; energyIn: Powers["energyIn"]; runs: number }) {
  const option = useMemo(
    () =>
      multiBarOption({
        categories: rows.map((r) => labelOf(ITEM_LABELS, r.item)),
        horizontal: true,
        stacked: true,
        valueName: "Uses per run",
        format: (v) => fmt1(v),
        series: [
          { name: "Paid with energy", color: PAID_COLOR, values: rows.map((r) => r.paid_per_run) },
          { name: "Free (wave reward)", color: FREE_COLOR, values: rows.map((r) => r.free_per_run) },
        ],
        tooltip: (i) =>
          tipHtml(labelOf(ITEM_LABELS, rows[i].item), [
            ["Paid uses per run", fmt1(rows[i].paid_per_run), PAID_COLOR],
            ["Free uses per run", fmt1(rows[i].free_per_run), FREE_COLOR],
            ["Uses in total", fmtInt(rows[i].uses)],
            ["Energy spent in total", fmtInt(rows[i].energy_spent)],
            ["Runs that used it", fmtInt(rows[i].runs_used) + " of " + fmtInt(runs)],
          ]),
      }),
    [rows, runs],
  );
  return (
    <ChartCard
      title="What players spend their energy on, per run"
      subtitle={
        <>
          <N n={runs} noun="runs" />
          {energyIn.crates > 0
            ? " · energy crates gave " + fmt1(runs > 0 ? energyIn.crate_energy / runs : 0) + " energy per run (" + fmtInt(energyIn.crates_when_full) + " of " + fmtInt(energyIn.crates) + " opened while full)"
            : ""}
        </>
      }
      option={option}
      rows={rows.map((r) => ({
        power: labelOf(ITEM_LABELS, r.item),
        runs,
        uses: r.uses,
        free_uses: r.free_uses,
        energy_spent: r.energy_spent,
        uses_per_run: Math.round(((r.uses / Math.max(1, runs)) * 100)) / 100,
        runs_that_used_it: r.runs_used,
      }))}
      height={240}
      fileName="energy-spent-per-power"
      empty={runs === 0}
      emptyText="No run of a current build with these filters."
      footnote={
        "A lure thrown, or a freeze, FRENZY or RPG used. Free = a use kept from an Endless wave reward (no energy spent). Energy comes in from kills (+1 each, not logged on its own), energy crates" +
        (energyIn.rewards > 0 ? " and wave rewards (" + fmtInt(energyIn.reward_energy) + " energy from " + fmtInt(energyIn.rewards) + " rewards)" : "") +
        ". A crate opened while full gave nothing."
      }
    />
  );
}

function GainedVsUsed({ rows }: { rows: Powers["gainedUsed"] }) {
  const option = useMemo(
    () =>
      multiBarOption({
        categories: rows.map((r) => labelOf(ITEM_LABELS, r.item)),
        valueName: "Charges per run",
        format: (v) => fmt1(v),
        series: [
          { name: "Gained from crates", color: SERIES[0], values: rows.map((r) => r.gained_per_run) },
          { name: "Used", color: SERIES[1], values: rows.map((r) => r.used_per_run) },
        ],
        tooltip: (i) =>
          tipHtml(labelOf(ITEM_LABELS, rows[i].item), [
            ["Gained per run", fmt1(rows[i].gained_per_run), SERIES[0]],
            ["Used per run", fmt1(rows[i].used_per_run), SERIES[1]],
            ["Gained in total", fmtInt(rows[i].gained)],
            ["Used in total", fmtInt(rows[i].used)],
            ["Runs that used one", fmtInt(rows[i].runs_used) + " of " + fmtInt(rows[i].runs)],
            ["Crates opened while full", fmtInt(rows[i].crates_when_full)],
          ]),
      }),
    [rows],
  );
  const runs = rows[0]?.runs ?? 0;
  return (
    <ChartCard
      title="Lure and freeze: crate charges gained vs used, per run"
      subtitle={<N n={runs} noun="older-build runs" />}
      option={option}
      rows={rows.map((r) => ({
        item: labelOf(ITEM_LABELS, r.item),
        runs: r.runs,
        gained: r.gained,
        used: r.used,
        gained_per_run: Math.round(r.gained_per_run * 100) / 100,
        used_per_run: Math.round(r.used_per_run * 100) / 100,
        runs_that_gained: r.runs_gained,
        runs_that_used: r.runs_used,
        crates_opened_while_full: r.crates_when_full,
      }))}
      height={260}
      fileName="lure-freeze-gained-used"
      empty={runs === 0}
      emptyText="No older-build run with these filters."
      footnote="Older builds only. Gained = a crate added a charge; used = a lure thrown (key 1) or a freeze used (key 2). A used bar far below the gained bar means charges pile up unused."
    />
  );
}

function StepFunnel({
  title,
  subtitle,
  steps,
  fileName,
  emptyText,
  footnote,
}: {
  title: string;
  subtitle: string;
  steps: { step: string; runs: number; share: number }[];
  fileName: string;
  emptyText: string;
  footnote: string;
}) {
  const option = useMemo(
    () =>
      barOption({
        categories: steps.map((s) => s.step),
        values: steps.map((s) => s.share),
        colors: rampColors(steps.length),
        horizontal: true,
        max: 1,
        format: fmtPctAxis,
        valueName: "Share of runs",
        barLabel: (i) => fmtPct(steps[i].share) + "  (" + fmtInt(steps[i].runs) + ")",
        tooltip: (i) =>
          tipHtml(steps[i].step, [
            ["Runs", fmtInt(steps[i].runs)],
            ["Share of runs", fmtPct(steps[i].share, 1)],
          ]),
      }),
    [steps],
  );
  return (
    <ChartCard
      title={title}
      subtitle={subtitle}
      option={option}
      rows={steps.map((s) => ({ step: s.step, runs: s.runs, share_pct: Math.round(s.share * 1000) / 10 }))}
      height={steps.length > 4 ? 230 : 200}
      fileName={fileName}
      empty={steps.every((s) => s.runs === 0)}
      emptyText={emptyText}
      footnote={footnote}
    />
  );
}

function Refusals({ rows }: { rows: Powers["refusals"] }) {
  const option = useMemo(() => {
    const reasons = [...new Set(rows.map((r) => r.reason))];
    const items = [...POWER_ITEMS, ...new Set(rows.map((r) => r.item).filter((i) => !POWER_ITEMS.includes(i as PowerItem)))].filter((item) =>
      rows.some((r) => r.item === item),
    );
    return multiBarOption({
      categories: reasons.map((r) => labelOf(REFUSAL_LABELS, r)),
      horizontal: true,
      stacked: true,
      valueName: "Refused presses",
      series: items.map((item) => ({
        name: labelOf(ITEM_LABELS, item),
        color: ITEM_COLORS[item] ?? INK.muted,
        values: reasons.map((reason) => rows.find((r) => r.item === item && r.reason === reason)?.count ?? 0),
      })),
    });
  }, [rows]);
  const total = rows.reduce((sum, r) => sum + r.count, 0);
  return (
    <ChartCard
      title="Why a power key did nothing"
      subtitle={<N n={total} noun="refused presses" />}
      option={option}
      rows={rows.map((r) => ({ power: labelOf(ITEM_LABELS, r.item), reason: labelOf(REFUSAL_LABELS, r.reason), count: r.count }))}
      height={Math.max(200, new Set(rows.map((r) => r.reason)).size * 40 + 90)}
      fileName="power-refusals"
      empty={rows.length === 0}
      emptyText="No refused presses for these filters."
      footnote="Current builds: not enough energy, or the freeze / FRENZY / RPG / shield was already running (a lure is aimed while the key is held, so it can also be refused when the key comes up). Older builds: a full hold of 3 / 4 that did not buy (slots full, one per wave, not enough combo)."
    />
  );
}

const GAIN_BINS = edgeBins([0, 5, 10, 20, 30, 60, 120]);

function GainToUse({ rows }: { rows: Powers["gainToUse"] }) {
  const items = useMemo(() => POWER_ITEMS.filter((it) => rows.some((r) => r.item === it)), [rows]);
  const [picked, setPicked] = useState<string>("lure");
  const item = items.includes(picked as PowerItem) ? picked : (items[0] ?? "lure");
  const table = useMemo(
    () =>
      POWER_ITEMS.flatMap((it) => {
        const list = rows.filter((r) => r.item === it);
        if (list.length === 0) {
          return [];
        }
        const seconds = list.map((r) => r.seconds).filter((s): s is number => s !== null);
        const counts = binCounts(seconds, GAIN_BINS);
        return [
          ...GAIN_BINS.map((bin, i) => ({ item: labelOf(ITEM_LABELS, it), seconds: bin.label, runs: counts[i] })),
          { item: labelOf(ITEM_LABELS, it), seconds: "Never used", runs: list.length - seconds.length },
        ];
      }),
    [rows],
  );
  const { shown, usedSeconds } = useMemo(() => {
    const list = rows.filter((r) => r.item === item);
    return { shown: list, usedSeconds: list.map((r) => r.seconds).filter((s): s is number => s !== null) };
  }, [rows, item]);
  const option = useMemo(() => {
    const counts = [...binCounts(usedSeconds, GAIN_BINS), shown.length - usedSeconds.length];
    const labels = [...GAIN_BINS.map((b) => b.label), "Never"];
    return barOption({
      categories: labels,
      values: counts,
      colors: [...GAIN_BINS.map(() => SERIES[0]), INK.muted],
      valueName: "Runs",
      categoryName: "Seconds from the first gain to the first use",
      tooltip: (i) =>
        tipHtml(i === labels.length - 1 ? "Gained but never used" : labels[i] + " s", [
          ["Runs", fmtInt(counts[i])],
          ["Share", shown.length ? fmtPct(counts[i] / shown.length) : "-"],
        ]),
    });
  }, [shown, usedSeconds]);
  return (
    <ChartCard
      title="How long a free power waits before its first use"
      subtitle={
        <>
          <N n={shown.length} noun={"runs that gained a " + labelOf(ITEM_LABELS, item)} />
          {usedSeconds.length > 0 ? " · median " + fmt1(median(usedSeconds)) + " s to the first use" : ""}
        </>
      }
      option={option}
      rows={table}
      height={260}
      fileName="gain-to-first-use"
      empty={rows.length === 0}
      emptyText="No free power was gained with these filters."
      actions={
        items.length > 1 ? (
          <Segmented value={item} options={items.map((it) => ({ value: it as string, label: labelOf(ITEM_LABELS, it) }))} onChange={setPicked} ariaLabel="Power" />
        ) : null
      }
      footnote="Per run: play seconds from the first free use gained (an Endless wave reward; older builds: a crate charge or a bought weapon) to its first use. “Never” = gained and never used in that run. Powers paid with energy are in the funnels above."
    />
  );
}
