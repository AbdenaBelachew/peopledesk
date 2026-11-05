import { useState } from 'react'
import { Plus, ClipboardCheck } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { useCorrections, useCreateCorrection, useReviewCorrection } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { formatDate, formatTime, statusTone, humanize, dateInput, todayInput } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Textarea from '../../components/ui/Textarea'
import Select from '../../components/ui/Select'
import Modal from '../../components/ui/Modal'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'
import Tabs from '../../components/ui/Tabs'

const STATUSES = ['Pending', 'Approved', 'Rejected']

export default function Corrections() {
  const { user, hasPerm } = useAuth()
  const canReview = hasPerm('attendance.approve')
  const [status, setStatus] = useState('Pending')
  const [requestOpen, setRequestOpen] = useState(false)
  const [reviewFor, setReviewFor] = useState(null) // { correction, approve }
  const [note, setNote] = useState('')

  const corrections = useCorrections(status === 'All' ? undefined : status)
  const review = useReviewCorrection()

  const myEmployeeId = user?.employeeId
  const items = corrections.data || []
  const mine = myEmployeeId ? items.filter((c) => c.employeeId === myEmployeeId) : []

  const columns = [
    { key: 'employeeName', header: 'Employee' },
    { key: 'workDate', header: 'Work date', render: (r) => formatDate(r.workDate) },
    { key: 'requestedClockInUtc', header: 'Requested in', render: (r) => formatTime(r.requestedClockInUtc) },
    { key: 'requestedClockOutUtc', header: 'Requested out', render: (r) => formatTime(r.requestedClockOutUtc) },
    { key: 'requestedBreakMinutes', header: 'Break', render: (r) => (r.requestedBreakMinutes ? `${r.requestedBreakMinutes}m` : '—') },
    { key: 'reason', header: 'Reason', render: (r) => <span className="max-w-56 truncate">{r.reason}</span> },
    { key: 'status', header: 'Status', render: (r) => <Badge tone={statusTone(r.status)}>{r.status}</Badge> },
    { key: 'reviewNote', header: 'Review note', render: (r) => r.reviewNote || '—' },
    ...(canReview
      ? [{
          key: 'actions',
          header: '',
          className: 'text-right',
          render: (r) =>
            r.status === 'Pending' ? (
              <span className="flex justify-end gap-1.5">
                <Button size="sm" onClick={() => { setReviewFor({ correction: r, approve: true }); setNote('') }}>Approve</Button>
                <Button size="sm" variant="secondary" onClick={() => { setReviewFor({ correction: r, approve: false }); setNote('') }}>Reject</Button>
              </span>
            ) : null,
        }]
      : []),
  ]

  async function submitReview() {
    if (!reviewFor) return
    try {
      await review.mutateAsync({
        id: reviewFor.correction.id,
        approve: reviewFor.approve,
        reviewNote: note.trim() || undefined,
      })
      toast.success(reviewFor.approve ? 'Correction approved' : 'Correction rejected')
      setReviewFor(null)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  return (
    <div>
      <PageHeader
        title="Attendance corrections"
        subtitle="Fix clock-in/out mistakes through reviewed requests."
        actions={<Button onClick={() => setRequestOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" /> Request correction</Button>}
      />

      {canReview ? (
        <>
          <Tabs
            tabs={[...STATUSES.map((s) => ({ key: s, label: s })), { key: 'All', label: 'All' }]}
            active={status}
            onChange={(k) => setStatus(k)}
            className="mb-4"
          />
          <DataTable
            columns={columns}
            rows={items}
            loading={corrections.isLoading}
            empty={<EmptyState icon={ClipboardCheck} title={`No ${status.toLowerCase()} corrections`} description="Correction requests will appear here." />}
          />
        </>
      ) : (
        <Card title="My correction requests" bodyClassName="!p-0">
          <DataTable
            columns={columns.filter((c) => c.key !== 'employeeName')}
            rows={mine}
            loading={corrections.isLoading}
            empty={<EmptyState icon={ClipboardCheck} title="No requests" description="Request a correction when your clock data is wrong." />}
          />
        </Card>
      )}

      {/* Review dialog */}
      <Modal
        open={!!reviewFor}
        onClose={() => setReviewFor(null)}
        title={reviewFor?.approve ? 'Approve correction' : 'Reject correction'}
        size="sm"
        footer={
          <>
            <Button variant="secondary" onClick={() => setReviewFor(null)}>Cancel</Button>
            <Button variant={reviewFor?.approve ? 'primary' : 'danger'} onClick={submitReview} loading={review.isPending}>
              {reviewFor?.approve ? 'Approve' : 'Reject'}
            </Button>
          </>
        }
      >
        {reviewFor && (
          <div className="space-y-3 text-sm text-slate-600 dark:text-slate-300">
            <p>
              <span className="font-medium text-slate-900 dark:text-slate-100">{reviewFor.correction.employeeName}</span>
              {' '}on {formatDate(reviewFor.correction.workDate)}: {formatTime(reviewFor.correction.requestedClockInUtc)} → {formatTime(reviewFor.correction.requestedClockOutUtc)}
            </p>
            {reviewFor.correction.reason && <p className="text-xs text-slate-400">Reason: {reviewFor.correction.reason}</p>}
            <Textarea label="Review note (optional)" value={note} onChange={(e) => setNote(e.target.value)} />
          </div>
        )}
      </Modal>

      <RequestModal open={requestOpen} onClose={() => setRequestOpen(false)} />
    </div>
  )
}

function RequestModal({ open, onClose }) {
  const toast = useToast()
  const create = useCreateCorrection()
  const [values, setValues] = useState({
    workDate: todayInput(), clockIn: '', clockOut: '', breakMinutes: '', reason: '',
  })

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  async function submit(ev) {
    ev.preventDefault()
    if (!values.workDate || !values.reason.trim()) {
      toast.error('Work date and reason are required')
      return
    }
    const toIso = (t) => (t ? new Date(`${values.workDate}T${t}`).toISOString() : undefined)
    try {
      await create.mutateAsync({
        workDate: values.workDate,
        requestedClockInUtc: toIso(values.clockIn),
        requestedClockOutUtc: toIso(values.clockOut),
        requestedBreakMinutes: values.breakMinutes ? Number(values.breakMinutes) : undefined,
        reason: values.reason.trim(),
      })
      toast.success('Correction requested')
      onClose()
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Request attendance correction"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={create.isPending}>Submit request</Button></>}>
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Input label="Work date" type="date" value={values.workDate} onChange={(e) => set('workDate', e.target.value)} required />
        <Input label="Break minutes (optional)" type="number" min="0" value={values.breakMinutes} onChange={(e) => set('breakMinutes', e.target.value)} />
        <Input label="Correct clock in" type="time" value={values.clockIn} onChange={(e) => set('clockIn', e.target.value)} />
        <Input label="Correct clock out" type="time" value={values.clockOut} onChange={(e) => set('clockOut', e.target.value)} />
        <Textarea label="Reason" value={values.reason} onChange={(e) => set('reason', e.target.value)} required className="sm:col-span-2"
          placeholder="Explain what the correct times are and why" />
      </form>
    </Modal>
  )
}
