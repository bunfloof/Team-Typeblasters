import { Suspense } from "react";
import { Spinner } from "@/components/ui";
import OverviewView from "@/components/overview/OverviewView";

// The Overview (home page). The view is a client component that reads the
// global filters and fetches /api/overview, so it streams in behind this
// Suspense fallback.
export default function OverviewPage() {
  return (
    <Suspense fallback={<Spinner label="Loading the overview" />}>
      <OverviewView />
    </Suspense>
  );
}
