import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { describeError } from '../api/errors';
import type { MyHold } from '../api/types';
import { Alert } from '../components/Alert';
import { StatusBadge } from '../components/StatusBadge';
import { MY_HOLDS_POLL_MS } from '../config';
import { formatRemaining, useCountdown } from '../hooks/useCountdown';
import { usePolling } from '../hooks/usePolling';
import { useStudentId } from '../hooks/useStudentId';

function Row({ hold }: { hold: MyHold }) {
  const remaining = useCountdown(hold.status === 'HELD' ? hold.expiresAt : null, hold.serverTime, 1000);
  const status = hold.status === 'HELD' && remaining === 0 ? 'EXPIRED' : hold.status;
  return (
    <tr data-testid={`hold-row-${hold.holdId}`}>
      <td>
        <strong>{hold.className}</strong>
        <div className="muted small">{hold.holdId}</div>
      </td>
      <td>
        <StatusBadge status={status} />
      </td>
      <td>
        {status === 'HELD' && remaining !== null ? (
          <span className={remaining < 60_000 ? 'danger' : ''}>বাকি {formatRemaining(remaining)}</span>
        ) : (
          <span className="muted">—</span>
        )}
      </td>
      <td className="actions">
        <Link className="btn btn-secondary" to={`/holds/${hold.holdId}`}>
          {status === 'HELD' ? 'ফি দিন / বাতিল' : 'বিস্তারিত'}
        </Link>
      </td>
    </tr>
  );
}

export function MyHoldsPage() {
  const { studentId } = useStudentId();
  const holds = usePolling(() => api.getMyHolds(studentId!), MY_HOLDS_POLL_MS, Boolean(studentId));

  return (
    <section>
      <div className="page-head">
        <h1>আমার আবেদন</h1>
        {studentId && <span className="muted">শিক্ষার্থী: {studentId}</span>}
      </div>

      {!studentId && <Alert kind="info">নিজের আবেদন দেখতে উপরের ঘরে শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।</Alert>}
      {holds.error ? <Alert kind="error">আবেদন আনা যাচ্ছে না: {describeError(holds.error)}</Alert> : null}

      {studentId && holds.loading && !holds.data && <p className="muted">লোড হচ্ছে…</p>}

      {studentId && holds.data && holds.data.length === 0 && (
        <div className="empty">
          এই শিক্ষার্থীর কোনো আবেদন নেই। <Link to="/">Class তালিকা</Link> থেকে আবেদন করুন।
        </div>
      )}

      {studentId && holds.data && holds.data.length > 0 && (
        <div className="table-wrap">
          <table className="table" aria-label="my holds">
            <thead>
              <tr>
                <th>Class</th>
                <th>অবস্থা</th>
                <th>মেয়াদ</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {holds.data.map((h) => (
                <Row key={h.holdId} hold={h} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
