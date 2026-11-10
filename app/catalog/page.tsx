// /catalog - the data dictionary: every event the game logs, when, and
// what each field means, with live row counts and links to the Explorer.
import { Suspense } from "react";
import type { Metadata } from "next";
import CatalogClient from "./CatalogClient";
import { PageHeader, Spinner } from "@/components/ui";

export const metadata: Metadata = { title: "Event catalog · Typeblasters analytics" };

export default function CatalogPage() {
  return (
    <>
      <PageHeader
        title="Event catalog"
        description="What the game logs: every event type, when it is sent and what each field means. Row counts are live from the database (with the top bar's filters); open any type in the Explorer to see its rows."
      />
      <Suspense fallback={<Spinner label="Loading the catalog" />}>
        <CatalogClient />
      </Suspense>
    </>
  );
}
