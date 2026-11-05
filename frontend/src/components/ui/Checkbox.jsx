import { useId } from 'react'

/** Styled checkbox with an optional right-side label. */
export default function Checkbox({ label, description, className = '', id, ...rest }) {
  const autoId = useId()
  const cbId = id || autoId
  return (
    <div className={`flex items-start gap-2 ${className}`}>
      <input
        id={cbId}
        type="checkbox"
        className="mt-0.5 h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-2 focus:ring-blue-500 dark:border-slate-600 dark:bg-slate-800"
        {...rest}
      />
      {(label || description) && (
        <div>
          {label && (
            <label htmlFor={cbId} className="block text-sm font-medium text-slate-700 dark:text-slate-300">
              {label}
            </label>
          )}
          {description && <p className="text-xs text-slate-500 dark:text-slate-400">{description}</p>}
        </div>
      )}
    </div>
  )
}
