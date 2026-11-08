import { useEffect, useMemo, useReducer } from 'react';

/**
 * Milliseconds until `expiresAt`, re-evaluated every `tickMs`, or null when no expiry is known.
 * The value is derived during render (a timer only forces re-renders), so the very first render
 * that knows the expiry already shows the right number.
 *
 * If the server told us its clock (`serverTime`) we measure against that instead of the browser's,
 * so a wrong PC clock cannot show a hold as alive after the server expired it.
 */
export function useCountdown(expiresAt: string | null | undefined, serverTime?: string | null, tickMs = 500): number | null {
  const [, forceTick] = useReducer((n: number) => n + 1, 0);

  const offsetMs = useMemo(() => {
    if (!serverTime) return 0;
    const server = Date.parse(serverTime);
    return Number.isNaN(server) ? 0 : server - Date.now();
  }, [serverTime]);

  const target = expiresAt ? Date.parse(expiresAt) : Number.NaN;
  const remaining = Number.isNaN(target) ? null : Math.max(0, target - (Date.now() + offsetMs));

  useEffect(() => {
    if (Number.isNaN(target) || remaining === 0) return;
    const id = window.setInterval(forceTick, tickMs);
    return () => window.clearInterval(id);
  }, [target, tickMs, remaining === 0]);

  return remaining;
}

export function formatRemaining(ms: number): string {
  const totalSeconds = Math.ceil(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  if (minutes >= 60) {
    const hours = Math.floor(minutes / 60);
    return `${hours} ঘণ্টা ${String(minutes % 60).padStart(2, '0')} মিনিট`;
  }
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}
