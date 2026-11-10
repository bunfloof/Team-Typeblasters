// GET /api/players?includeTest=1&level=Hard&from=&to= -> PlayersResponse: one row per
// install_id (browser), most recently seen first, plus the number of test
// installs under the level filter.
import { errorResponse } from "@/lib/db";
import { parseEventFilter } from "@/lib/filters";
import { loadPlayers } from "@/lib/runsServer";

export async function GET(request: Request) {
  try {
    const filter = parseEventFilter(new URL(request.url).searchParams);
    return Response.json(await loadPlayers(filter.includeTest, filter.level, filter.from, filter.to));
  } catch (error) {
    return errorResponse(error);
  }
}
