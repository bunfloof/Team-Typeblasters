// AG Grid column definitions for a SQL result: the MySQL type in the header
// tooltip, number filters for numeric columns, and a sort that orders
// numbers stored as text (JSON_UNQUOTE results, DECIMAL) as numbers.
import type { ColDef, ValueFormatterParams } from "ag-grid-community";
import { columnTypeName, isNumericType, type SqlRunResponse } from "./types";

// "12", "-3.5", 7 -> numbers; anything else -> null.
function asNumber(value: unknown): number | null {
  if (typeof value === "number") return Number.isFinite(value) ? value : null;
  if (typeof value === "string" && value.trim() !== "" && /^-?\d*\.?\d+(e[-+]?\d+)?$/i.test(value.trim())) {
    return Number(value);
  }
  return null;
}

// Empty values last, numbers by value, the rest as text.
export function smartCompare(a: unknown, b: unknown): number {
  const emptyA = a === null || a === undefined || a === "";
  const emptyB = b === null || b === undefined || b === "";
  if (emptyA || emptyB) return emptyA === emptyB ? 0 : emptyA ? 1 : -1;
  const numberA = asNumber(a);
  const numberB = asNumber(b);
  if (numberA !== null && numberB !== null) return numberA - numberB;
  return String(a).localeCompare(String(b), undefined, { numeric: true });
}

function formatValue(params: ValueFormatterParams): string {
  const value = params.value;
  if (value === null || value === undefined) return "";
  return typeof value === "object" ? JSON.stringify(value) : String(value);
}

export function buildColumnDefs(columns: SqlRunResponse["columns"]): ColDef[] {
  return columns.map((column, index) => {
    const numeric = isNumericType(column.type);
    return {
      // Two result columns can share a name (SELECT e.id, r.id); colId keeps them apart.
      colId: column.name + "#" + index,
      field: column.name,
      headerName: column.name,
      headerTooltip: column.name + (column.type !== null ? " · " + columnTypeName(column.type) : ""),
      valueFormatter: formatValue,
      tooltipValueGetter: (params) => formatValue(params as unknown as ValueFormatterParams),
      comparator: smartCompare,
      filter: numeric ? "agNumberColumnFilter" : "agTextColumnFilter",
      filterValueGetter: numeric ? (params) => asNumber(params.data?.[column.name]) : undefined,
      type: numeric ? "rightAligned" : undefined,
      cellClass: numeric ? "tabular" : undefined,
    };
  });
}

// Column names used more than once (the rows only keep the last one).
export function duplicateNames(columns: SqlRunResponse["columns"]): string[] {
  const seen = new Set<string>();
  const duplicates = new Set<string>();
  for (const column of columns) {
    if (seen.has(column.name)) duplicates.add(column.name);
    seen.add(column.name);
  }
  return [...duplicates];
}
