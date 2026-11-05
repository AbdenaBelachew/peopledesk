import { useMemo, useState } from 'react'
import { ChevronLeft, ChevronRight, CalendarDays } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { useLeaveCalendar, useStructure } from '../../api/queries'
import { formatDate, colorFromString } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Button from '../../components/ui/Button'
import Select from '../../components/ui/Select'
import Spinner from '../../components/ui/Spinner'
import Badge from '../../components/ui/Badge'

const DOW = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

function monthTitle(year, month) {
  return new Date(year, month - 1, 1).toLocaleString([], { month: 'long', year: 'numeric' })
}

export default function LeaveCalendar() {
  const { hasPerm } = useAuth()
  const now = new Date()
  const [ym, setYm] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 })
  const [departmentId, setDepartmentId] = useState('')
  const departments = useStructure('departments', { enabled: hasPerm('leave.viewAll') })

  const pad = (x) => String(x).padStart(2, '0')
  const from = `${ym.year}-${pad(ym.month)}-01`
  const to = dateInputEnd(ym.year, ym.month)
  const calendar = useLeaveCalendar({ from, to, departmentId })

  // Group entries by ISO day key (entries spanning multiple days appear on each day).
  const byDay = useMemo(() => {
    const map = new Map()
    for (const req of calendar.data || []) {
      const start = new Date(req.startDate)
      const end = new Date(req.endDate)
      if (Number.isNaN(start.getTime())) continue
      const cursor = new Date(start)
      while (cursor <= end) {
        const key = `${cursor.getFullYear()}-${pad(cursor.getMonth() + 1)}-${pad(cursor.getDate())}`
        if (!map.has(key)) map.set(key, [])
        map.get(key).push(req)
        cursor.setDate(cursor.getDate() + 1)
      }
    }
    return map
  }, [calendar.data])

  // Monday-first grid cells.
  const cells = useMemo(() => {
    const first = new Date(ym.year, ym.month - 1, 1)
    const daysInMonth = new Date(ym.year, ym.month, 0).getDate()
    const lead = (first.getDay() + 6) % 7
    const list = []
    for (let i = 0; i < lead; i++) list.push(null)
    for (let d = 1; d <= daysInMonth; d++) {
      list.push({ day: d, key: `${ym.year}-${pad(ym.month)}-${pad(d)}`, isToday: d === now.getDate() && ym.month === now.getMonth() + 1 && ym.year === now.getFullYear() })
    }
    while (list.length % 7 !== 0) list.push(null)
    return list
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ym])

  const legendTypes = useMemo(() => {
    const seen = new Map()
    for (const r of calendar.data || []) {
      if (!seen.has(r.leaveTypeId)) seen.set(r.leaveTypeId, { name: r.typeName, color: r.colorHex })
    }
    return [...seen.values()]
  }, [calendar.data])

  function moveMonth(delta) {
    setYm(({ year, month }) => {
      const d = new Date(year, month - 1 + delta, 1)
      return { year: d.getFullYear(), month: d.getMonth() + 1 }
    })
  }

  return (
    <div>
      <PageHeader
        title="Leave calendar"
        subtitle={monthTitle(ym.year, ym.month)}
        actions={
          <div className="flex items-center gap-2">
            {hasPerm('leave.viewAll') && (
              <Select
                aria-label="Filter by department"
                placeholder="All departments"
                value={departmentId}
                onChange={(e) => setDepartmentId(e.target.value)}
                options={(departments.data || []).map((d) => ({ value: d.id, label: d.name }))}
                className="!mt-0"
              />
            )}
            <Button variant="secondary" size="icon" aria-label="Previous month" onClick={() => moveMonth(-1)}>
              <ChevronLeft className="h-4 w-4" aria-hidden="true" />
            </Button>
            <Button variant="secondary" size="sm" onClick={() => setYm({ year: now.getFullYear(), month: now.getMonth() + 1 })}>
              Today
            </Button>
            <Button variant="secondary" size="icon" aria-label="Next month" onClick={() => moveMonth(1)}>
              <ChevronRight className="h-4 w-4" aria-hidden="true" />
            </Button>
          </div>
        }
      />

      {legendTypes.length > 0 && (
        <div className="mb-3 flex flex-wrap gap-2">
          {legendTypes.map((t) => (
            <Badge key={t.name} tone="slate">
              <span className="mr-1.5 inline-block h-2.5 w-2.5 rounded-full" style={{ backgroundColor: t.color || colorFromString(t.name) }} aria-hidden="true" />
              {t.name}
            </Badge>
          ))}
        </div>
      )}

      <Card bodyClassName="!p-2 sm:!p-4">
        {calendar.isLoading ? (
          <div className="py-16 text-center"><Spinner className="mx-auto" /></div>
        ) : (
          <>
            <div className="grid grid-cols-7 gap-1 text-center text-xs font-semibold uppercase text-slate-400 dark:text-slate-500">
              {DOW.map((d) => <div key={d} className="py-1">{d}</div>)}
            </div>
            <div className="grid grid-cols-7 gap-1">
              {cells.map((cell, i) => (
                <div
                  key={i}
                  className={`min-h-20 rounded-md border p-1 sm:min-h-24 ${
                    cell
                      ? 'border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900'
                      : 'border-transparent bg-slate-50/50 dark:bg-slate-900/40'
                  }`}
                >
                  {cell && (
                    <>
                      <p className={`mb-1 text-right text-xs font-medium ${cell.isToday ? 'flex items-center justify-end gap-1 text-blue-600 dark:text-blue-400' : 'text-slate-400'}`}>
                        {cell.isToday && <span className="h-1.5 w-1.5 rounded-full bg-blue-500" aria-hidden="true" />}
                        {cell.day}
                      </p>
                      <div className="space-y-0.5">
                        {(byDay.get(cell.key) || []).slice(0, 3).map((r) => (
                          <div
                            key={`${r.requestId}-${cell.key}`}
                            title={`${r.employeeName} — ${r.typeName} (${r.status})`}
                            className="truncate rounded px-1 py-0.5 text-[10px] font-medium text-white"
                            style={{
                              backgroundColor: r.colorHex || colorFromString(r.typeName),
                              opacity: r.status === 'Approved' ? 1 : r.status === 'Pending' ? 0.65 : 0.4,
                              textDecoration: r.status === 'Rejected' || r.status === 'Cancelled' ? 'line-through' : 'none',
                            }}
                          >
                            {r.employeeName}
                          </div>
                        ))}
                        {(byDay.get(cell.key) || []).length > 3 && (
                          <p className="px-1 text-[10px] text-slate-400">+{(byDay.get(cell.key)).length - 3} more</p>
                        )}
                      </div>
                    </>
                  )}
                </div>
              ))}
            </div>
          </>
        )}
      </Card>

      {!calendar.isLoading && !(calendar.data || []).length && (
        <p className="mt-3 flex items-center justify-center gap-1.5 text-sm text-slate-400">
          <CalendarDays className="h-4 w-4" aria-hidden="true" /> No leave scheduled this month (entries from {formatDate(from)} to {formatDate(to)}).
        </p>
      )}
    </div>
  )
}

function dateInputEnd(year, month) {
  const last = new Date(year, month, 0).getDate()
  return `${year}-${String(month).padStart(2, '0')}-${String(last).padStart(2, '0')}`
}
