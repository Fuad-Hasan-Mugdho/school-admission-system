import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { describeError } from '../api/errors';
import type { MyHold } from '../api/types';
import { Alert } from '../components/Alert';
import { CLASS_LIST_POLL_MS, MY_HOLDS_POLL_MS } from '../config';
import { usePolling } from '../hooks/usePolling';
import { useStudentId } from '../hooks/useStudentId';

export function ClassesPage() {
  const navigate = useNavigate();
  const { studentId } = useStudentId();
  const classes = usePolling(() => api.getClasses(), CLASS_LIST_POLL_MS);
  const myHolds = usePolling(() => api.getMyHolds(studentId!), MY_HOLDS_POLL_MS, Boolean(studentId));
  const [applyingId, setApplyingId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  /** classId -> my active (HELD/CONFIRMED) hold, so the row can link to it instead of re-applying. */
  const activeByClass = useMemo(() => {
    const map = new Map<string, MyHold>();
    for (const h of myHolds.data ?? []) {
      if (h.status === 'HELD' || h.status === 'CONFIRMED') map.set(h.classId, h);
    }
    return map;
  }, [myHolds.data]);

  async function apply(classId: string) {
    if (!studentId) return;
    setActionError(null);
    setApplyingId(classId);
    try {
      const hold = await api.createHold(classId, studentId);
      navigate(`/holds/${hold.holdId}`);
    } catch (e) {
      setActionError(describeError(e));
      void classes.refresh();
      void myHolds.refresh();
    } finally {
      setApplyingId(null);
    }
  }

  return (
    <section>
      <div className="page-head">
        <h1>Class তালিকা</h1>
        <span className="muted">seat সংখ্যা প্রতি {CLASS_LIST_POLL_MS / 1000} সেকেন্ডে নিজে থেকে হালনাগাদ হয়</span>
      </div>

      {classes.error ? <Alert kind="error">তালিকা আনা যাচ্ছে না: {describeError(classes.error)}</Alert> : null}
      {actionError && (
        <Alert kind="error" onDismiss={() => setActionError(null)}>
          {actionError}
        </Alert>
      )}
      {!studentId && (
        <Alert kind="info">আবেদন করতে হলে উপরের ঘরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</Alert>
      )}

      {classes.loading && !classes.data && <p className="muted">লোড হচ্ছে…</p>}

      {classes.data && classes.data.length === 0 && (
        <div className="empty">
          এখনো কোনো class তৈরি হয়নি। <Link to="/admin/classes/new">নতুন class তৈরি করুন</Link>।
        </div>
      )}

      {classes.data && classes.data.length > 0 && (
        <div className="table-wrap">
          <table className="table" aria-label="class list">
            <thead>
              <tr>
                <th>Class</th>
                <th className="num">মোট</th>
                <th className="num">Hold-এ</th>
                <th className="num">নিশ্চিত</th>
                <th className="num">খালি</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {classes.data.map((c) => {
                const mine = activeByClass.get(c.id);
                const full = c.availableSeats <= 0;
                return (
                  <tr key={c.id} data-testid={`class-row-${c.id}`}>
                    <td>
                      <strong>{c.name}</strong>
                      <div className="muted small">{c.id}</div>
                    </td>
                    <td className="num">{c.totalSeats}</td>
                    <td className="num">{c.heldSeats}</td>
                    <td className="num">{c.confirmedSeats}</td>
                    <td className={`num ${full ? 'danger' : 'ok'}`}>{c.availableSeats}</td>
                    <td className="actions">
                      {mine ? (
                        <Link className="btn btn-secondary" to={`/holds/${mine.holdId}`}>
                          {mine.status === 'CONFIRMED' ? 'ভর্তি নিশ্চিত — দেখুন' : 'আপনার আবেদন দেখুন'}
                        </Link>
                      ) : (
                        <button
                          type="button"
                          className="btn btn-primary"
                          disabled={!studentId || full || applyingId === c.id}
                          title={!studentId ? 'আগে জন্ম নিবন্ধন নম্বর দিন' : full ? 'কোনো seat খালি নেই' : undefined}
                          onClick={() => apply(c.id)}
                        >
                          {applyingId === c.id ? 'আবেদন হচ্ছে…' : full ? 'Seat নেই' : 'আবেদন করুন'}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
