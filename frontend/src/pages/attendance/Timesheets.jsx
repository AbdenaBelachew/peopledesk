import { useMemo, useState } from 'react'
import { Plus } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { useAttendance, useManualAttendance, useAttendanceSummary, useEmployees } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { formatDate, formatTime, duration, statusTone, humanize, dateInput } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Textarea from '../../components/ui/Textarea'
import Modal from '../../components/ui/Modal'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'
import StatCard from '../../components/ui/StatCard'

function monthBounds(d = new Date()) {
  const p = (x) => String(x).padStart(2, '0')
  const first = `${d.getFullYear()}-${p(d.getMonth() + 1)}-01`
  const last = dateInput(new Date(d.getFullYear(), d.getMonth() + 1, 0).toISOString())
  return { first, last }
}

export default function Timesheets() {
  const { user, hasPerm } = useAuth()
  const canViewAll = hasPerm('attendance.viewAll')
  const canManage = hasPerm('attendance.manage')
  const defaults = monthBounds()

  const [employeeId, setEmployeeId] = useState(canViewAll ? '' : user?.employeeId || '')
  const [from, setFrom] = useState(defaults.first)
  const [to, setTo] = useState(dateInput(new Date().toISOString()))
  const [page, setPage] = useState(1)
  const [manualOpen, setManualOpen] = useState(false)

  const employees = useEmployees({ page: 1, pageSize: 300 }, { enabled: canViewAll })
  const attendance = useAttendance({ page, pageSize: 20, employeeId, from, to })

  // Monthly summary follows the "from" date's month for the selected employee.
  const fromParts = from ? from.split('-').map(Number) : null
  const summary = useAttendanceSummary(employeeId, fromParts?.[0], fromParts?.[1])

  const employeeOptions = useMemo(
    () => (employees.data?.items || []).map((e) => ({ value: e.id, label: `${e.fullName} (${e.employeeNumber})` })),
    [employees.data],
  )

  const columns = [
    { key: 'employeeName', header: 'Employee' },
    { key: 'workDate', header: 'Date', render: (r) => formatDate(r.workDate) },
    { key: 'clockInUtc', header: 'In', render: (r) => formatTime(r.clockInUtc) },
    { key: 'clockOutUtc', header: 'Out', render: (r) => formatTime(r.clockOutUtc) },
    { key: 'breakMinutes', header: 'Break', render: (r) => (r.breakMinutes ? `${r.breakMinutes}m` : '—') },
    { key: 'workedMinutes', header: 'Worked', render: (r) => duration(r.workedMinutes) },
    { key: 'lateMinutes', header: 'Late', render: (r) => (r.lateMinutes ? `${r.lateMinutes}m` : '—') },
    { key: 'earlyDepartureMinutes', header: 'Early out', render: (r) => (r.earlyDepartureMinutes ? `${r.earlyDepartureMinutes}m` : '—') },
    { key: 'overtimeMinutes', header: 'Overtime', render: (r) => (r.overtimeMinutes ? `${r.overtimeMinutes}m` : '—') },
    { key: 'status', header: 'Status', render: (r) => <Badge tone={statusTone(r.status)}>{humanize(r.status)}</Badge> },
    { key: 'source', header: 'Source', render: (r) => humanize(r.source) },
    { key: 'note', header: 'Note', render: (r) => <span className="max-w-40 truncate text-xs text-slate-400">{r.note || '—'}</span> },
    { key: 'isLocked', header: 'Locked', render: (r) => (r.isLocked ? <Badge tone="slate">Locked</Badge> : '') },
  ]

  return (
    <div>
      <PageHeader
        title="Timesheets"
        subtitle="Attendance records and monthly summaries."
        actions={canManage && <Button onClick={() => setManualOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" /> Manual entry</Button>}
      />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {canViewAll && (
          <Select
            aria-label="Employee"
            placeholder="All employees"
            value={employeeId}
            onChange={(e) => { setEmployeeId(e.target.value); setPage(1) }}
            options={employeeOptions}
          />
        )}
        <Input label={canViewAll ? undefined : 'From'} type="date" aria-label="From date" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1) }} />
        <Input label={canViewAll ? undefined : 'To'} type="date" aria-label="To date" value={to} onChange={(e) => { setTo(e.target.value); setPage(1) }} />
      </div>

      {employeeId && (
        <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <StatCard label="Working days" value={summary.data?.workingDays ?? '—'} loading={summary.isLoading} />
          <StatCard label="Present" value={summary.data?.daysPresent ?? '—'} loading={summary.isLoading} />
          <StatCard label="Late" value={summary.data?.daysLate ?? '—'} loading={summary.isLoading} />
          <StatCard label="Absent" value={summary.data?.daysAbsent ?? '—'} loading={summary.isLoading} />
          <StatCard label="Worked" value={summary.data ? duration(summary.data.totalWorkedMinutes) : '—'} loading={summary.isLoading} />
          <StatCard label="Overtime" value={summary.data ? duration(summary.data.totalOvertimeMinutes) : '—'} loading={summary.isLoading} />
        </div>
      )}

      <DataTable
        columns={columns}
        rows={attendance.data?.items || []}
        loading={attendance.isLoading}
        page={attendance.data?.page || page}
        pageSize={attendance.data?.pageSize || 20}
        totalCount={attendance.data?.totalCount || 0}
        totalPages={attendance.data?.totalPages || 1}
        onPageChange={setPage}
        empty={<EmptyState title="No records" description="No attendance found for this range." />}
      />

      {canManage && (
        <ManualEntryModal
          open={manualOpen}
          onClose={() => setManualOpen(false)}
          employees={employeeOptions}
        />
      )}
    </div>
  )
}

