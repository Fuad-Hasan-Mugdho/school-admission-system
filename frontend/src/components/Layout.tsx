import { NavLink, Outlet } from 'react-router-dom';
import { API_BASE_URL } from '../config';
import { STUDENT_ID_LENGTH, useStudentId } from '../hooks/useStudentId';

export function Layout() {
  const { rawStudentId, studentId, setRawStudentId } = useStudentId();
  const incomplete = rawStudentId.length > 0 && !studentId;

  return (
    <>
      <header className="topbar">
        <div className="container topbar-inner">
          <div className="brand">
            <span className="brand-title">School Admission</span>
            <span className="brand-sub">অনলাইন ভর্তি — আগে আবেদন করলে আগে পাবে</span>
          </div>

          <label className="student-box">
            <span className="student-label">শিক্ষার্থীর জন্ম নিবন্ধন নম্বর (১৭ অঙ্ক)</span>
            <input
              type="text"
              inputMode="numeric"
              autoComplete="off"
              placeholder="যেমন 20152691234567890"
              value={rawStudentId}
              onChange={(e) => setRawStudentId(e.target.value)}
              aria-label="শিক্ষার্থীর জন্ম নিবন্ধন নম্বর"
              aria-invalid={incomplete}
              className={incomplete ? 'input input-invalid' : 'input'}
            />
            <span className={`student-hint ${studentId ? 'ok' : incomplete ? 'bad' : ''}`} aria-live="polite">
              {studentId
                ? 'পরিচয় ঠিক আছে'
                : incomplete
                  ? `${STUDENT_ID_LENGTH} অঙ্ক লাগবে (${rawStudentId.length}/${STUDENT_ID_LENGTH})`
                  : 'আবেদন করতে বা নিজের আবেদন দেখতে নম্বরটি দিন'}
            </span>
          </label>
        </div>
        <nav className="container nav">
          <NavLink to="/" end>
            Class তালিকা
          </NavLink>
          <NavLink to="/me">আমার আবেদন</NavLink>
          <NavLink to="/admin/classes/new">নতুন Class (admin)</NavLink>
        </nav>
      </header>
      <main className="container">
        <Outlet />
      </main>
      <footer className="container footer">API: {API_BASE_URL}</footer>
    </>
  );
}
