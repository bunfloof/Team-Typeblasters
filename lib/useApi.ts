"use client";
// Fetches JSON from one of our API routes and keeps the previous result on
// screen while a new one loads (no flash of empty content on refetch). If the
// new request fails, the old result stays only when it was for the same URL
// (a failed refresh), never under different filters.
import { useCallback, useEffect, useState } from "react";

type Loaded<T> = { url: string; data: T | null; error: string | null; configError: boolean };

export type ApiState<T> = {
  data: T | null; // the latest successful result (kept while refetching)
  error: string | null;
  configError: boolean; // the error is "fill in .env.local" (the API answered {config: true})
  loading: boolean;
  reload: () => void;
};

class ApiError extends Error {
  constructor(
    message: string,
    public config: boolean,
  ) {
    super(message);
  }
}

export function useApi<T>(url: string | null): ApiState<T> {
  const [loaded, setLoaded] = useState<Loaded<T> | null>(null);
  const [reloadCount, setReloadCount] = useState(0);
  const [last, setLast] = useState<{ url: string; data: T } | null>(null);

  useEffect(() => {
    if (!url) {
      return;
    }
    const controller = new AbortController();
    const key = url + "#" + reloadCount;
    fetch(url, { signal: controller.signal, cache: "no-store" })
      .then(async (response) => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new ApiError((body && body.error) || response.status + " " + response.statusText, !!(body && body.config));
        }
        return body as T;
      })
      .then((data) => {
        setLast({ url, data });
        setLoaded({ url: key, data, error: null, configError: false });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) {
          return;
        }
        setLoaded({
          url: key,
          data: null,
          error: error instanceof Error ? error.message : String(error),
          configError: error instanceof ApiError && error.config,
        });
      });
    return () => controller.abort();
  }, [url, reloadCount]);

  const key = url ? url + "#" + reloadCount : null;
  const current = loaded && loaded.url === key ? loaded : null;
  const kept = last && last.url === url ? last.data : null; // a failed refresh keeps the last good result
  const reload = useCallback(() => setReloadCount((count) => count + 1), []);
  return {
    data: !current ? (last?.data ?? null) : current.error ? kept : current.data,
    error: current?.error ?? null,
    configError: current?.configError ?? false,
    loading: !!url && !current,
    reload,
  };
}

// "/api/x" + params -> "/api/x?a=1&b=2" (empty values left out).
export function apiUrl(path: string, params?: URLSearchParams | Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();
  if (params instanceof URLSearchParams) {
    params.forEach((value, name) => search.append(name, value));
  } else if (params) {
    for (const [name, value] of Object.entries(params)) {
      if (value === null || value === undefined || value === "" || value === false) {
        continue;
      }
      search.set(name, value === true ? "1" : String(value));
    }
  }
  const text = search.toString();
  return text ? path + "?" + text : path;
}
