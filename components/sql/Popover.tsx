"use client";
// A button that opens a small panel below it (menus of the SQL console).
// Closes on a click outside, on Escape, or when the content calls close().
import { useEffect, useRef, useState, type ReactNode } from "react";
import { ChevronDown } from "lucide-react";
import { buttonClass } from "@/components/ui";

type Props = {
  label: ReactNode;
  title?: string;
  width?: number;
  align?: "left" | "right";
  children: (close: () => void) => ReactNode;
};

export default function Popover({ label, title, width = 360, align = "left", children }: Props) {
  const [open, setOpen] = useState(false);
  const boxRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onPointer = (event: MouseEvent) => {
      if (boxRef.current && !boxRef.current.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("mousedown", onPointer);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onPointer);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={boxRef} className="relative">
      <button type="button" className={buttonClass} title={title} aria-expanded={open} onClick={() => setOpen((value) => !value)}>
        {label}
        <ChevronDown className="h-3.5 w-3.5 text-neutral-400" />
      </button>
      {open ? (
        <div
          className={
            "absolute top-full z-30 mt-1 max-h-[70vh] overflow-y-auto rounded-lg border border-black/10 bg-white p-1 shadow-[0_8px_30px_rgba(0,0,0,0.12)] " +
            (align === "right" ? "right-0" : "left-0")
          }
          style={{ width: "min(" + width + "px, calc(100vw - 2rem))" }}
        >
          {children(() => setOpen(false))}
        </div>
      ) : null}
    </div>
  );
}
