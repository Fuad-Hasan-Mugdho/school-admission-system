import type { ReactNode } from 'react';

export function Alert({ kind, children, onDismiss }: { kind: 'error' | 'success' | 'info' | 'warning'; children: ReactNode; onDismiss?: () => void }) {
  return (
    <div className={`alert alert-${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      <div className="alert-body">{children}</div>
      {onDismiss && (
        <button type="button" className="alert-close" onClick={onDismiss} aria-label="বন্ধ করুন">
          ×
        </button>
      )}
    </div>
  );
}
