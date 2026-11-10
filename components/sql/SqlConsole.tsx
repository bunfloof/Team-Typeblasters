"use client";
// Entry of the /sql page (rendered inside <Suspense>, as it reads the URL).
// /sql?q=<sql> opens that query and runs it (the "Copy link" button makes
// such links). The console reads localStorage, so it only renders in the
// browser; the server sends a placeholder.
import { useSyncExternalStore } from "react";
import { useSearchParams } from "next/navigation";
import { Spinner } from "@/components/ui";
import SqlWorkbench from "./SqlWorkbench";

const subscribeNothing = () => () => {};

export default function SqlConsole() {
  const query = useSearchParams().get("q");
  const inBrowser = useSyncExternalStore(
    subscribeNothing,
    () => true,
    () => false,
  );
  if (!inBrowser) {
    return <Spinner label="Opening the SQL console" />;
  }
  // A new ?q= link starts a fresh console.
  return <SqlWorkbench key={query ?? ""} initialSql={query} />;
}
