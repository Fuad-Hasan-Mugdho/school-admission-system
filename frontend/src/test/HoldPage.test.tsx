import { screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { HoldPage } from '../pages/HoldPage';
import { STUDENT, mockFetch, renderPage } from './utils';

const HOLD_ID = 'h_test123';

function holdBody(status: string, expiresInMs: number, now = Date.now()) {
  return {
    holdId: HOLD_ID,
    classId: 'c_1',
    className: 'Class 6 - Morning Shift',
    studentId: STUDENT,
    status,
    expiresAt: new Date(now + expiresInMs).toISOString(),
    serverTime: new Date(now).toISOString(),
  };
}

describe('HoldPage', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('shows the countdown and, when the hold expires, flips to EXPIRED and disables payment without a reload', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const start = Date.now();
    let serverStatus = 'HELD';
    mockFetch((method, path) => {
      if (method === 'GET' && path === `/holds/${HOLD_ID}`) {
        // server keeps answering HELD until its own clock passes expiry
        if (Date.now() - start >= 3000) serverStatus = 'EXPIRED';
        return { status: 200, body: holdBody(serverStatus, 3000, start) };
      }
      return { status: 404, body: { error: 'HOLD_NOT_FOUND', message: 'nope' } };
    });

    renderPage(<HoldPage />, { route: `/holds/${HOLD_ID}`, path: '/holds/:holdId' });

    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveAttribute('data-status', 'HELD'));
    expect(screen.getByTestId('pay-button')).toBeEnabled();
    expect(screen.getByTestId('countdown')).toHaveTextContent(/00:0[123]/);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(3500);
    });

    expect(screen.getByTestId('status-badge')).toHaveAttribute('data-status', 'EXPIRED');
    expect(screen.getByTestId('pay-button')).toBeDisabled();
    expect(screen.getByTestId('cancel-button')).toBeDisabled();
    expect(screen.getByText(/মেয়াদ শেষ হয়ে গেছে/)).toBeInTheDocument();
  });

  it('confirms the admission when "ভর্তি ফি দিন" is clicked', async () => {
    const user = userEvent.setup();
    let status = 'HELD';
    const { calls } = mockFetch((method, path) => {
      if (method === 'GET' && path === `/holds/${HOLD_ID}`) return { status: 200, body: holdBody(status, 600_000) };
      if (method === 'POST' && path === `/holds/${HOLD_ID}/confirm`) {
        status = 'CONFIRMED';
        return { status: 200, body: { holdId: HOLD_ID, status: 'CONFIRMED' } };
      }
      return { status: 404, body: { error: 'HOLD_NOT_FOUND', message: 'nope' } };
    });

    renderPage(<HoldPage />, { route: `/holds/${HOLD_ID}`, path: '/holds/:holdId' });
    await waitFor(() => expect(screen.getByTestId('pay-button')).toBeEnabled());

    await user.click(screen.getByTestId('pay-button'));

    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveAttribute('data-status', 'CONFIRMED'));
    expect(screen.getByText(/ভর্তি নিশ্চিত!/)).toBeInTheDocument();
    expect(screen.getByTestId('pay-button')).toBeDisabled();
    const confirmCall = calls.find((c) => c.method === 'POST');
    expect(confirmCall?.headers['X-Student-Id']).toBe(STUDENT);
  });

  it('shows a readable message when the server rejects the payment', async () => {
    const user = userEvent.setup();
    mockFetch((method, path) => {
      if (method === 'GET' && path === `/holds/${HOLD_ID}`) return { status: 200, body: holdBody('HELD', 600_000) };
      if (method === 'POST') return { status: 409, body: { error: 'HOLD_EXPIRED', message: 'expired' } };
      return { status: 500 };
    });

    renderPage(<HoldPage />, { route: `/holds/${HOLD_ID}`, path: '/holds/:holdId' });
    await waitFor(() => expect(screen.getByTestId('pay-button')).toBeEnabled());
    await user.click(screen.getByTestId('pay-button'));

    expect(await screen.findByRole('alert')).toHaveTextContent(/মেয়াদ শেষ হয়ে গেছে/);
  });

  it('asks before cancelling and then cancels', async () => {
    const user = userEvent.setup();
    let status = 'HELD';
    mockFetch((method, path) => {
      if (method === 'GET' && path === `/holds/${HOLD_ID}`) return { status: 200, body: holdBody(status, 600_000) };
      if (method === 'DELETE' && path === `/holds/${HOLD_ID}`) {
        status = 'CANCELLED';
        return { status: 204 };
      }
      return { status: 500 };
    });

    renderPage(<HoldPage />, { route: `/holds/${HOLD_ID}`, path: '/holds/:holdId' });
    await waitFor(() => expect(screen.getByTestId('cancel-button')).toBeEnabled());

    await user.click(screen.getByTestId('cancel-button'));
    expect(screen.getByText(/সত্যিই বাতিল করবেন\?/)).toBeInTheDocument();
    await user.click(screen.getByTestId('cancel-confirm'));

    await waitFor(() => expect(screen.getByTestId('status-badge')).toHaveAttribute('data-status', 'CANCELLED'));
  });

  it('keeps the buttons disabled when the application belongs to another student', async () => {
    mockFetch((method, path) => {
      if (method === 'GET' && path === `/holds/${HOLD_ID}`) {
        return { status: 200, body: { ...holdBody('HELD', 600_000), studentId: '11111111111111111' } };
      }
      return { status: 500 };
    });

    renderPage(<HoldPage />, { route: `/holds/${HOLD_ID}`, path: '/holds/:holdId' });
    await waitFor(() => expect(screen.getByTestId('status-badge')).toBeInTheDocument());
    expect(screen.getByTestId('pay-button')).toBeDisabled();
    expect(screen.getByText(/অন্য একজন শিক্ষার্থীর/)).toBeInTheDocument();
  });

  it('shows a friendly message instead of a blank page when the server is unreachable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('Failed to fetch'); }));
    renderPage(<HoldPage />, { route: `/holds/${HOLD_ID}`, path: '/holds/:holdId' });
    expect(await screen.findByRole('alert')).toHaveTextContent(/Server-এর সাথে সংযোগ করা যাচ্ছে না/);
  });

  beforeEach(() => {
    vi.restoreAllMocks();
  });
});
