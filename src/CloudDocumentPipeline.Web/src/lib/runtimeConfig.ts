function readRuntimeValue(value?: string) {
  return value?.toString().trim() || undefined;
}

export function getRuntimeApiBaseUrl() {
  return (
    readRuntimeValue(window.__APP_CONFIG__?.apiBaseUrl) ??
    readRuntimeValue(import.meta.env.VITE_API_BASE_URL) ??
    "http://localhost:8080"
  );
}

export function getRuntimeAppEnvironment() {
  return (
    readRuntimeValue(window.__APP_CONFIG__?.appEnvironment) ??
    readRuntimeValue(import.meta.env.VITE_APP_ENV) ??
    "development"
  );
}
