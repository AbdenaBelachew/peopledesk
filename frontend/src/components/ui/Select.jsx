import { forwardRef, useId } from 'react'
import Label from './Label'
import { FieldShell, BASE } from './Input'

const Select = forwardRef(function Select(
  { label, error, hint, className = '', id, options, placeholder, children, ...rest },
  ref,
) {
  const autoId = useId()
  const selectId = id || autoId
  return (
    <FieldShell id={selectId} label={label} error={error} hint={hint} className={className}>
      <div className="relative">
        <select
          ref={ref}
          id={selectId}
          aria-invalid={!!error || undefined}
          className={`${BASE} appearance-none pr-8 ${error ? 'border-red-400 focus:ring-red-500 focus:border-red-500' : 'border-slate-300 dark:border-slate-700'}`}
          {...rest}
        >
          {placeholder !== undefined && <option value="">{placeholder}</option>}
          {(options || []).map((opt) => (
            <option key={String(opt.value)} value={opt.value}>
              {opt.label}
            </option>
          ))}
          {children}
        </select>
        <svg
          className="pointer-events-none absolute right-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400"
          viewBox="0 0 20 20"
          fill="currentColor"
          aria-hidden="true"
        >
          <path
            fillRule="evenodd"
            d="M5.23 7.21a.75.75 0 011.06.02L10 11.06l3.71-3.83a.75.75 0 111.08 1.04l-4.25 4.39a.75.75 0 01-1.08 0L5.21 8.27a.75.75 0 01.02-1.06z"
            clipRule="evenodd"
          />
        </svg>
      </div>
    </FieldShell>
  )
})

export default Select
