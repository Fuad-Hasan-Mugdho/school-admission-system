import { API_BASE_URL } from '../config';
import type {
  ApiErrorBody,
  ClassCreated,
  ClassSeats,
  Hold,
  HoldConfirmed,
  HoldCreated,
  MyHold,
} from './types';

/** Every failure the UI can see is an ApiError: HTTP errors carry the server's code, network failures use NETWORK. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly details?: Record<string, string>,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE';
  body?: unknown;
  studentId?: string | null;
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  if (options.studentId) headers['X-Student-Id'] = options.studentId;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch {
    throw new ApiError(0, 'NETWORK', 'Could not reach the server.');
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  let payload: unknown = undefined;
  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = undefined;
    }
  }

  if (!response.ok) {
    const body = (payload ?? {}) as Partial<ApiErrorBody>;
    throw new ApiError(
      response.status,
      body.error ?? `HTTP_${response.status}`,
      body.message ?? `Request failed with status ${response.status}.`,
      body.details,
    );
  }

  return payload as T;
}

export const api = {
  getClasses: () => request<ClassSeats[]>('/classes'),
  getClass: (classId: string) => request<ClassSeats>(`/classes/${encodeURIComponent(classId)}`),
  createClass: (name: string, totalSeats: number) =>
    request<ClassCreated>('/classes', { method: 'POST', body: { name, totalSeats } }),
  createHold: (classId: string, studentId: string) =>
    request<HoldCreated>(`/classes/${encodeURIComponent(classId)}/holds`, { method: 'POST', studentId }),
  getHold: (holdId: string) => request<Hold>(`/holds/${encodeURIComponent(holdId)}`),
  confirmHold: (holdId: string, studentId: string) =>
    request<HoldConfirmed>(`/holds/${encodeURIComponent(holdId)}/confirm`, { method: 'POST', studentId }),
  cancelHold: (holdId: string, studentId: string) =>
    request<void>(`/holds/${encodeURIComponent(holdId)}`, { method: 'DELETE', studentId }),
  getMyHolds: (studentId: string) => request<MyHold[]>('/me/holds', { studentId }),
};

export type Api = typeof api;
