import { Link, NavLink } from "react-router-dom";
import type { PropsWithChildren } from "react";

const navClassName = ({ isActive }: { isActive: boolean }) =>
  [
    "rounded-full px-4 py-2 text-sm font-medium transition-colors",
    isActive ? "bg-accent text-white" : "text-ink hover:bg-soft"
  ].join(" ");

export function Layout({ children }: PropsWithChildren) {
  return (
    <div className="min-h-screen bg-mist text-ink">
      <header className="border-b border-line bg-white/90 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-5">
          <Link to="/" className="text-xl font-semibold tracking-tight">
            DocFlowCloud
          </Link>
          <nav className="flex gap-2 rounded-full border border-line bg-white p-1">
            <NavLink to="/" end className={navClassName}>
              上传任务
            </NavLink>
            <NavLink to="/jobs" className={navClassName}>
              任务列表
            </NavLink>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-6 py-10">{children}</main>
    </div>
  );
}
