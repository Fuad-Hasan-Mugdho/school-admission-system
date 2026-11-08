import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ClassesPage } from '../pages/ClassesPage';
import { STUDENT, mockFetch, renderPage } from './utils';

const CLASSES = [
  { id: 'c_morning', name: 'Class 6 - Morning Shift', totalSeats: 120, heldSeats: 15, confirmedSeats: 80, availableSeats: 25 },
  { id: 'c_full', name: 'Class 6 - Day Shift', totalSeats: 60, heldSeats: 10, confirmedSeats: 50, availableSeats: 0 },
];

describe('ClassesPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('lists every class with total / held / confirmed / available seats', async () => {
    mockFetch((_m, path) => {
      if (path === '/classes') return { status: 200, body: CLASSES };
      if (path === '/me/holds') return { status: 200, body: [] };
      return { status: 404 };
    });

    renderPage(<ClassesPage />);

    const row = await screen.findByTestId('class-row-c_morning');
    const cells = within(row).getAllByRole('cell').map((c) => c.textContent);
    expect(cells[0]).toContain('Class 6 - Morning Shift');
    expect(cells.slice(1, 5)).toEqual(['120', '15', '80', '25']);
    expect(within(row).getByRole('button', { name: 'আবেদন করুন' })).toBeEnabled();

    const fullRow = screen.getByTestId('class-row-c_full');
    expect(within(fullRow).getByRole('button', { name: 'Seat নেই' })).toBeDisabled();
  });

  it('disables applying until a valid 17-digit student id is entered', async () => {
    mockFetch((_m, path) => (path === '/classes' ? { status: 200, body: CLASSES } : { status: 404 }));

    renderPage(<ClassesPage />, { studentId: '' });

    const row = await screen.findByTestId('class-row-c_morning');
    expect(within(row).getByRole('button', { name: 'আবেদন করুন' })).toBeDisabled();
    expect(screen.getByText(/১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন/)).toBeInTheDocument();
  });

  it('applies with the X-Student-Id header and navigates to the new application', async () => {
    const user = userEvent.setup();
    const { calls } = mockFetch((method, path) => {
      if (path === '/classes') return { status: 200, body: CLASSES };
      if (path === '/me/holds') return { status: 200, body: [] };
      if (method === 'POST' && path === '/classes/c_morning/holds') {
        return { status: 201, body: { holdId: 'h_new', classId: 'c_morning', studentId: STUDENT, status: 'HELD', expiresAt: new Date().toISOString() } };
      }
      return { status: 404 };
    });

    renderPage(<ClassesPage />);
    const row = await screen.findByTestId('class-row-c_morning');
    await user.click(within(row).getByRole('button', { name: 'আবেদন করুন' }));

    await waitFor(() => expect(screen.getByTestId('other-route')).toBeInTheDocument());
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.path).toBe('/classes/c_morning/holds');
    expect(post?.headers['X-Student-Id']).toBe(STUDENT);
  });

  it('shows the seat-full message when the server answers 409 (someone was faster)', async () => {
    const user = userEvent.setup();
    mockFetch((method, path) => {
      if (path === '/classes') return { status: 200, body: CLASSES };
      if (path === '/me/holds') return { status: 200, body: [] };
      if (method === 'POST') return { status: 409, body: { error: 'SEAT_UNAVAILABLE', message: 'full' } };
      return { status: 404 };
    });

    renderPage(<ClassesPage />);
    const row = await screen.findByTestId('class-row-c_morning');
    await user.click(within(row).getByRole('button', { name: 'আবেদন করুন' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/seat খালি নেই/);
  });

  it('links to the existing application instead of offering to apply twice', async () => {
    mockFetch((_m, path) => {
      if (path === '/classes') return { status: 200, body: CLASSES };
      if (path === '/me/holds') {
        return { status: 200, body: [{ holdId: 'h_mine', classId: 'c_morning', className: 'Class 6 - Morning Shift', status: 'HELD', expiresAt: new Date(Date.now() + 60000).toISOString() }] };
      }
      return { status: 404 };
    });

    renderPage(<ClassesPage />);
    const row = await screen.findByTestId('class-row-c_morning');
    await waitFor(() => expect(within(row).getByRole('link', { name: 'আপনার আবেদন দেখুন' })).toHaveAttribute('href', '/holds/h_mine'));
  });
});
