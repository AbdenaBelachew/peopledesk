import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Clock, LogOut, Users } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import {
  useAttendanceToday, useClockIn, useClockOut, useAttendance,
} from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { formatDate, formatTime, duration, statusTone, humanize } from '../../lib/format'
import { useNow, useDebounce } from '../../lib/hooks'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'
import PageHeader from '../../components/ui/PageHeader'
import Avatar from '../../components/ui/Avatar'

export default function AttendanceToday() {
  const { user, hasPerm } = useAuth()
  const canViewAll = hasPerm('attendance.viewAll')

  return (
    <div>
      <PageHeader
        title="Attendance"
        subtitle={todayLabel()}
        actions={
          <div className="flex gap-2">
            {hasPerm('attendance.viewAll') && (
              <Link to="/attendance/timesheets"><Button variant="secondary">Timesheets</Button></Link>
            )}
            <Link to="/attendance/corrections"><Button variant="secondary">Corrections</Button></Link>
          </div>
        }
      />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <ClockWidget />
        {canViewAll && (
          <div className="lg:col-span-2">
            <TeamToday />
          </div>
        )}
      </div>
    </div>
  )
}

function todayLabel() {
  return new Date().toLocaleDateString([], { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' })
}

function ClockWidget() {
  const today = useAttendanceToday()
  const clockIn = useClockIn()
  const clockOut = useClockOut()
  const toast = useToast()
  const now = useNow()
  const record = today.data

  async function doClockIn() {
    try {
      const res = await clockIn.mutateAsync()
      toast.success(res?.lateMinutes > 0 ? `Clocked in — ${res.lateMinutes}m late` : 'Clocked in')
    } catch (e) { toast.error(await apiErrorMessage(e)) }
  }

  async function doClockOut() {
    try {
      const res = await clockOut.mutateAsync()
      toast.success(res?.workedMinutes ? `Clocked out — worked ${duration(res.workedMinutes)}` : 'Clocked out')
    } catch (e) { toast.error(await apiErrorMessage(e)) }
  }

  return (
    <Card title="My clock" className="lg:col-span-1">
      <div className="flex flex-col items-center py-6 text-center">
        <p className="text-4xl font-bold tabular-nums text-slate-900 dark:text-white">
          {now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false })}
        </p>

        {today.isLoading ? (
          <div className="mt-6"><Clock className="h-8 w-8 animate-pulse text-slate-300" aria-hidden="true" /></div>
        ) : !record ? (
          <>
            <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">Not clocked in yet today.</p>
            <Button size="lg" className="mt-4 w-full max-w-56" loading={clockIn.isPending} onClick={doClockIn}>
              <Clock className="h-4 w-4" aria-hidden="true" /> Clock in
            </Button>
          </>
        ) : !record.clockOutUtc ? (
          <>
            <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
              Working since <span className="font-semibold text-slate-900 dark:text-white">{formatTime(record.clockInUtc)}</span>
              {record.lateMinutes > 0 && <span className="ml-1 text-amber-600 dark:text-amber-400">({record.lateMinutes}m late)</span>}
            </p>
            <Button size="lg" variant="danger" className="mt-4 w-full max-w-56" loading={clockOut.isPending} onClick={doClockOut}>
              <LogOut className="h-4 w-4" aria-hidden="true" /> Clock out
            </Button>
          </>
        ) : (
          <>
            <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
              {formatTime(record.clockInUtc)} → {formatTime(record.clockOutUtc)}
            </p>
            <p className="mt-2 text-3xl font-bold text-emerald-600 dark:text-emerald-400">{duration(record.workedMinutes)}</p>
            <p className="text-xs text-slate-400">worked — day complete</p>
          </>
        )}
      </div>
    </Card>
  )
}

function TeamToday() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const debounced = useDebounce(search)
  const today = new Date()
  const p = (x) => String(x).padStart(2, '0')
  const day = `${today.getFullYear()}-${p(today.getMonth() + 1)}-${p(today.getDate())}`

  const attendance = useAttendance({ page, pageSize: 15, from: day, to: day, search: debounced })

  const columns = [
    { key: 'employeeName', header: 'Employee', render: (r) => (
      <span className="flex items-center gap-2">
        <Avatar name={r.employeeName} size="sm" />
        <span>
          <span className="block font-medium text-slate-900 dark:text-slate-100">{r.employeeName}</span>
          <span className="text-xs text-slate-400">{r.employeeNumber}</span>
        </span>
      </span>
    ) },
    { key: 'clockInUtc', header: 'In', render: (r) => formatTime(r.clockInUtc) },
    { key: 'clockOutUtc', header: 'Out', render: (r) => formatTime(r.clockOutUtc) },
    { key: 'workedMinutes', header: 'Worked', render: (r) => duration(r.workedMinutes) },
    { key: 'lateMinutes', header: 'Late', render: (r) => (r.lateMinutes ? `${r.lateMinutes}m` : '—') },
    { key: 'overtimeMinutes', header: 'Overtime', render: (r) => (r.overtimeMinutes ? `${r.overtimeMinutes}m` : '—') },
    { key: 'status', header: 'Status', render: (r) => <Badge tone={statusTone(r.status)}>{humanize(r.status)}</Badge> },
    { key: 'source', header: 'Source', render: (r) => humanize(r.source) },
  ]

  return (
    <Card title="Team today" bodyClassName="!p-0">
      <div className="border-b border-slate-100 p-3 dark:border-slate-800">
        <Input
          placeholder="Search employee…"
          aria-label="Search team attendance"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
          className="max-w-xs"
        />
      </div>
      <DataTable
        columns={columns}
        rows={attendance.data?.items || []}
        loading={attendance.isLoading}
        page={attendance.data?.page || page}
        pageSize={attendance.data?.pageSize || 15}
        totalCount={attendance.data?.totalCount || 0}
        totalPages={attendance.data?.totalPages || 1}
        onPageChange={setPage}
        empty={<EmptyState icon={Users} title="No records today" description="Nobody has clocked in yet." />}
      />
    </Card>
  )
}
