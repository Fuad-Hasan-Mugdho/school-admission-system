import { useCallback, useEffect, useRef, useState } from 'react';

export interface PollingState<T> {
  data: T | null;
  error: unknown;
  loading: boolean;
  /** Re-fetch immediately (e.g. right after a mutation). */
  refresh: () => Promise<void>;
  /** Replace the cached value locally without waiting for the next poll. */
  setData: (updater: (previous: T | null) => T | null) => void;
}

/**
 * Fetch now and then every `intervalMs` while mounted. Overlapping requests are skipped and a
 * fetch that resolves after unmount is ignored. Pass `enabled=false` to pause (e.g. no student id yet).
 */
export function usePolling<T>(fetcher: () => Promise<T>, intervalMs: number, enabled = true): PollingState<T> {
  const [data, setDataState] = useState<T | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState<boolean>(enabled);
  const inFlight = useRef(false);
  const mounted = useRef(true);
  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;

  const run = useCallback(async () => {
    if (inFlight.current) return;
    inFlight.current = true;
    try {
      const result = await fetcherRef.current();
      if (!mounted.current) return;
      setDataState(result);
      setError(null);
    } catch (e) {
      if (!mounted.current) return;
      setError(e);
    } finally {
      inFlight.current = false;
      if (mounted.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    mounted.current = true;
    if (!enabled) {
      setLoading(false);
      return () => {
        mounted.current = false;
      };
    }
    setLoading(true);
    void run();
    const id = window.setInterval(() => void run(), intervalMs);
    return () => {
      mounted.current = false;
      window.clearInterval(id);
    };
  }, [run, intervalMs, enabled]);

  const setData = useCallback((updater: (previous: T | null) => T | null) => {
    setDataState((prev) => updater(prev));
  }, []);

  return { data, error, loading, refresh: run, setData };
}
