"use client";
// Every event of the run as one table: the columns, then each data field
// flattened into its own column (catalog order), with CSV export. Clicking a
// row selects its time on the timeline.
import { useMemo } from "react";
import DataGrid from "@/components/DataGrid";
import { ENVELOPE_FIELDS, fieldsOf } from "@/lib/catalog";
import { flattenEvents, type Timeline } from "@/lib/timelineData";

// Kept outside the component: a new array every render would rebuild the grid's columns.
const PINNED = ["seq", "time", "event"];

type Props = {
  timeline: Timeline;
  fileName: string;
  onSelectT: (t: number) => void;
};

export default function RunEventsTable({ timeline, fileName, onSelectT }: Props) {
  const { rows, columns } = useMemo(() => {
    // Field order: the catalog's, event type by event type, as they appear.
    const order: string[] = Object.keys(ENVELOPE_FIELDS);
    for (const type of new Set(timeline.events.map((e) => e.event))) {
      for (const field of Object.keys(fieldsOf(type))) {
        if (!order.includes(field)) order.push(field);
      }
    }
    return flattenEvents(timeline.events, order);
  }, [timeline]);

  return (
    <DataGrid
      rows={rows}
      columns={columns}
      height={560}
      fileName={fileName}
      pinnedLeft={PINNED}
      onRowClicked={(row) => {
        if (typeof row.t_play === "number") onSelectT(row.t_play);
      }}
    />
  );
}
