// Text for the runs and players tables. Safe to import anywhere.
import { KIND_LABELS, OUTCOME_COLORS } from "@/lib/theme";

export const OUTCOME_LABELS: Record<string, string> = {
  won: "Won",
  lost: "Lost",
  restart: "Restarted",
  abandoned: "Abandoned",
  playing: "Playing",
};

export function outcomeColor(outcome: string): string {
  return OUTCOME_COLORS[outcome] || OUTCOME_COLORS.abandoned;
}

// The zombie kind in a damage source ("zombie_runner" -> "runner"), else null.
export function killerKind(source: string | null | undefined): string | null {
  return source && source.startsWith("zombie_") ? source.slice("zombie_".length) : null;
}

// "zombie_runner" -> "Runner zombie", "orb" -> "Energy orb".
export function killerLabel(source: string | null | undefined): string {
  if (!source) {
    return "";
  }
  const kind = killerKind(source);
  if (kind) {
    return (KIND_LABELS[kind] || kind) + " zombie";
  }
  return source === "orb" ? "Energy orb" : source;
}

// A user agent shortened to browser + version + system, e.g. "Chrome 141 · Windows".
// The full text stays in its own column.
export function shortUserAgent(ua: string | null | undefined): string {
  if (!ua) {
    return "";
  }
  const browsers: [RegExp, string][] = [
    [/Edg(?:e|A|iOS)?\/(\d+)/, "Edge"],
    [/OPR\/(\d+)/, "Opera"],
    [/SamsungBrowser\/(\d+)/, "Samsung Internet"],
    [/Firefox\/(\d+)/, "Firefox"],
    [/FxiOS\/(\d+)/, "Firefox"],
    [/CriOS\/(\d+)/, "Chrome"],
    [/Chrome\/(\d+)/, "Chrome"],
    [/Version\/(\d+)[\d.]* .*Safari\//, "Safari"],
  ];
  let browser = "";
  for (const [pattern, name] of browsers) {
    const match = ua.match(pattern);
    if (match) {
      browser = name + " " + match[1];
      break;
    }
  }
  const systems: [RegExp, string][] = [
    [/iPhone|iPad|iPod/, "iOS"],
    [/Android/, "Android"],
    [/CrOS/, "ChromeOS"],
    [/Windows/, "Windows"],
    [/Mac OS X|Macintosh/, "macOS"],
    [/Linux/, "Linux"],
  ];
  const system = systems.find(([pattern]) => pattern.test(ua))?.[1] || "";
  const text = [browser, system].filter(Boolean).join(" · ");
  return text || ua.slice(0, 40);
}

// Device in one line: "Windows 10 64bit · 1920x1080".
export function deviceText(os: string | null, screen: string | null): string {
  return [os, screen].filter(Boolean).join(" · ");
}

export function median(values: number[]): number | null {
  if (values.length === 0) {
    return null;
  }
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

// The hint under an error: the database password is missing from .env.local.
export function errorHint(error: string): string | undefined {
  return error.includes(".env.local")
    ? "Fill in DB_PASSWORD in .env.local (next to package.json), then restart the dashboard (pnpm dev, or pnpm start on the server)."
    : undefined;
}
