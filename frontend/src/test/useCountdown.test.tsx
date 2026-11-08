import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { formatRemaining, useCountdown } from '../hooks/useCountdown';

describe('useCountdown', () => {
  afterEach(() => vi.useRealTimers());

  it('counts down to zero and stops there', () => {
    vi.useFakeTimers();
    const now = Date.now();
    const expiresAt = new Date(now + 2000).toISOString();
    const { result } = renderHook(() => useCountdown(expiresAt, new Date(now).toISOString(), 500));
    expect(result.current).toBeGreaterThan(1500);

    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(result.current).toBeLessThanOrEqual(1000);

    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(result.current).toBe(0);
  });

  it('starts as null until an expiry is known, then updates when it arrives', () => {
    vi.useFakeTimers();
    const now = Date.now();
    const { result, rerender } = renderHook(({ expiresAt }: { expiresAt?: string }) => useCountdown(expiresAt), {
      initialProps: { expiresAt: undefined as string | undefined },
    });
    expect(result.current).toBeNull();
    rerender({ expiresAt: new Date(now + 60_000).toISOString() });
    expect(result.current).not.toBeNull();
    expect(result.current).toBeGreaterThan(59_000);
  });

  it('uses the server clock when the browser clock is wrong', () => {
    vi.useFakeTimers();
    const browserNow = Date.now();
    const serverNow = browserNow - 120_000; // browser is 2 minutes ahead of the server
    const expiresAt = new Date(serverNow + 30_000).toISOString();
    const { result } = renderHook(() => useCountdown(expiresAt, new Date(serverNow).toISOString()));
    // Measured by the browser clock this would already be expired; by the server clock 30 s remain.
    expect(result.current).toBeGreaterThan(29_000);
  });

  it('formats mm:ss', () => {
    expect(formatRemaining(0)).toBe('00:00');
    expect(formatRemaining(61_000)).toBe('01:01');
    expect(formatRemaining(599_400)).toBe('10:00');
  });
});
