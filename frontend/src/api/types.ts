export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED';

export interface ClassCreated {
  id: string;
  name: string;
  totalSeats: number;
}

export interface ClassSeats extends ClassCreated {
  heldSeats: number;
  confirmedSeats: number;
  availableSeats: number;
}

export interface HoldCreated {
  holdId: string;
  classId: string;
  studentId: string;
  status: HoldStatus;
  expiresAt: string;
  serverTime?: string;
}

export interface Hold {
  holdId: string;
  classId: string;
  status: HoldStatus;
  expiresAt: string;
  className?: string;
  studentId?: string;
  serverTime?: string;
}

export interface HoldConfirmed {
  holdId: string;
  status: HoldStatus;
}

export interface MyHold {
  holdId: string;
  classId: string;
  className: string;
  status: HoldStatus;
  expiresAt: string;
  createdAt?: string;
  serverTime?: string;
}

export interface ApiErrorBody {
  error: string;
  message: string;
  details?: Record<string, string>;
}
