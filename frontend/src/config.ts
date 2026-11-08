/**
 * Backend base URL resolution order:
 *  1. window.__APP_CONFIG__.apiBaseUrl - written at container start from the API_BASE_URL env var
 *  2. VITE_API_BASE_URL              - build-time env (npm run dev / build)
 *  3. http://localhost:8080          - the port the assignment fixes for the backend
 */
export function resolveApiBaseUrl(): string {
  const runtime = typeof window !== 'undefined' ? window.__APP_CONFIG__?.apiBaseUrl : undefined;
  const buildTime = import.meta.env.VITE_API_BASE_URL;
  const value = (runtime && runtime.trim()) || (buildTime && buildTime.trim()) || 'http://localhost:8080';
  return value.replace(/\/+$/, '');
}

export const API_BASE_URL = resolveApiBaseUrl();

/** Polling cadence (ms). Kept short so seat counts feel live without hammering the API. */
export const CLASS_LIST_POLL_MS = 3000;
export const HOLD_POLL_MS = 2000;
export const MY_HOLDS_POLL_MS = 3000;

export const STUDENT_ID_STORAGE_KEY = 'schoolAdmission.studentId';
