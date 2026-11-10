// /players: one row per browser (install id) (components/runs/PlayersView.tsx).
import { Suspense } from "react";
import type { Metadata } from "next";
import PlayersView from "@/components/runs/PlayersView";
import { Spinner } from "@/components/ui";

export const metadata: Metadata = { title: "Players · Typeblasters analytics" };

export default function PlayersPage() {
  return (
    <Suspense fallback={<Spinner label="Loading players" />}>
      <PlayersView />
    </Suspense>
  );
}
