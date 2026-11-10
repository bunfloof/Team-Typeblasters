// /runs/[runId] - one run on a timeline. The page itself is a static shell:
// the client component reads the run id from the URL and loads its data
// from /api/runs/[runId] (inside Suspense, as useParams / useSearchParams need).
import { Suspense } from "react";
import { Spinner } from "@/components/ui";
import RunTimelinePage from "@/components/timeline/RunTimelinePage";

export default function RunPage() {
  return (
    <Suspense fallback={<Spinner label="Loading run" />}>
      <RunTimelinePage />
    </Suspense>
  );
}
