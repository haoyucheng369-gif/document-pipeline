import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CreateJobPage } from "./CreateJobPage";
import { renderWithProviders } from "../test/render";
import * as api from "../lib/api";

vi.mock("../lib/api", async () => {
  const actual = await vi.importActual<typeof import("../lib/api")>("../lib/api");
  return {
    ...actual,
    createDocumentToPdf: vi.fn()
  };
});

describe("CreateJobPage", () => {
  it("submits the selected file and creates a conversion job", async () => {
    const createDocumentToPdf = vi.mocked(api.createDocumentToPdf);
    createDocumentToPdf.mockResolvedValue({
      jobId: "job-1",
      correlationId: "corr-1"
    });

    const user = userEvent.setup();
    const view = renderWithProviders(<CreateJobPage />);

    const input = view.container.querySelector("input[type='file']") as HTMLInputElement;
    const file = new File(["hello"], "sample.txt", { type: "text/plain" });
    const files = createFileList(file);

    fireEvent.change(input, {
      target: {
        files
      }
    });
    await user.click(screen.getByRole("button", { name: /submit conversion job/i }));

    await waitFor(() => {
      expect(createDocumentToPdf).toHaveBeenCalledWith(file, "");
    });
  });
});

function createFileList(...files: File[]) {
  const fileList = Object.create(FileList.prototype) as FileList;

  Object.defineProperty(fileList, "length", {
    value: files.length
  });

  files.forEach((file, index) => {
    Object.defineProperty(fileList, index, {
      value: file,
      enumerable: true
    });
  });

  Object.defineProperty(fileList, "item", {
    value: (index: number) => files[index] ?? null
  });

  return fileList;
}
