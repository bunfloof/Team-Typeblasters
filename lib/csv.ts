// CSV writing (RFC 4180: quotes doubled, fields with , " or newlines quoted).
// Safe to import anywhere. Objects and arrays are written as JSON text.

export function csvCell(value: unknown): string {
  if (value === null || value === undefined) {
    return "";
  }
  let text: string;
  if (typeof value === "object") {
    text = JSON.stringify(value);
  } else {
    text = String(value);
  }
  if (/[",\r\n]/.test(text) || /^\s|\s$/.test(text)) {
    return '"' + text.replace(/"/g, '""') + '"';
  }
  return text;
}

export function csvLine(values: unknown[]): string {
  return values.map(csvCell).join(",") + "\r\n";
}

export function toCsv(columns: string[], rows: Record<string, unknown>[]): string {
  let out = csvLine(columns);
  for (const row of rows) {
    out += csvLine(columns.map((column) => row[column]));
  }
  return out;
}

// Every column name used by any of the rows, in first-seen order.
export function columnsOf(rows: Record<string, unknown>[], first: string[] = []): string[] {
  const seen = new Set<string>(first);
  const columns = [...first];
  for (const row of rows) {
    for (const key of Object.keys(row)) {
      if (!seen.has(key)) {
        seen.add(key);
        columns.push(key);
      }
    }
  }
  return columns;
}

// Starts a browser download of CSV text (client side only).
export function downloadCsv(fileName: string, csv: string): void {
  // The BOM makes Excel open the file as UTF-8.
  const blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName.endsWith(".csv") ? fileName : fileName + ".csv";
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
