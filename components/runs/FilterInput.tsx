"use client";
// A text filter kept in the URL: typing updates the field at once and the URL
// (via onCommit) after a short pause or on Enter. When the URL value changes
// from outside (Clear filters, back button), the field follows it.
import { useEffect, useRef, useState } from "react";
import { Search } from "lucide-react";

type Props = {
  value: string; // the value in the URL
  onCommit: (value: string) => void;
  placeholder: string;
  label: string; // for screen readers
  width?: string; // Tailwind width class
  delayMs?: number;
};

export default function FilterInput({ value, onCommit, placeholder, label, width = "w-56", delayMs = 400 }: Props) {
  const [text, setText] = useState(value);
  const [urlValue, setUrlValue] = useState(value); // the URL value this field last saw
  const [committed, setCommitted] = useState(value); // what this field last sent to the URL
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // The URL changed: follow it, unless it is our own commit arriving.
  if (value !== urlValue) {
    setUrlValue(value);
    if (value !== committed) {
      setText(value);
      setCommitted(value);
    }
  }

  // Leaving the page drops a pending commit (it would navigate back here).
  useEffect(() => {
    const pending = timer;
    return () => {
      if (pending.current) {
        clearTimeout(pending.current);
      }
    };
  }, []);

  function clearPending() {
    if (timer.current) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }

  function commit(next: string) {
    clearPending();
    setCommitted(next);
    onCommit(next);
  }

  return (
    <label className="relative">
      <span className="sr-only">{label}</span>
      <Search className="pointer-events-none absolute left-2 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-neutral-400" />
      <input
        value={text}
        placeholder={placeholder}
        onChange={(event) => {
          const next = event.target.value;
          setText(next);
          clearPending();
          timer.current = setTimeout(() => commit(next), delayMs);
        }}
        onKeyDown={(event) => {
          if (event.key === "Enter") {
            commit(text);
          }
        }}
        className={
          "h-8 rounded-md border border-neutral-200 bg-white pl-7 pr-2 text-sm text-neutral-800 outline-none focus:border-blue-400 " +
          width
        }
      />
    </label>
  );
}
