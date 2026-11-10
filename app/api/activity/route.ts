// GET /api/activity?includeTest=1&level=Hard -> ActivityResponse: the play
// sessions (stretches of time with events, newest first) for the top bar's
// time-range picker. The time range itself is ignored. Read only.
import { errorResponse } from "@/lib/db";
import { loadActivity } from "@/lib/activityServer";
import { parseEventFilter } from "@/lib/filters";

export async function GET(request: Request) {
  try {
    return Response.json(await loadActivity(parseEventFilter(new URL(request.url).searchParams)));
  } catch (error) {
    return errorResponse(error);
  }
}
