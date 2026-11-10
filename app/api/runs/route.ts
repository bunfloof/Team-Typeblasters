// GET /api/runs -> RunsResponse: one row per run, newest first.
// Query parameters:
//   includeTest=1, level=Hard          the global filters
//   installId, sessionId, runId, build  runs of that install / session / run / build
//   event, waveMin, waveMax, from, to, search, json=field|op|value
//                                       runs with at least one event matching these
//                                       (see lib/filters.ts)
//   outcome=won,lost,restart,abandoned,playing   (comma separated; empty = all)
//   runNumber=first | repeat           first run in that browser / later runs
import { errorResponse } from "@/lib/db";
import { parseEventFilter } from "@/lib/filters";
import { loadRuns } from "@/lib/runsServer";
import type { RunOutcome } from "@/lib/runSummary";
import { RUN_OUTCOMES, type RunNumberFilter, type RunsResponse } from "@/components/runs/types";

export async function GET(request: Request) {
  try {
    const params = new URL(request.url).searchParams;
    const outcomes = (params.get("outcome") || "")
      .split(",")
      .map((value) => value.trim())
      .filter((value): value is RunOutcome => RUN_OUTCOMES.includes(value as RunOutcome));
    const runNumberParam = params.get("runNumber");
    const runNumber: RunNumberFilter = runNumberParam === "first" || runNumberParam === "repeat" ? runNumberParam : "";

    const runs = await loadRuns(parseEventFilter(params), { outcomes, runNumber });
    const body: RunsResponse = { runs };
    return Response.json(body);
  } catch (error) {
    return errorResponse(error);
  }
}
