import { QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { ToastProvider } from "./components/ToastProvider";
import { queryClient } from "./lib/queryClient";
import "./styles.css";

// 启动时按环境补齐浏览器标题，方便同时开多个环境页面时快速区分。
const appEnv = import.meta.env.VITE_APP_ENV?.toString()?.trim() || "testbed";
document.title = `DocFlowCloud - ${appEnv}`;

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
