import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NewClassPage } from '../pages/NewClassPage';
import { mockFetch, renderPage } from './utils';

describe('NewClassPage', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('validates before sending anything', async () => {
    const user = userEvent.setup();
    const { fetchMock } = mockFetch(() => ({ status: 500 }));

    renderPage(<NewClassPage />);
    await user.click(screen.getByRole('button', { name: 'Class তৈরি করুন' }));

    expect(screen.getByText(/Class-এর নাম দিন/)).toBeInTheDocument();
    expect(screen.getByText(/Seat সংখ্যা ১ বা তার বেশি/)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('posts name and totalSeats and shows the created class', async () => {
    const user = userEvent.setup();
    const { calls } = mockFetch((method, path) => {
      if (method === 'POST' && path === '/classes') return { status: 201, body: { id: 'c_123', name: 'Class 6 - Morning Shift', totalSeats: 120 } };
      return { status: 404 };
    });

    renderPage(<NewClassPage />);
    await user.type(screen.getByLabelText('Class-এর নাম'), 'Class 6 - Morning Shift');
    await user.type(screen.getByLabelText('Seat সংখ্যা'), '120');
    await user.click(screen.getByRole('button', { name: 'Class তৈরি করুন' }));

    expect(await screen.findByRole('status')).toHaveTextContent(/তৈরি হয়েছে/);
    expect(JSON.parse(calls[0].body!)).toEqual({ name: 'Class 6 - Morning Shift', totalSeats: 120 });
  });

  it('surfaces server-side validation messages', async () => {
    const user = userEvent.setup();
    mockFetch(() => ({ status: 400, body: { error: 'VALIDATION_ERROR', message: 'Invalid class data.', details: { totalSeats: 'totalSeats is unrealistically large (max 100000).' } } }));

    renderPage(<NewClassPage />);
    await user.type(screen.getByLabelText('Class-এর নাম'), 'Class 9');
    await user.type(screen.getByLabelText('Seat সংখ্যা'), '999999');
    await user.click(screen.getByRole('button', { name: 'Class তৈরি করুন' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/unrealistically large/);
  });
});
