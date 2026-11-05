// Shared formatting helpers.

/** Money: "ETB 10,138.50" */
export function money(value, currency = 'ETB') {
  const n = Number(value ?? 0)
  const safe = Number.isFinite(n) ? n : 0
  return `${currency || 'ETB'} ${safe.toLocaleString('en-US', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
}

export function num(value) {
  const n = Number(value ?? 0)
  return (Number.isFinite(n) ? n : 0).toLocaleString('en-US')
}

/** "Nov 4, 2026" */
export function formatDate(value) {
  if (!value) return '—'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

/** "Nov 4, 2026, 09:30" */
export function formatDateTime(value) {
  if (!value) return '—'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

/** "09:30" */
export function formatTime(value) {
  if (!value) return '—'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false })
}

/** yyyy-mm-dd for <input type="date"> */
export function dateInput(value) {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  const p = (x) => String(x).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

export function todayInput() {
  return dateInput(new Date().toISOString())
}

/** minutes -> "2h 15m" */
export function duration(minutes) {
  const m = Number(minutes)
  if (!Number.isFinite(m) || m <= 0) return '—'
  const h = Math.floor(m / 60)
  const mm = Math.round(m % 60)
  if (h === 0) return `${mm}m`
  if (mm === 0) return `${h}h`
  return `${h}h ${mm}m`
}

/** "Sara Bekele" -> "SB" */
export function initials(name) {
  if (!name) return '?'
  const parts = String(name).trim().split(/\s+/).slice(0, 2)
  return parts.map((p) => p[0]?.toUpperCase() || '').join('') || '?'
}

export const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
]

export const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

/** Deterministic color for avatars/charts from a string. */
export function colorFromString(str) {
  const palette = [
    '#2563eb', '#0ea5e9', '#8b5cf6', '#10b981', '#f59e0b',
    '#ef4444', '#14b8a6', '#f472b6', '#6366f1', '#84cc16',
  ]
  let h = 0
  for (let i = 0; i < String(str || '').length; i++) h = (h * 31 + str.charCodeAt(i)) >>> 0
  return palette[h % palette.length]
}

/** slug for org registration auto-suggest */
export function slugify(text) {
  return String(text || '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
}

/** "Pending" -> "pending" */
export function statusTone(status) {
  switch (String(status || '').toLowerCase()) {
    case 'active': case 'approved': case 'finalized': case 'present': case 'published': case 'paid':
      return 'green'
    case 'pending': case 'probation': case 'trialing': case 'calculated': case 'draft': case 'pastdue':
      return 'amber'
    case 'rejected': case 'terminated': case 'suspended': case 'absent': case 'reversed': case 'cancelled': case 'canceled':
      return 'red'
    case 'onleave': case 'on_leave': case 'halfday':
      return 'purple'
    case 'resigned': case 'retired': case 'closed':
      return 'slate'
    default:
      return 'blue'
  }
}

export function humanize(value) {
  if (value === null || value === undefined) return '—'
  return String(value)
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
}
