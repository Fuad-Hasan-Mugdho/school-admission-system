import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { STUDENT_ID_STORAGE_KEY } from '../config';

export const STUDENT_ID_LENGTH = 17;

export function isValidStudentId(value: string): boolean {
  return new RegExp(`^\\d{${STUDENT_ID_LENGTH}}$`).test(value);
}

interface StudentIdContextValue {
  /** Raw text as typed (digits only, max 17). */
  rawStudentId: string;
  /** The id when it is a complete 17-digit number, otherwise null. */
  studentId: string | null;
  setRawStudentId: (value: string) => void;
}

const StudentIdContext = createContext<StudentIdContextValue | null>(null);

function readStored(): string {
  try {
    return window.localStorage.getItem(STUDENT_ID_STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}

export function StudentIdProvider({ children, initialValue }: { children: ReactNode; initialValue?: string }) {
  const [rawStudentId, setRaw] = useState<string>(() => initialValue ?? readStored());

  useEffect(() => {
    try {
      if (rawStudentId) window.localStorage.setItem(STUDENT_ID_STORAGE_KEY, rawStudentId);
      else window.localStorage.removeItem(STUDENT_ID_STORAGE_KEY);
    } catch {
      /* storage unavailable (private mode) - the id still works for this tab */
    }
  }, [rawStudentId]);

  const setRawStudentId = useCallback((value: string) => {
    setRaw(value.replace(/\D/g, '').slice(0, STUDENT_ID_LENGTH));
  }, []);

  const value = useMemo<StudentIdContextValue>(
    () => ({
      rawStudentId,
      studentId: isValidStudentId(rawStudentId) ? rawStudentId : null,
      setRawStudentId,
    }),
    [rawStudentId, setRawStudentId],
  );

  return <StudentIdContext.Provider value={value}>{children}</StudentIdContext.Provider>;
}

export function useStudentId(): StudentIdContextValue {
  const ctx = useContext(StudentIdContext);
  if (!ctx) throw new Error('useStudentId must be used inside StudentIdProvider');
  return ctx;
}
