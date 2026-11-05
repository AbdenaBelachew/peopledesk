import { forwardRef, useId } from 'react'
import Label from './Label'

const BASE =
  'w-full rounded-md border bg-white dark:bg-slate-900 px-3 py-2 text-sm text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 disabled:opacity-60 disabled:bg-slate-50 dark:disabled:bg-slate-800'

function FieldShell({ id, label, error, hint, className, children }) {
  return (
    <div className={className}>
      {label && <Label htmlFor={id}>{label}</Label>}
      <div className={label ? 'mt-1.5' : ''}>{children}</div>
      {error && <p className="mt-1 text-xs text-red-600 dark:text-red-400" role="alert">{error}</p>}
      {hint && !error && <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{hint}</p>}
    </div>
  )
}

export { FieldShell, BASE }

const Input = forwardRef(function Input(
  { label, error, hint, className = '', id, type = 'text', ...rest },
  ref,
) {
  const autoId = useId()
  const inputId = id || autoId
  return (
    <FieldShell id={inputId} label={label} error={error} hint={hint} className={className}>
      <input
        ref={ref}
        id={inputId}
        type={type}
        aria-invalid={!!error || undefined}
        className={`${BASE} ${error ? 'border-red-400 focus:ring-red-500 focus:border-red-500' : 'border-slate-300 dark:border-slate-700'}`}
        {...rest}
      />
    </FieldShell>
  )
})

export default Input
