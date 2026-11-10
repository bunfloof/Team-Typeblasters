// Number and time formatting for the UI. Safe to import anywhere.

const integer = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });
const oneDecimal = new Intl.NumberFormat("en-US", { maximumFractionDigits: 1 });
const compact = new Intl.NumberFormat("en-US", { notation: "compact", maximumFractionDigits: 1 });

export function fmtInt(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value) ? "-" : integer.format(value);
}

export function fmt1(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value) ? "-" : oneDecimal.format(value);
}

export function fmtCompact(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value) ? "-" : compact.format(value);
}

export function fmtPct(value: number | null | undefined, digits = 0): string {
  return value === null || value === undefined || !Number.isFinite(value) ? "-" : (value * 100).toFixed(digits) + "%";
}

// Seconds as m:ss (or h:mm:ss), e.g. 83.4 -> "1:23".
export function fmtDuration(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) {
    return "-";
  }
  const total = Math.max(0, Math.round(seconds));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const ss = String(s).padStart(2, "0");
  return h > 0 ? h + ":" + String(m).padStart(2, "0") + ":" + ss : m + ":" + ss;
}

// A long id shortened for display: "3f2a9c…b41d".
export function shortId(id: string | null | undefined, keep = 6): string {
  if (!id) {
    return "-";
  }
  const prefix = id.startsWith("qa-") ? "qa-" : "";
  const body = prefix ? id.slice(3) : id;
  return body.length <= keep * 2 + 1 ? id : prefix + body.slice(0, keep) + "…" + body.slice(-4);
}

// "2026-10-13 02:01:27.063688" (UTC) -> local time text.
export function fmtTime(utc: string | null | undefined): string {
  if (!utc) {
    return "-";
  }
  const date = new Date(utc.replace(" ", "T") + "Z");
  return Number.isNaN(date.getTime()) ? utc : date.toLocaleString();
}

export function isTestInstall(installId: string | null | undefined): boolean {
  return !!installId && installId.startsWith("qa-");
}
