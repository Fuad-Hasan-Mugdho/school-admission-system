import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { StudentIdProvider } from '../hooks/useStudentId';

export const STUDENT = '20152691234567890';

/** Render a page inside the router + student-id context, as App does. */
export function renderPage(ui: ReactElement, { route = '/', path = '/', studentId = STUDENT }: { route?: string; path?: string; studentId?: string } = {}) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <StudentIdProvider initialValue={studentId}>
        <Routes>
          <Route path={path} element={ui} />
          <Route path="*" element={<div data-testid="other-route">other</div>} />
        </Routes>
      </StudentIdProvider>
    </MemoryRouter>,
  );
}

/** A fetch stub that answers by method+path, mimicking the backend's JSON conventions. */
export type Responder = (method: string, path: string, init?: RequestInit) => { status: number; body?: unknown } | Promise<{ status: number; body?: unknown }>;

export function mockFetch(responder: Responder) {
  const calls: { method: string; path: string; headers: Record<string, string>; body?: string }[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.toString() : input.url;
    const path = url.replace(/^https?:\/\/[^/]+/, '');
    const method = init?.method ?? 'GET';
    calls.push({ method, path, headers: (init?.headers as Record<string, string>) ?? {}, body: init?.body as string | undefined });
    const result = await responder(method, path, init);
    const text = result.body === undefined ? '' : JSON.stringify(result.body);
    return new Response(text, {
      status: result.status,
      headers: text ? { 'Content-Type': 'application/json' } : undefined,
    });
  });
  vi.stubGlobal('fetch', fetchMock);
  return { fetchMock, calls };
}
