// GET /api/insights -> InsightsResponse (components/insights/types.ts)
// Query parameters:
//   includeTest=1, level=Hard      the global filters
//   build=...                      one game build only
//   runNumber=first | repeat       first run in that browser / later runs (default: all runs)
//   (any other lib/filters.ts parameter, e.g. from/to, also applies)
// Every dataset is computed from deduplicated events in lib/insightsServer.ts.
import { errorResponse } from "@/lib/db";
import { computeInsights } from "@/lib/insightsServer";

export async function GET(request: Request) {
  try {
    const body = await computeInsights(new URL(request.url).searchParams);
    return Response.json(body);
  } catch (error) {
    return errorResponse(error);
  }
}
