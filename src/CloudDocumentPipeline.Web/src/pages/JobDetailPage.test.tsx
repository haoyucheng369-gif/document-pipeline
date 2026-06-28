import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Routes, Route } from "react-router-dom";
import { JobDetailPage } from "./JobDetailPage";
import { renderWithProviders } from "../test/render";
import * as api from "../lib/api";

vi.mock("../lib/signalr", () => ({
  subscribeToJobUpdates: vi.fn().mockResolvedValue(() => {})
}));

vi.mock("../lib/api", async () => {
  const actual = await vi.importActual<typeof import("../lib/api")>("../lib/api");
  return {
    ...actual,
    getJob: vi.fn(),
    retryJob: vi.fn(),
    downloadResultFile: vi.fn()
  };
});

describe("JobDetailPage", () => {
  it("retries a failed job from the detail page", async () => {
    const getJob = vi.mocked(api.getJob);
    const retryJob = vi.mocked(api.retryJob);

    getJob.mockResolvedValue({
      id: "job-1",
      name: "demo",
      type: "DocumentToPdf",
      status: "Failed",
      retryCount: 1,
      correlationId: "corr-1",
      createdAtUtc: new Date().toISOString(),
      errorMessage: "failed"
    });
    retryJob.mockResolvedValue();

    const user = userEvent.setup();
    renderWithProviders(
      <Routes>
        <Route path="/jobs/:id" element={<JobDetailPage />} />
      </Routes>,
      { route: "/jobs/job-1" }
    );

    await screen.findByText("Job Detail");
    await user.click(screen.getByRole("button", { name: /retry failed job/i }));

    await waitFor(() => {
      expect(retryJob).toHaveBeenCalledWith("job-1");
    });
  });
});
