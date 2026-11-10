"use client";
// The frame around every page: the sidebar (navigation) and the top bar with
// the global filters (test data, level, time range) and the database status.
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Suspense, useEffect, useRef, type ReactNode } from "react";
import { Activity, BookOpen, Database, Gauge, LayoutDashboard, Lightbulb, Table2, Users } from "lucide-react";
import { setGlobalFilters, useGlobalFilters } from "@/lib/globalFilters";
import { apiUrl, useApi } from "@/lib/useApi";
import { levelName } from "@/lib/catalog";
import { fmtCompact, fmtTime } from "@/lib/format";
import type { MetaResponse } from "@/lib/types";
import TimeRangeControl from "./TimeRangeControl";

const NAV = [
  { href: "/", label: "Overview", icon: LayoutDashboard },
  { href: "/insights", label: "Insights", icon: Lightbulb },
  { href: "/runs", label: "Runs & timeline", icon: Activity },
  { href: "/players", label: "Players", icon: Users },
  { href: "/explorer", label: "Explorer", icon: Table2 },
  { href: "/sql", label: "SQL", icon: Database },
  { href: "/catalog", label: "Event catalog", icon: BookOpen },
];

function isActive(pathname: string, href: string): boolean {
  return href === "/" ? pathname === "/" : pathname === href || pathname.startsWith(href + "/");
}

// The nav links. usePathname() reads the URL, which on dynamic routes
// (/runs/[runId]) is request data, so this part sits inside <Suspense> and
// renders without the "active" highlight until the URL is known.
function NavLinks({ mobile }: { mobile?: boolean }) {
  const pathname = usePathname();
  return <NavList pathname={pathname} mobile={mobile} />;
}

function NavList({ pathname, mobile }: { pathname: string | null; mobile?: boolean }) {
  if (mobile) {
    return (
      <nav className="flex gap-3 text-sm md:hidden">
        {NAV.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={pathname && isActive(pathname, item.href) ? "font-medium text-[#1c5cab]" : "text-neutral-600"}
          >
            {item.label}
          </Link>
        ))}
      </nav>
    );
  }
  return (
    <nav className="flex flex-col gap-0.5">
      {NAV.map((item) => {
        const Icon = item.icon;
        const active = !!pathname && isActive(pathname, item.href);
        return (
          <Link
            key={item.href}
            href={item.href}
            className={
              "flex items-center gap-2.5 rounded-md px-2 py-1.5 text-sm " +
              (active ? "bg-[#2a78d6]/10 font-medium text-[#1c5cab]" : "text-neutral-600 hover:bg-black/[0.04]")
            }
          >
            <Icon className="h-4 w-4" />
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}

// Re-reads the database status (and the list of levels) on every page change
// and once a minute, so a database that comes back, or a level that gets its
// first plays, shows up without reloading the browser.
function MetaRefresher({ reload }: { reload: () => void }) {
  const pathname = usePathname();
  const shown = useRef<string | null>(null);
  useEffect(() => {
    if (shown.current !== null && shown.current !== pathname) {
      reload(); // (the first page already loaded it)
    }
    shown.current = pathname;
  }, [pathname, reload]);
  useEffect(() => {
    const timer = setInterval(reload, 60000);
    return () => clearInterval(timer);
  }, [reload]);
  return null;
}

export default function AppShell({ children }: { children: ReactNode }) {
  const filters = useGlobalFilters();
  const meta = useApi<MetaResponse>(apiUrl("/api/meta", { includeTest: true }));

  return (
    <div className="flex min-h-screen bg-[#f9f9f7] text-neutral-900">
      <aside className="sticky top-0 hidden h-screen w-56 shrink-0 flex-col border-r border-black/10 bg-[#fcfcfb] px-3 py-4 md:flex">
        <Link href="/" className="mb-6 flex items-center gap-2 px-2">
          <Gauge className="h-5 w-5 text-[#2a78d6]" />
          <span className="text-[15px] font-semibold leading-tight">
            Typeblasters
            <span className="block text-xs font-normal text-neutral-500">Playtest analytics</span>
          </span>
        </Link>
        <Suspense fallback={<NavList pathname={null} />}>
          <NavLinks />
          <MetaRefresher reload={meta.reload} />
        </Suspense>
        <div className="mt-auto px-2 text-xs leading-relaxed text-neutral-500">
          {meta.error ? (
            <span className="text-red-700">Database not reachable</span>
          ) : meta.data ? (
            <>
              {fmtCompact(meta.data.totals.events)} events · {fmtCompact(meta.data.totals.requests)} requests
              <br />
              last {fmtTime(meta.data.lastReceived)}
            </>
          ) : (
            "Connecting…"
          )}
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex flex-wrap items-center gap-4 border-b border-black/10 bg-[#f9f9f7]/90 px-6 py-2.5 backdrop-blur">
          <Suspense fallback={<NavList pathname={null} mobile />}>
            <NavLinks mobile />
          </Suspense>
          <label className="flex items-center gap-2 text-sm text-neutral-700">
            <input
              type="checkbox"
              checked={filters.includeTest}
              onChange={(event) => setGlobalFilters({ includeTest: event.target.checked })}
              className="h-4 w-4 accent-[#2a78d6]"
            />
            Include test plays (qa-)
            {meta.data && !filters.includeTest && meta.data.totals.testEvents > 0 ? (
              <span className="text-xs text-neutral-500">· {fmtCompact(meta.data.totals.testEvents)} test events hidden</span>
            ) : null}
          </label>
          <label className="flex items-center gap-2 text-sm text-neutral-700">
            Level
            <select
              value={filters.level}
              onChange={(event) => setGlobalFilters({ level: event.target.value })}
              className="h-8 rounded-md border border-neutral-200 bg-white px-2 text-sm"
            >
              <option value="">All levels</option>
              {(meta.data?.levels || []).map((level) => (
                <option key={level} value={level}>
                  {levelName(level)}
                </option>
              ))}
            </select>
          </label>
          <TimeRangeControl />
          {meta.error ? (
            <span className="ml-auto rounded-md bg-red-50 px-2 py-1 text-xs text-red-800">
              {meta.error.includes(".env.local") ? meta.error : "Database error: " + meta.error}
            </span>
          ) : null}
        </header>
        <main className="min-w-0 flex-1 px-6 py-6">{children}</main>
      </div>
    </div>
  );
}
