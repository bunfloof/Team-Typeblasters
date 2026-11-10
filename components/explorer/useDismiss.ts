"use client";
// Calls onDismiss when the user clicks outside `ref` or presses Escape
// while `active` (closing a dropdown panel or the row drawer).
import { useEffect, useEffectEvent, type RefObject } from "react";

export function useDismiss(ref: RefObject<HTMLElement | null>, active: boolean, onDismiss: () => void, outsideClick = true) {
  const dismiss = useEffectEvent(onDismiss);

  useEffect(() => {
    if (!active) {
      return;
    }
    const onMouseDown = (event: MouseEvent) => {
      if (outsideClick && ref.current && !ref.current.contains(event.target as Node)) {
        dismiss();
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        dismiss();
      }
    };
    document.addEventListener("mousedown", onMouseDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onMouseDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [active, outsideClick, ref]);
}
