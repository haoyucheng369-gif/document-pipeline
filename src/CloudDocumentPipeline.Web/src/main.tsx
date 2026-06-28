import { QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { ToastProvider } from "./components/ToastProvider";
import { getRuntimeAppEnvironment } from "./lib/runtimeConfig";
import { queryClient } from "./lib/queryClient";
import "./styles.css";

// Use the runtime environment label in the browser title so cloud deployments
// can switch environments without rebuilding the frontend image.
const appEnv = getRuntimeAppEnvironment();
document.title = `CloudDocumentPipeline - ${appEnv}`;

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </ToastProvider>
    </QueryClientProvider>
  </React.StrictMode>
);
