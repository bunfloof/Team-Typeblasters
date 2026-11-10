"use client";
// Conditions on fields inside an event's data, e.g. kind = runner or
// health < 30. The field box suggests keys seen for the chosen event types.
import { useId, type KeyboardEvent } from "react";
import { X } from "lucide-react";
import type { JsonCondition } from "@/lib/filters";
import { inputClass } from "@/components/ui";
import { jsonConditionProblem } from "./explorerShared";

export const JSON_OPS: { value: JsonCondition["op"]; label: string }[] = [
  { value: "=", label: "=" },
  { value: "!=", label: "≠" },
  { value: ">", label: ">" },
  { value: ">=", label: "≥" },
  { value: "<", label: "<" },
  { value: "<=", label: "≤" },
  { value: "contains", label: "contains" },
];

type Props = {
  conditions: JsonCondition[];
  fieldOptions: string[];
  onChange: (conditions: JsonCondition[]) => void;
  onSubmit: () => void;
};

export default function JsonConditions({ conditions, fieldOptions, onChange, onSubmit }: Props) {
  const listId = useId();
  if (conditions.length === 0) {
    return null;
  }

  const update = (index: number, patch: Partial<JsonCondition>) =>
    onChange(conditions.map((condition, i) => (i === index ? { ...condition, ...patch } : condition)));

  const submitOnEnter = (event: KeyboardEvent) => {
    if (event.key === "Enter") onSubmit();
  };

  return (
    <>
      <datalist id={listId}>
        {fieldOptions.map((field) => (
          <option key={field} value={field} />
        ))}
      </datalist>
      {conditions.map((condition, index) => (
        <div key={index} className="flex items-center gap-1 rounded-lg border border-black/10 bg-white py-0.5 pl-2 pr-0.5">
          <span className="text-xs text-neutral-400">data.</span>
          <input
            list={listId}
            value={condition.field}
            onChange={(event) => update(index, { field: event.target.value.replace(/[^A-Za-z0-9_]/g, "") })}
            onKeyDown={submitOnEnter}
            placeholder="field"
            aria-label="Data field"
            className={inputClass + " h-7 w-44 font-mono text-[13px]"}
          />
          <select
            value={condition.op}
            onChange={(event) => update(index, { op: event.target.value as JsonCondition["op"] })}
            aria-label="Operator"
            className={inputClass + " h-7"}
          >
            {JSON_OPS.map((op) => (
              <option key={op.value} value={op.value}>
                {op.label}
              </option>
            ))}
          </select>
          <input
            value={condition.value}
            onChange={(event) => update(index, { value: event.target.value })}
            onKeyDown={submitOnEnter}
            placeholder={condition.op === "=" || condition.op === "!=" || condition.op === "contains" ? "value" : "number"}
            aria-label="Value"
            aria-invalid={condition.field && condition.value && jsonConditionProblem(condition) ? true : undefined}
            title={(condition.field && condition.value && jsonConditionProblem(condition)) || undefined}
            className={inputClass + " h-7 w-32 aria-[invalid=true]:border-red-400 aria-[invalid=true]:bg-red-50"}
          />
          <button
            type="button"
            onClick={() => onChange(conditions.filter((_, i) => i !== index))}
            className="rounded p-1 text-neutral-400 hover:bg-neutral-100 hover:text-neutral-700"
            aria-label="Remove condition"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      ))}
    </>
  );
}
