const TONES = {
  blue: 'bg-blue-50 text-blue-600 dark:bg-blue-500/10 dark:text-blue-300',
  green: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-300',
  amber: 'bg-amber-50 text-amber-600 dark:bg-amber-500/10 dark:text-amber-300',
  red: 'bg-red-50 text-red-600 dark:bg-red-500/10 dark:text-red-300',
  purple: 'bg-purple-50 text-purple-600 dark:bg-purple-500/10 dark:text-purple-300',
  slate: 'bg-slate-100 text-slate-600 dark:bg-slate-500/10 dark:text-slate-300',
}

export default function StatCard({ label, value, icon: Icon, hint, tone = 'blue', loading = false }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-xs font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">{label}</p>
          {loading ? (
            <div className="mt-2 h-7 w-16 animate-pulse rounded bg-slate-200 dark:bg-slate-700" />
          ) : (
            <p className="mt-1 truncate text-2xl font-bold tracking-tight text-slate-900 dark:text-slate-100">{value}</p>
          )}
          {hint && !loading && <p className="mt-0.5 truncate text-xs text-slate-400 dark:text-slate-500">{hint}</p>}
        </div>
        {Icon && (
          <div className={`rounded-lg p-2.5 ${TONES[tone] || TONES.blue}`} aria-hidden="true">
            <Icon className="h-5 w-5" />
          </div>
        )}
      </div>
    </div>
  )
}
