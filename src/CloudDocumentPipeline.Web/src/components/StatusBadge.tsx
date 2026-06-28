type Props = { status: string };

// 状态徽标：把后端任务状态转换成直观颜色。
const styles: Record<string, string> = {
  Pending: "bg-slate-100 text-slate-700",
  Processing: "bg-amber-100 text-amber-800",
  Succeeded: "bg-emerald-100 text-emerald-800",
  Failed: "bg-red-100 text-red-800"
};

export function StatusBadge({ status }: Props) {
  return (
    <span
      className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${styles[status] ?? "bg-slate-100 text-slate-700"}`}
    >
      {status}
    </span>
  );
}
