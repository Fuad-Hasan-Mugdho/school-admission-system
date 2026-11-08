import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { describeError } from '../api/errors';
import type { Hold, HoldStatus } from '../api/types';
import { Alert } from '../components/Alert';
import { StatusBadge } from '../components/StatusBadge';
import { HOLD_POLL_MS } from '../config';
import { formatRemaining, useCountdown } from '../hooks/useCountdown';
import { usePolling } from '../hooks/usePolling';
import { useStudentId } from '../hooks/useStudentId';

export function HoldPage() {
  const { holdId = '' } = useParams();
  const { studentId } = useStudentId();
  const hold = usePolling(() => api.getHold(holdId), HOLD_POLL_MS, Boolean(holdId));
  const remainingMs = useCountdown(hold.data?.expiresAt, hold.data?.serverTime);

  const [busy, setBusy] = useState<'confirm' | 'cancel' | null>(null);
  const [message, setMessage] = useState<{ kind: 'success' | 'error'; text: string } | null>(null);
  const [confirmingCancel, setConfirmingCancel] = useState(false);

  // The server is the source of truth, but the moment the countdown hits zero we show EXPIRED
  // locally too, so the user is never invited to pay for a seat that is already gone.
  const serverStatus: HoldStatus | undefined = hold.data?.status;
  const effectiveStatus: HoldStatus | undefined =
    serverStatus === 'HELD' && remainingMs === 0 ? 'EXPIRED' : serverStatus;

  useEffect(() => {
    if (serverStatus === 'HELD' && remainingMs === 0) void hold.refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remainingMs, serverStatus]);

  const isOwner = Boolean(studentId) && (!hold.data?.studentId || hold.data.studentId === studentId);
  const canPay = effectiveStatus === 'HELD' && isOwner && busy === null;
  const canCancel = effectiveStatus === 'HELD' && isOwner && busy === null;

  async function pay() {
    if (!studentId) return;
    setBusy('confirm');
    setMessage(null);
    try {
      const result = await api.confirmHold(holdId, studentId);
      hold.setData((prev) => (prev ? { ...prev, status: result.status } : prev));
      setMessage({ kind: 'success', text: 'ভর্তি ফি জমা হয়েছে। ভর্তি নিশ্চিত!' });
    } catch (e) {
      setMessage({ kind: 'error', text: describeError(e) });
    } finally {
      setBusy(null);
      void hold.refresh();
    }
  }

  async function cancel() {
    if (!studentId) return;
    setBusy('cancel');
    setMessage(null);
    setConfirmingCancel(false);
    try {
      await api.cancelHold(holdId, studentId);
      hold.setData((prev: Hold | null) => (prev ? { ...prev, status: 'CANCELLED' } : prev));
      setMessage({ kind: 'success', text: 'আবেদন বাতিল হয়েছে, seat-টি অন্যদের জন্য খুলে গেছে।' });
    } catch (e) {
      setMessage({ kind: 'error', text: describeError(e) });
    } finally {
      setBusy(null);
      void hold.refresh();
    }
  }

  if (hold.error && !hold.data) {
    return (
      <section className="narrow">
        <Alert kind="error">{describeError(hold.error)}</Alert>
        <Link to="/me">← আমার আবেদন</Link>
      </section>
    );
  }

  if (!hold.data) {
    return (
      <section className="narrow">
        <p className="muted">আবেদন লোড হচ্ছে…</p>
      </section>
    );
  }

  const data = hold.data;

  return (
    <section className="narrow">
      <div className="page-head">
        <h1>আবেদন</h1>
        <Link to="/me" className="muted">
          ← আমার আবেদন
        </Link>
      </div>

      {hold.error ? <Alert kind="warning">সর্বশেষ তথ্য আনা যাচ্ছে না: {describeError(hold.error)}</Alert> : null}
      {message && (
        <Alert kind={message.kind} onDismiss={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}
      {!studentId && <Alert kind="info">ফি দিতে বা বাতিল করতে উপরের ঘরে শিক্ষার্থীর জন্ম নিবন্ধন নম্বর দিন।</Alert>}
      {studentId && data.studentId && data.studentId !== studentId && (
        <Alert kind="warning">এই আবেদনটি অন্য একজন শিক্ষার্থীর ({data.studentId})। নিজের নম্বর দিলে নিজের আবেদন দেখতে পাবেন।</Alert>
      )}

      <div className="card">
        <dl className="details">
          <dt>Class</dt>
          <dd>
            <strong>{data.className || data.classId}</strong>
            <div className="muted small">{data.classId}</div>
          </dd>
          <dt>আবেদন নম্বর</dt>
          <dd>
            <code>{data.holdId}</code>
          </dd>
          <dt>অবস্থা</dt>
          <dd>{effectiveStatus && <StatusBadge status={effectiveStatus} />}</dd>
          <dt>মেয়াদ</dt>
          <dd>
            {effectiveStatus === 'HELD' && remainingMs !== null ? (
              <span className={remainingMs < 60_000 ? 'countdown danger' : 'countdown'} data-testid="countdown">
                শেষ হতে বাকি {formatRemaining(remainingMs)}
              </span>
            ) : effectiveStatus === 'EXPIRED' ? (
              <span className="danger">মেয়াদ শেষ হয়ে গেছে, seat ছেড়ে দেওয়া হয়েছে</span>
            ) : effectiveStatus === 'CONFIRMED' ? (
              <span className="ok">ফি জমা হয়েছে, seat আপনার</span>
            ) : (
              <span className="muted">—</span>
            )}
            <div className="muted small">{new Date(data.expiresAt).toLocaleString()}</div>
          </dd>
        </dl>

        <div className="form-actions">
          <button type="button" className="btn btn-primary" disabled={!canPay} onClick={pay} data-testid="pay-button">
            {busy === 'confirm' ? 'জমা হচ্ছে…' : 'ভর্তি ফি দিন'}
          </button>
          {!confirmingCancel ? (
            <button
              type="button"
              className="btn btn-danger"
              disabled={!canCancel}
              onClick={() => setConfirmingCancel(true)}
              data-testid="cancel-button"
            >
              বাতিল
            </button>
          ) : (
            <span className="confirm-inline">
              সত্যিই বাতিল করবেন?
              <button type="button" className="btn btn-danger" onClick={cancel} data-testid="cancel-confirm">
                হ্যাঁ, বাতিল করুন
              </button>
              <button type="button" className="btn" onClick={() => setConfirmingCancel(false)}>
                না
              </button>
            </span>
          )}
        </div>

        {effectiveStatus === 'EXPIRED' && (
          <p className="muted">
            চাইলে <Link to="/">Class তালিকা</Link> থেকে আবার আবেদন করতে পারেন (seat খালি থাকলে)।
          </p>
        )}
      </div>
    </section>
  );
}
