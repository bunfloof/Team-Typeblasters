"use client";
// "Download full result as CSV": POSTs the statement to /api/sql/export,
// which streams every row; shows the bytes received, can be cancelled, then
// saves the file under the name the server gives.
import { useRef, useState } from "react";
import type { SqlErrorBody } from "./types";

export type ExportState =
  | { status: "idle" }
  | { status: "downloading"; sql: string; bytes: number }
  | { status: "done"; sql: string; bytes: number; fileName: string }
  | { status: "error"; sql: string; error: SqlErrorBody };

function fileNameFrom(disposition: string | null): string {
  return disposition?.match(/filename="([^"]+)"/)?.[1] || "typeblasters-sql.csv";
}

function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function useFullExport() {
  const [state, setState] = useState<ExportState>({ status: "idle" });
  const controllerRef = useRef<AbortController | null>(null);

  async function start(sql: string) {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    setState({ status: "downloading", sql, bytes: 0 });
    try {
      const response = await fetch("/api/sql/export", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ sql }),
        signal: controller.signal,
      });
      if (!response.ok || !response.body) {
        const body = (await response.json().catch(() => null)) as SqlErrorBody | null;
        setState({ status: "error", sql, error: body?.error ? body : { error: response.status + " " + response.statusText } });
        return;
      }
      const reader = response.body.getReader();
      const chunks: BlobPart[] = [];
      let bytes = 0;
      let shownAt = 0;
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
        bytes += value.byteLength;
        if (performance.now() - shownAt > 200) {
          shownAt = performance.now();
          setState({ status: "downloading", sql, bytes });
        }
      }
      const fileName = fileNameFrom(response.headers.get("Content-Disposition"));
      saveBlob(new Blob(chunks, { type: "text/csv;charset=utf-8" }), fileName);
      setState({ status: "done", sql, bytes, fileName });
    } catch (error) {
      if (controller.signal.aborted) {
        setState({ status: "idle" });
        return;
      }
      const message = error instanceof Error ? error.message : String(error);
      setState({ status: "error", sql, error: { error: "The download broke off: " + message } });
    } finally {
      if (controllerRef.current === controller) controllerRef.current = null;
    }
  }

  function cancel() {
    controllerRef.current?.abort();
  }

  return { state, start, cancel };
}
