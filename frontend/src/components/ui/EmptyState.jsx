export default function EmptyState({ icon: Icon, title, description, action, className = '' }) {
  return (
    <div className={`flex flex-col items-center justify-center px-4 py-10 text-center ${className}`}>
      {Icon && (
        <div className="rounded-full bg-slate-100 p-3 dark:bg-slate-800">
          <Icon className="h-6 w-6 text-slate-400 dark:text-slate-500" aria-hidden="true" />
        </div>
      )}
      <h3 className="mt-3 text-sm font-semibold text-slate-900 dark:text-slate-100">{title}</h3>
      {description && <p className="mt-1 max-w-sm text-sm text-slate-500 dark:text-slate-400">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  )
}
