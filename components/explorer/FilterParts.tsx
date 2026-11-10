"use client";
// Small inputs shared by the Explorer's two filter rows.
import type { KeyboardEvent, ReactNode } from "react";
import { inputClass } from "@/components/ui";
import { localInputToUtc, utcToLocalInput } from "./explorerShared";

// A labelled text box that applies the filters on Enter.
export function TextFilter({
  label,
  value,
  onChange,
  onSubmit,
  placeholder,
  width = "w-44",
  mono = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  onSubmit: () => void;
  placeholder?: string;
  width?: string;
  mono?: boolean;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-xs text-neutral-500">{label}</span>
      <input
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event: KeyboardEvent) => {
          if (event.key === "Enter") onSubmit();
        }}
        placeholder={placeholder}
        spellCheck={false}
        className={inputClass + " " + width + (mono ? " font-mono text-[13px]" : "")}
      />
    </label>
  );
}

// received_at bound: shown in the viewer's local time, stored in the URL as UTC.
export function TimeFilter({
  label,
  utc,
  onChange,
  onSubmit,
}: {
  label: string;
  utc: string;
  onChange: (utc: string) => void;
  onSubmit: () => void;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-xs text-neutral-500" title="Received at, in your local time (stored as UTC)">
        {label}
      </span>
      <input
        type="datetime-local"
        step={1}
        value={utcToLocalInput(utc)}
        onChange={(event) => onChange(localInputToUtc(event.target.value))}
        onKeyDown={(event: KeyboardEvent) => {
          if (event.key === "Enter") onSubmit();
        }}
        className={inputClass + " w-[200px]"}
      />
    </label>
  );
}

// A labelled control slot (for selects and toggles in the same row).
export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <span className="text-xs text-neutral-500">{label}</span>
      {children}
    </div>
  );
}