function ManualEntryModal({ open, onClose, employees }) {
  const toast = useToast()
  const manual = useManualAttendance()
  const [values, setValues] = useState({
    employeeId: '', workDate: dateInput(new Date().toISOString()),
    clockIn: '09:00', clockOut: '17:30', breakMinutes: 60, status: 'Present', note: '',
  })
  const [errors, setErrors] = useState({})

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  async function submit(ev) {
    ev.preventDefault()
    const next = {}
    if (!values.employeeId) next.employeeId = 'Select an employee'
    if (!values.workDate) next.workDate = 'Date is required'
    setErrors(next)
    if (Object.keys(next).length) return

    // Combine date + time inputs into UTC ISO instants.
    const toIso = (t) => (t ? new Date(`${values.workDate}T${t}`).toISOString() : undefined)
    try {
      await manual.mutateAsync({
        employeeId: values.employeeId,
        workDate: values.workDate,
        clockInUtc: toIso(values.clockIn),
        clockOutUtc: toIso(values.clockOut),
        breakMinutes: Number(values.breakMinutes) || 0,
        status: values.status,
        note: values.note.trim() || undefined,
      })
      toast.success('Attendance entry saved')
      onClose()
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Manual attendance entry" size="md"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={manual.isPending}>Save entry</Button></>}>
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Select label="Employee" value={values.employeeId} onChange={(e) => set('employeeId', e.target.value)}
          options={employees} error={errors.employeeId} className="sm:col-span-2" />
        <Input label="Work date" type="date" value={values.workDate} onChange={(e) => set('workDate', e.target.value)} error={errors.workDate} />
        <Select label="Status" value={values.status} onChange={(e) => set('status', e.target.value)}
          options={['Present', 'Late', 'Absent', 'OnLeave', 'HalfDay', 'Holiday'].map((s) => ({ value: s, label: humanize(s) }))} />
        <Input label="Clock in" type="time" value={values.clockIn} onChange={(e) => set('clockIn', e.target.value)} />
        <Input label="Clock out" type="time" value={values.clockOut} onChange={(e) => set('clockOut', e.target.value)} />
        <Input label="Break (minutes)" type="number" min="0" value={values.breakMinutes} onChange={(e) => set('breakMinutes', e.target.value)} />
        <Input label="Note" value={values.note} onChange={(e) => set('note', e.target.value)} />
      </form>
    </Modal>
  )
}
