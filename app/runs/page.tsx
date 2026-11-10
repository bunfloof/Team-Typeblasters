// /runs: every run with filters, stat tiles and a table (components/runs/RunsView.tsx).
import { Suspense } from "react";
import type { Metadata } from "next";
import RunsView from "@/components/runs/RunsView";
import { Spinner } from "@/components/ui";

export const metadata: Metadata = { title: "Runs · Typeblasters analytics" };

export default function RunsPage() {
  // RunsView reads its filters from the URL (useSearchParams), so it needs a Suspense boundary.
  return (
    <Suspense fallback={<Spinner label="Loading runs" />}>
      <RunsView />
    </Suspense>
  );
}
