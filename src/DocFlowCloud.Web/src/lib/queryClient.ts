import { QueryClient } from "@tanstack/react-query";

// QueryClient：统一管理前端的服务端数据缓存、重试和刷新策略。
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false
    }
  }
});
