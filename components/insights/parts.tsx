"use client";
// Small layout pieces of the Insights page.
import type { ReactNode } from "react";

// One question of the page: a label ("Question 1"), the question, a short intro.
export function Section({ id, label, title, intro, children }: { id: string; label: string; title: string; intro: ReactNode; children: ReactNode }) {
  return (
    <section id={id} className="scroll-mt-16 border-t border-black/10 pt-8">
      <div className="mb-5">
        <div className="text-xs font-medium uppercase tracking-wide text-neutral-500">{label}</div>
        <h2 className="mt-1 text-xl font-semibold tracking-tight text-neutral-900">{title}</h2>
        <p className="mt-1 max-w-3xl text-sm leading-relaxed text-neutral-500">{intro}</p>
      </div>
      <div className="flex flex-col gap-4">{children}</div>
    </section>
  );
}

// A row of buttons where one is selected (e.g. All runs / First run / Repeat runs).
export function Segmented<T extends string>({
  value,
  options,
  onChange,
  ariaLabel,
}: {
  value: T;
  options: { value: T; label: string }[];
  onChange: (value: T) => void;
  ariaLabel: string;
}) {
  return (
    <div role="radiogroup" aria-label={ariaLabel} className="inline-flex rounded-md border border-neutral-200 bg-white p-0.5">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          role="radio"
          aria-checked={option.value === value}
          onClick={() => onChange(option.value)}
          className={
            "rounded px-2.5 py-1 text-sm " +
            (option.value === value ? "bg-[#2a78d6]/10 font-medium text-[#1c5cab]" : "text-neutral-600 hover:bg-neutral-50")
          }
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

// "n = 42" in a subtitle.
export function N({ n, noun }: { n: number; noun: string }) {
  return (
    <span className="tabular">
      n = {n.toLocaleString()} {noun}
    </span>
  );
}

// The hint under an ErrorNote: what to do when the database is not configured.
export function configHint(error: string): string {
  return error.includes(".env.local")
    ? "Fill in DB_PASSWORD (and the other DB_ settings) in .env.local, then restart the dashboard (pnpm dev, or pnpm start on the server)."
    : "Check that the database is reachable, then press Refresh.";
}
