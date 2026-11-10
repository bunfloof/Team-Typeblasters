// Small shared UI pieces (Tailwind). Server and client safe (no hooks here).
import type { ReactNode } from "react";

export function PageHeader({ title, description, actions }: { title: string; description?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-neutral-900">{title}</h1>
        {description ? <p className="mt-1 max-w-3xl text-sm text-neutral-500">{description}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </div>
  );
}

export function Card({
  title,
  subtitle,
  actions,
  children,
  className = "",
}: {
  title?: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={"rounded-xl border border-black/10 bg-[#fcfcfb] p-5 shadow-[0_1px_2px_rgba(0,0,0,0.03)] " + className}>
      {title || actions ? (
        <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div>
            {title ? <h2 className="text-[15px] font-semibold text-neutral-900">{title}</h2> : null}
            {subtitle ? <p className="mt-0.5 text-[13px] leading-snug text-neutral-500">{subtitle}</p> : null}
          </div>
          {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
        </div>
      ) : null}
      {children}
    </section>
  );
}

// A number that IS the chart (label, value, optional note).
export function StatTile({ label, value, note }: { label: string; value: ReactNode; note?: ReactNode }) {
  return (
    <div className="rounded-xl border border-black/10 bg-[#fcfcfb] px-4 py-3">
      <div className="text-[13px] text-neutral-500">{label}</div>
      <div className="mt-1 text-2xl font-semibold text-neutral-900">{value}</div>
      {note ? <div className="mt-0.5 text-xs text-neutral-500">{note}</div> : null}
    </div>
  );
}

export function Badge({ children, color }: { children: ReactNode; color?: string }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-black/10 bg-white px-2 py-0.5 text-xs text-neutral-700">
      {color ? <span className="h-2 w-2 rounded-full" style={{ backgroundColor: color }} /> : null}
      {children}
    </span>
  );
}

export function ErrorNote({ error, hint, title = "Could not load data" }: { error: string; hint?: ReactNode; title?: string }) {
  return (
    <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800">
      <div className="font-medium">{title}</div>
      <div className="mt-1 break-words font-mono text-xs">{error}</div>
      {hint ? <div className="mt-2 text-xs text-red-700">{hint}</div> : null}
    </div>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="rounded-lg border border-dashed border-neutral-300 px-4 py-10 text-center text-sm text-neutral-500">{children}</div>;
}

// Keeps the previous content visible (dimmed) while new data loads.
export function Hold({ loading, children }: { loading: boolean; children: ReactNode }) {
  return <div className={loading ? "pointer-events-none opacity-50 transition-opacity" : "transition-opacity"}>{children}</div>;
}

export function Spinner({ label = "Loading" }: { label?: string }) {
  return (
    <div className="flex items-center gap-2 py-8 text-sm text-neutral-500">
      <span className="h-4 w-4 animate-spin rounded-full border-2 border-neutral-300 border-t-blue-500" />
      {label}…
    </div>
  );
}

export const inputClass =
  "h-8 rounded-md border border-neutral-200 bg-white px-2 text-sm text-neutral-800 outline-none focus:border-blue-400";
export const buttonClass =
  "inline-flex h-8 items-center gap-1.5 rounded-md border border-neutral-200 bg-white px-3 text-sm text-neutral-700 hover:bg-neutral-50 disabled:cursor-not-allowed disabled:opacity-40";
export const primaryButtonClass =
  "inline-flex h-8 items-center gap-1.5 rounded-md bg-[#2a78d6] px-3 text-sm font-medium text-white hover:bg-[#256abf] disabled:cursor-not-allowed disabled:opacity-40";
