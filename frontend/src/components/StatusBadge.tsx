import type { HoldStatus } from '../api/types';

export const STATUS_LABELS: Record<HoldStatus, string> = {
  HELD: 'Hold-এ আছে (ফি বাকি)',
  CONFIRMED: 'ভর্তি নিশ্চিত',
  EXPIRED: 'মেয়াদ শেষ',
  CANCELLED: 'বাতিল',
};

export function StatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge badge-${status.toLowerCase()}`} data-testid="status-badge" data-status={status}>
      {STATUS_LABELS[status] ?? status}
    </span>
  );
}
