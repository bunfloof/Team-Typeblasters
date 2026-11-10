"use client";
// The Insights page: one filter row, then the charts that answer the team's
// questions. The page's own filters (runs, build) live in the URL so a view
// can be shared; the global ones (test plays, level, time range) come from
// the top bar.
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { Info, RefreshCw } from "lucide-react";
import { levelName } from "@/lib/catalog";
import { fmtInt } from "@/lib/format";
import { globalParams, rangeLabel, useGlobalFilters } from "@/lib/globalFilters";
import { apiUrl, useApi } from "@/lib/useApi";
import { ErrorNote, Hold, PageHeader, Spinner, buttonClass, inputClass } from "@/components/ui";
import DeathsSection from "./DeathsSection";
import MoreSection from "./MoreSection";
import PowersSection from "./PowersSection";
import RedSection from "./RedSection";
import TypingSection from "./TypingSection";
import { Segmented, configHint } from "./parts";
import type { InsightsResponse, RunNumberScope } from "./types";

const RUN_OPTIONS: { value: RunNumberScope; label: string }[] = [
  { value: "", label: "All runs" },
  { value: "first", label: "First run of each player" },
  { value: "repeat", label: "Repeat runs only" },
];

const SECTIONS = [
  { id: "deaths", label: "Deaths" },
  { id: "red", label: "Red zombies" },
  { id: "powers", label: "Powers & weapons" },
  { id: "typing", label: "Typing" },
  { id: "more", label: "Quiz, tips, pauses" },
];

export default function InsightsView() {
  const filters = useGlobalFilters();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const runParam = searchParams.get("runNumber");
  const runNumber: RunNumberScope = runParam === "first" || runParam === "repeat" ? runParam : "";
  const build = searchParams.get("build") ?? "";

  const { data, error, loading, reload } = useApi<InsightsResponse>(apiUrl("/api/insights", { ...globalParams(filters), runNumber, build }));

  const setParam = (name: string, value: string) => {
    const next = new URLSearchParams(searchParams.toString());
    if (value) {
      next.set(name, value);
    } else {
      next.delete(name);
    }
    const query = next.toString();
    router.replace(query ? pathname + "?" + query : pathname, { scroll: false });
  };

  const builds = data?.scope.builds ?? [];
  return (
    <div className="mx-auto max-w-[1400px]">
      <PageHeader
        title="Insights"
        description="The charts behind the team's questions: where players die, whether they use the red-zombie blast ring and their powers, and how they type. Every chart has a table view (top-right toggle) with a CSV export."
        actions={
          <button type="button" className={buttonClass} onClick={reload} disabled={loading}>
            <RefreshCw className={"h-3.5 w-3.5 " + (loading ? "animate-spin" : "")} /> Refresh
          </button>
        }
      />

      <div className="sticky top-[53px] z-10 -mx-6 mb-4 flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-black/10 bg-[#f9f9f7]/95 px-6 py-2.5 backdrop-blur">
        <Segmented value={runNumber} options={RUN_OPTIONS} onChange={(value) => setParam("runNumber", value)} ariaLabel="Runs" />
        <label className="flex items-center gap-2 text-sm text-neutral-700">
          Build
          <select value={build} onChange={(event) => setParam("build", event.target.value)} className={inputClass + " max-w-[240px]"}>
            <option value="">All builds</option>
            {(builds.includes(build) || !build ? builds : [build, ...builds]).map((b) => (
              <option key={b} value={b}>
                {b}
              </option>
            ))}
          </select>
        </label>
        <span className="text-xs text-neutral-500">
          {filters.level ? levelName(filters.level) + " level" : "All levels"} · {filters.includeTest ? "with test plays" : "no test plays"} · {rangeLabel(filters)}
        </span>
        {data ? (
          <span className="ml-auto text-sm tabular-nums text-neutral-600">
            {fmtInt(data.scope.runs)} runs · {fmtInt(data.scope.players)} players
            {runNumber && data.scope.runsWithoutNumber > 0 ? (
              <span className="text-xs text-neutral-500"> · {fmtInt(data.scope.runsWithoutNumber)} runs without a run number left out</span>
            ) : null}
          </span>
        ) : null}
      </div>

      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <p className="flex max-w-3xl items-start gap-2 rounded-lg border border-black/10 bg-white px-3 py-2 text-[13px] leading-relaxed text-neutral-600">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-neutral-400" />
          These charts show correlations, not causes: players who die more may also be newer, faster or on a harder level. Check n (the sample size in each
          subtitle) before reading much into a difference.
        </p>
        <nav className="flex flex-wrap gap-1.5 text-sm" aria-label="Sections">
          {SECTIONS.map((section) => (
            <a key={section.id} href={"#" + section.id} className="rounded-md border border-black/10 bg-white px-2.5 py-1 text-neutral-600 hover:bg-neutral-50">
              {section.label}
            </a>
          ))}
        </nav>
      </div>

      {error ? (
        <div className="mb-6">
          <ErrorNote error={error} hint={configHint(error)} />
        </div>
      ) : null}
      {!data && loading ? <Spinner label="Computing insights" /> : null}

      {data ? (
        <Hold loading={loading}>
          <div className="flex flex-col gap-10 pb-16">
            <DeathsSection data={data.deaths} />
            <RedSection data={data.red} />
            <PowersSection data={data.powers} />
            <TypingSection data={data.typing} />
            <MoreSection data={data.more} />
          </div>
        </Hold>
      ) : null}
    </div>
  );
}
