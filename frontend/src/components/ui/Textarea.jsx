import { forwardRef, useId } from 'react'
import { FieldShell, BASE } from './Input'

const Textarea = forwardRef(function Textarea(
  { label, error, hint, className = '', id, rows = 3, ...rest },
  ref,
) {
  const autoId = useId()
  const areaId = id || autoId
  return (
    <FieldShell id={areaId} label={label} error={error} hint={hint} className={className}>
      <textarea
        ref={ref}
        id={areaId}
        rows={rows}
        aria-invalid={!!error || undefined}
        className={`${BASE} ${error ? 'border-red-400 focus:ring-red-500 focus:border-red-500' : 'border-slate-300 dark:border-slate-700'}`}
        {...rest}
      />
    </FieldShell>
  )
})

export default Textarea
