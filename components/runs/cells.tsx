"use client";
// AG Grid cell renderers for the runs and players tables. The color is only
// a dot next to neutral text (the text says what the color means).
import type { CustomCellRendererProps } from "ag-grid-react";
import { Badge } from "@/components/ui";
import { INK, KIND_COLORS } from "@/lib/theme";
import { killerKind, killerLabel, OUTCOME_LABELS, outcomeColor } from "./display";

export function OutcomeCell({ value }: CustomCellRendererProps) {
  if (!value) {
    return null;
  }
  const outcome = String(value);
  return <Badge color={outcomeColor(outcome)}>{OUTCOME_LABELS[outcome] || outcome}</Badge>;
}

export function KillerCell({ value }: CustomCellRendererProps) {
  if (!value) {
    return null;
  }
  const kind = killerKind(String(value));
  return (
    <span className="inline-flex items-center gap-1.5">
      <span
        className="h-2 w-2 shrink-0 rounded-full"
        style={{ backgroundColor: kind ? KIND_COLORS[kind] || KIND_COLORS.normal : INK.secondary }}
      />
      {killerLabel(String(value))}
    </span>
  );
}

export function TestCell({ value }: CustomCellRendererProps) {
  return value ? <Badge>test</Badge> : null;
}
