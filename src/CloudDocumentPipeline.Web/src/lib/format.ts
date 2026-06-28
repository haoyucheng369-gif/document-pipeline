export function formatDate(value?: string | null) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString();
}

export function canRetry(status: string) {
  return status === "Failed";
}
