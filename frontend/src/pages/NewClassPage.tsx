import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { describeError } from '../api/errors';
import type { ClassCreated } from '../api/types';
import { Alert } from '../components/Alert';

export function NewClassPage() {
  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{ name?: string; totalSeats?: string }>({});
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<ClassCreated | null>(null);

  function validate(): boolean {
    const errors: { name?: string; totalSeats?: string } = {};
    if (!name.trim()) errors.name = 'Class-এর নাম দিন (যেমন "Class 6 - Morning Shift")।';
    const seats = Number(totalSeats);
    if (!totalSeats.trim() || !Number.isInteger(seats) || seats <= 0) {
      errors.totalSeats = 'Seat সংখ্যা ১ বা তার বেশি একটা পূর্ণসংখ্যা হতে হবে।';
    }
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setCreated(null);
    if (!validate()) return;
    setSubmitting(true);
    try {
      const result = await api.createClass(name.trim(), Number(totalSeats));
      setCreated(result);
      setName('');
      setTotalSeats('');
    } catch (err) {
      setError(describeError(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="narrow">
      <div className="page-head">
        <h1>নতুন Class তৈরি (school admin)</h1>
      </div>
      <p className="muted">
        প্রতিটা shift আলাদা একটা class। যেমন "Class 6 - Morning Shift" আর "Class 6 - Day Shift" দুটো আলাদা class, নিজের নিজের seat সংখ্যা নিয়ে।
      </p>

      {created && (
        <Alert kind="success" onDismiss={() => setCreated(null)}>
          <strong>{created.name}</strong> তৈরি হয়েছে ({created.totalSeats}টা seat, id: <code>{created.id}</code>)।{' '}
          <Link to="/">Class তালিকায় যান</Link>
        </Alert>
      )}
      {error && (
        <Alert kind="error" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}

      <form className="card form" onSubmit={onSubmit} noValidate>
        <label className="field">
          <span>Class-এর নাম</span>
          <input
            className={fieldErrors.name ? 'input input-invalid' : 'input'}
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Class 6 - Morning Shift"
            maxLength={200}
            aria-invalid={Boolean(fieldErrors.name)}
          />
          {fieldErrors.name && <span className="field-error">{fieldErrors.name}</span>}
        </label>

        <label className="field">
          <span>Seat সংখ্যা</span>
          <input
            className={fieldErrors.totalSeats ? 'input input-invalid' : 'input'}
            value={totalSeats}
            onChange={(e) => setTotalSeats(e.target.value)}
            placeholder="120"
            inputMode="numeric"
            aria-invalid={Boolean(fieldErrors.totalSeats)}
          />
          {fieldErrors.totalSeats && <span className="field-error">{fieldErrors.totalSeats}</span>}
        </label>

        <div className="form-actions">
          <button type="submit" className="btn btn-primary" disabled={submitting}>
            {submitting ? 'তৈরি হচ্ছে…' : 'Class তৈরি করুন'}
          </button>
        </div>
      </form>
    </section>
  );
}
