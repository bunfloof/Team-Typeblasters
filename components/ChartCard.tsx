"use client";
// A card with a chart and its table twin (every chart's numbers are also
// readable as a table, and exportable as CSV).
//   <ChartCard title="Deaths by wave" subtitle="..." option={option} rows={rows} />
import { useState, type ReactNode } from "react";
import type { EChartsOption } from "echarts";
import { BarChart3, Table2 } from "lucide-react";
import Chart from "./Chart";
import DataGrid from "./DataGrid";
import { Card, EmptyState } from "./ui";

type Props = {
  title: ReactNode;
  subtitle?: ReactNode;
  option: EChartsOption;
  rows: Record<string, unknown>[]; // the numbers behind the chart
  columns?: string[];
  height?: number;
  fileName?: string;
  empty?: boolean; // nothing to show yet
  emptyText?: ReactNode;
  footnote?: ReactNode; // how to read it / caveats
  actions?: ReactNode;
  className?: string;
  getRowId?: (row: Record<string, unknown>) => string; // table view: keeps ticked rows across refreshes
};

export default function ChartCard({
  title,
  subtitle,
  option,
  rows,
  columns,
  height = 300,
  fileName = "chart-data",
  empty,
  emptyText = "No data for these filters yet.",
  footnote,
  actions,
  className,
  getRowId,
}: Props) {
  const [view, setView] = useState<"chart" | "table">("chart");
  const toggle = (
    <div className="flex rounded-md border border-neutral-200 bg-white p-0.5">
      <button
        type="button"
        onClick={() => setView("chart")}
        className={"rounded px-2 py-1 " + (view === "chart" ? "bg-neutral-100 text-neutral-900" : "text-neutral-500")}
        aria-label="Chart view"
        title="Chart"
      >
        <BarChart3 className="h-3.5 w-3.5" />
      </button>
      <button
        type="button"
        onClick={() => setView("table")}
        className={"rounded px-2 py-1 " + (view === "table" ? "bg-neutral-100 text-neutral-900" : "text-neutral-500")}
        aria-label="Table view"
        title="Table"
      >
        <Table2 className="h-3.5 w-3.5" />
      </button>
    </div>
  );
  return (
    <Card
      title={title}
      subtitle={subtitle}
      className={className}
      actions={
        <>
          {actions}
          {toggle}
        </>
      }
    >
      {empty ? (
        <EmptyState>{emptyText}</EmptyState>
      ) : view === "chart" ? (
        <Chart option={option} height={height} />
      ) : (
        <DataGrid rows={rows} columns={columns} height={Math.max(height, 260)} fileName={fileName} getRowId={getRowId} />
      )}
      {footnote ? <p className="mt-3 text-xs leading-relaxed text-neutral-500">{footnote}</p> : null}
    </Card>
  );
}
