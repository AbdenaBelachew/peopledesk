import { forwardRef } from 'react'

export default forwardRef(function Label({ className = '', ...rest }, ref) {
  return (
    <label
      ref={ref}
      className={`block text-sm font-medium text-slate-700 dark:text-slate-300 ${className}`}
      {...rest}
    />
  )
})
