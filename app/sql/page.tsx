// /sql - the SQL console: run any read-only query on the whole database,
// see the rows in a table, export them as CSV.
import { Suspense } from "react";
import type { Metadata } from "next";
import { PageHeader, Spinner } from "@/components/ui";
import SqlConsole from "@/components/sql/SqlConsole";

export const metadata: Metadata = {
  title: "SQL · Typeblasters analytics",
};

export default function SqlPage() {
  return (
    <>
      <PageHeader
        title="SQL console"
        description="Ask the database anything: write a query (or start from an example), see every row, select rows and export them as CSV. Read-only: nothing here can change or delete data."
      />
      <Suspense fallback={<Spinner label="Opening the SQL console" />}>
        <SqlConsole />
      </Suspense>
    </>
  );
}
