// Small statistics helpers (pure functions, safe to import anywhere).

export function mean(values: number[]): number | null {
  if (values.length === 0) {
    return null;
  }
  return values.reduce((sum, v) => sum + v, 0) / values.length;
}

export function median(values: number[]): number | null {
  if (values.length === 0) {
    return null;
  }
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

// Half-width of the 95% confidence interval of the mean (normal approximation):
// the mean is probably within +- this. null below 2 values.
export function ci95(values: number[]): number | null {
  if (values.length < 2) {
    return null;
  }
  const m = mean(values) as number;
  const variance = values.reduce((sum, v) => sum + (v - m) * (v - m), 0) / (values.length - 1);
  return (1.96 * Math.sqrt(variance)) / Math.sqrt(values.length);
}

export function share(part: number, whole: number): number {
  return whole > 0 ? part / whole : 0;
}

// Counts per key, in first-seen order.
export function countBy<T>(items: T[], key: (item: T) => string): Map<string, number> {
  const counts = new Map<string, number>();
  for (const item of items) {
    const k = key(item);
    counts.set(k, (counts.get(k) ?? 0) + 1);
  }
  return counts;
}

export type Bin = { label: string; from: number; to: number }; // from <= value < to

// Bins of a fixed width covering the values (e.g. WPM 0-5, 5-10, ...),
// starting at a multiple of width; at most maxBins (the last one is open).
export function evenBins(values: number[], width: number, maxBins = 14, floor?: number): Bin[] {
  if (values.length === 0) {
    return [];
  }
  const low = floor ?? Math.floor(Math.min(...values) / width) * width;
  const high = Math.max(...values);
  const bins: Bin[] = [];
  for (let from = low; from <= high; from += width) {
    if (bins.length === maxBins - 1) {
      bins.push({ label: fmtEdge(from) + "+", from, to: Infinity });
      break;
    }
    bins.push({ label: fmtEdge(from) + "–" + fmtEdge(from + width), from, to: from + width });
  }
  return bins;
}

// Bins with hand-picked edges, e.g. [0, 5, 10, 30] -> 0–5, 5–10, 10–30, 30+.
export function edgeBins(edges: number[], unit = ""): Bin[] {
  return edges.map((from, i) => {
    const to = i + 1 < edges.length ? edges[i + 1] : Infinity;
    return { label: to === Infinity ? fmtEdge(from) + unit + "+" : fmtEdge(from) + "–" + fmtEdge(to) + unit, from, to };
  });
}

export function binCounts(values: number[], bins: Bin[]): number[] {
  const counts = bins.map(() => 0);
  for (const value of values) {
    const index = bins.findIndex((bin) => value >= bin.from && value < bin.to);
    if (index >= 0) {
      counts[index] += 1;
    }
  }
  return counts;
}

function fmtEdge(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}

// A stable pseudo-random offset in [-1, 1] for point i (jitter that does not
// move when the chart re-renders).
export function jitter(i: number, salt = 0): number {
  const x = Math.sin(i * 12.9898 + salt * 78.233) * 43758.5453;
  return (x - Math.floor(x)) * 2 - 1;
}
