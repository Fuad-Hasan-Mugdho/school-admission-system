import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { ClassesPage } from './pages/ClassesPage';
import { HoldPage } from './pages/HoldPage';
import { MyHoldsPage } from './pages/MyHoldsPage';
import { NewClassPage } from './pages/NewClassPage';

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<ClassesPage />} />
        <Route path="classes" element={<Navigate to="/" replace />} />
        <Route path="admin/classes/new" element={<NewClassPage />} />
        <Route path="holds/:holdId" element={<HoldPage />} />
        <Route path="me" element={<MyHoldsPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
