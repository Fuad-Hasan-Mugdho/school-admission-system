import { describe, expect, it } from 'vitest';
import { ApiError } from '../api/client';
import { describeError } from '../api/errors';

describe('describeError', () => {
  it('explains a network failure in plain language', () => {
    expect(describeError(new ApiError(0, 'NETWORK', 'x'))).toMatch(/Server-এর সাথে সংযোগ/);
  });

  it('maps known backend codes to user-facing sentences', () => {
    expect(describeError(new ApiError(409, 'SEAT_UNAVAILABLE', 'x'))).toMatch(/seat খালি নেই/);
    expect(describeError(new ApiError(409, 'HOLD_EXPIRED', 'x'))).toMatch(/মেয়াদ শেষ/);
    expect(describeError(new ApiError(409, 'ALREADY_APPLIED', 'x'))).toMatch(/ইতিমধ্যে/);
    expect(describeError(new ApiError(403, 'NOT_OWNER', 'x'))).toMatch(/অন্য একজন শিক্ষার্থীর/);
  });

  it('includes field details for validation errors', () => {
    const err = new ApiError(400, 'VALIDATION_ERROR', 'Invalid class data.', { totalSeats: 'totalSeats must be positive.' });
    expect(describeError(err)).toContain('totalSeats must be positive.');
  });

  it('never returns an empty string', () => {
    expect(describeError(undefined)).not.toBe('');
    expect(describeError(new Error(''))).not.toBe('');
    expect(describeError(new ApiError(500, 'HTTP_500', ''))).toMatch(/Server-এ একটা সমস্যা/);
  });
});
