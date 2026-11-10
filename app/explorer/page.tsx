// /explorer - every row of the database: filter, sort, select, export CSV.
// The page itself is static; the client component reads the URL and loads
// rows from /api/rows (inside Suspense, as useSearchParams requires).
import { Suspense } from "react";
import type { Metadata } from "next";
import ExplorerClient from "@/components/explorer/ExplorerClient";
import { PageHeader, Spinner } from "@/components/ui";

export const metadata: Metadata = { title: "Explorer · Typeblasters analytics" };

export default function ExplorerPage() {
  return (
    <>
      <PageHeader
        title="Explorer"
        description="Every row of the events and requests tables, nothing hidden. Filter, sort, open a row for its full detail and the request (IP, browser) it came in, select rows and export them as CSV. The filters live in the address bar: copy it to share this view."
      />
      <Suspense fallback={<Spinner label="Loading the explorer" />}>
        <ExplorerClient />
      </Suspense>
    </>
  );
}
