import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui";
import InsightsView from "@/components/insights/InsightsView";

export const metadata: Metadata = { title: "Insights · Typeblasters analytics" };

// The charts that answer the team's questions. The view is a client
// component (it reads the URL and the global filters, and fetches
// /api/insights), so it streams in behind this Suspense fallback.
export default function InsightsPage() {
  return (
    <Suspense fallback={<Spinner label="Loading insights" />}>
      <InsightsView />
    </Suspense>
  );
}
