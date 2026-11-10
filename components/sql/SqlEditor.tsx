"use client";
// The query editor: a monospace textarea with a line-number gutter.
//   Ctrl/Cmd+Enter runs, Tab inserts two spaces (indents selected lines;
//   Shift+Tab outdents), Esc then Tab moves focus out of the editor.
// Text is inserted with the browser's insertText command, so Ctrl+Z undoes
// insertions from the sidebar and menus too.
import { useImperativeHandle, useMemo, useRef, type KeyboardEvent, type Ref } from "react";

export type SqlEditorHandle = {
  insert: (text: string) => void; // at the cursor (replacing a selection)
  replaceAll: (text: string) => void; // undoable with Ctrl+Z
  selectedText: () => string; // "" when nothing is selected
};

type Props = {
  value: string;
  onChange: (value: string) => void;
  onRun: () => void;
  onSelectionChange?: (hasSelection: boolean) => void;
  ref?: Ref<SqlEditorHandle>;
};

const LINE_HEIGHT = 20; // px, shared by the gutter and the textarea so numbers line up

// Replaces the selection with `text` (keeping the undo history when the
// browser allows it).
function insertText(textarea: HTMLTextAreaElement, text: string, onChange: (value: string) => void) {
  textarea.focus();
  const inserted = typeof document.execCommand === "function" && document.execCommand("insertText", false, text);
  if (!inserted) {
    textarea.setRangeText(text, textarea.selectionStart, textarea.selectionEnd, "end");
    onChange(textarea.value);
  }
}

export default function SqlEditor({ value, onChange, onRun, onSelectionChange, ref }: Props) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const gutterRef = useRef<HTMLDivElement>(null);
  const tabLeavesRef = useRef(false); // set by Esc: the next Tab moves focus

  const lineNumbers = useMemo(() => {
    const count = value.split("\n").length;
    return Array.from({ length: count }, (_, index) => index + 1).join("\n");
  }, [value]);

  useImperativeHandle(
    ref,
    () => ({
      insert(text: string) {
        const textarea = textareaRef.current;
        if (!textarea) return;
        // Keep words apart: "SELECT" + "id" -> "SELECT id".
        const before = textarea.value.slice(0, textarea.selectionStart);
        const spacer = /[\w)'"`]$/.test(before) ? " " : "";
        insertText(textarea, spacer + text, onChange);
      },
      replaceAll(text: string) {
        const textarea = textareaRef.current;
        if (!textarea) return;
        textarea.focus();
        textarea.setSelectionRange(0, textarea.value.length);
        insertText(textarea, text, onChange);
        textarea.setSelectionRange(0, 0);
        textarea.scrollTop = 0;
        onSelectionChange?.(false);
      },
      selectedText() {
        const textarea = textareaRef.current;
        if (!textarea) return "";
        const text = textarea.value.slice(textarea.selectionStart, textarea.selectionEnd);
        return text.trim() ? text : "";
      },
    }),
    [onChange, onSelectionChange],
  );

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    const textarea = event.currentTarget;
    if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      onRun();
      return;
    }
    if (event.key === "Escape") {
      tabLeavesRef.current = true;
      return;
    }
    if (event.key !== "Tab" || event.ctrlKey || event.metaKey || event.altKey) {
      tabLeavesRef.current = false;
      return;
    }
    if (tabLeavesRef.current) {
      tabLeavesRef.current = false;
      return; // let the browser move focus
    }
    event.preventDefault();
    const { selectionStart: start, selectionEnd: end } = textarea;
    const text = textarea.value;
    if (!event.shiftKey && !text.slice(start, end).includes("\n")) {
      insertText(textarea, "  ", onChange);
      return;
    }
    // Indent (Tab) or outdent (Shift+Tab) every line the selection touches.
    const lineStart = text.lastIndexOf("\n", start - 1) + 1;
    const block = text.slice(lineStart, end);
    const changed = block
      .split("\n")
      .map((line) => (event.shiftKey ? line.replace(/^ {1,2}/, "") : "  " + line))
      .join("\n");
    if (changed === block) return;
    textarea.setSelectionRange(lineStart, end);
    insertText(textarea, changed, onChange);
    textarea.setSelectionRange(lineStart, lineStart + changed.length);
  };

  return (
    <div
      className="flex resize-y overflow-hidden rounded-lg border border-neutral-200 bg-white focus-within:border-blue-400"
      style={{ height: 260, minHeight: 120 }}
    >
      <div
        ref={gutterRef}
        aria-hidden
        className="shrink-0 select-none overflow-hidden border-r border-neutral-100 bg-[#f6f6f3] px-2 text-right font-mono text-[12px] tabular-nums text-neutral-400"
      >
        {/* bottom padding leaves room for the textarea's horizontal scrollbar */}
        <pre className="pb-10 pt-2 font-mono" style={{ lineHeight: LINE_HEIGHT + "px" }}>
          {lineNumbers}
        </pre>
      </div>
      <textarea
        ref={textareaRef}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={onKeyDown}
        onSelect={(event) => onSelectionChange?.(event.currentTarget.selectionStart !== event.currentTarget.selectionEnd)}
        onScroll={(event) => {
          if (gutterRef.current) gutterRef.current.scrollTop = event.currentTarget.scrollTop;
        }}
        wrap="off"
        spellCheck={false}
        autoCapitalize="off"
        autoComplete="off"
        aria-label="SQL query"
        placeholder="SELECT * FROM events ORDER BY id DESC LIMIT 100"
        className="h-full min-w-0 flex-1 resize-none bg-transparent px-3 pt-2 font-mono text-[13px] text-neutral-900 outline-none placeholder:text-neutral-400"
        style={{ lineHeight: LINE_HEIGHT + "px", whiteSpace: "pre", tabSize: 2 }}
      />
    </div>
  );
}
