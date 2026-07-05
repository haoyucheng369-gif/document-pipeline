import { Navigate, Route, Routes } from "react-router-dom";
import { Layout } from "./components/Layout";
import { CreateJobPage } from "./pages/CreateJobPage";
import { JobsPage } from "./pages/JobsPage";
import { JobDetailPage } from "./pages/JobDetailPage";

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<CreateJobPage />} />
        <Route path="/jobs" element={<JobsPage />} />
        <Route path="/jobs/:id" element={<JobDetailPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Layout>
  );
}
