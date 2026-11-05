import { forwardRef } from 'react'
import { Loader2 } from 'lucide-react'

const VARIANTS = {
  primary:
    'bg-blue-600 text-white border border-transparent hover:bg-blue-700 active:bg-blue-800 focus-visible:ring-blue-500',
  secondary:
    'bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-200 border border-slate-300 dark:border-slate-600 hover:bg-slate-50 dark:hover:bg-slate-700 focus-visible:ring-blue-500',
  danger:
    'bg-red-600 text-white border border-transparent hover:bg-red-700 active:bg-red-800 focus-visible:ring-red-500',
  ghost:
    'text-slate-600 dark:text-slate-300 border border-transparent hover:bg-slate-100 dark:hover:bg-slate-800 focus-visible:ring-blue-500',
  subtle:
    'bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 border border-transparent hover:bg-blue-100 dark:hover:bg-blue-900/50 focus-visible:ring-blue-500',
}

const SIZES = {
  sm: 'h-8 px-3 text-xs gap-1.5',
  md: 'h-9 px-4 text-sm gap-2',
  lg: 'h-10 px-5 text-sm gap-2',
  icon: 'h-9 w-9 p-0',
}

const Button = forwardRef(function Button(
  { variant = 'primary', size = 'md', loading = false, disabled, className = '', type = 'button', children, ...rest },
  ref,
) {
  return (
    <button
      ref={ref}
      type={type}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={`inline-flex items-center justify-center rounded-lg font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-offset-white dark:focus-visible:ring-offset-slate-900 disabled:opacity-50 disabled:pointer-events-none ${VARIANTS[variant] || VARIANTS.primary} ${SIZES[size] || SIZES.md} ${className}`}
      {...rest}
    >
      {loading && <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />}
      {children}
    </button>
  )
})

export default Button
