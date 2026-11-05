import { initials, colorFromString } from '../../lib/format'

/** Initials avatar with a deterministic background color. */
export default function Avatar({ name, size = 'md', className = '' }) {
  const SIZES = {
    sm: 'h-7 w-7 text-[10px]',
    md: 'h-9 w-9 text-xs',
    lg: 'h-12 w-12 text-sm',
    xl: 'h-16 w-16 text-lg',
  }
  return (
    <span
      aria-hidden="true"
      className={`inline-flex shrink-0 items-center justify-center rounded-full font-semibold text-white ${SIZES[size] || SIZES.md} ${className}`}
      style={{ backgroundColor: colorFromString(name) }}
    >
      {initials(name)}
    </span>
  )
}
