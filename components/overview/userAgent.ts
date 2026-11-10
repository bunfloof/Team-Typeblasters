// A user-agent string as a short "Browser version · OS" label, e.g.
// "Chrome 141 · Windows". Safe to import anywhere. Good enough for a table
// column; the full string stays available next to it.

const BROWSERS: [name: string, pattern: RegExp][] = [
  ["Edge", /Edg(?:e|A|iOS)?\/(\d+)/],
  ["Opera", /OPR\/(\d+)/],
  ["Samsung Internet", /SamsungBrowser\/(\d+)/],
  ["Firefox", /(?:Firefox|FxiOS)\/(\d+)/],
  ["Chrome", /(?:Chrome|CriOS)\/(\d+)/],
  ["Safari", /Version\/(\d+)[\d.]* (?:Mobile\/\S+ )?Safari\//],
];

function osOf(ua: string): string {
  if (/iPhone|iPad|iPod/.test(ua)) return "iOS";
  if (/Android/.test(ua)) return "Android";
  if (/Windows NT/.test(ua)) return "Windows";
  if (/CrOS/.test(ua)) return "ChromeOS";
  if (/Mac OS X|Macintosh/.test(ua)) return "macOS";
  if (/Linux/.test(ua)) return "Linux";
  return "";
}

export function browserLabel(ua: string | null | undefined): string {
  if (!ua) {
    return "-";
  }
  let browser = "";
  for (const [name, pattern] of BROWSERS) {
    const match = ua.match(pattern);
    if (match) {
      browser = name + " " + match[1];
      break;
    }
  }
  if (!browser) {
    // Bots and tools (curl/8.4, python-requests/2.31...): their first word.
    browser = ua.split(/[\s;(]/)[0].slice(0, 40) || "Unknown";
  }
  const os = osOf(ua);
  return os ? browser + " · " + os : browser;
}
