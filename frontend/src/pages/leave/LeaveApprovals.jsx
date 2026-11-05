import { useState } from 'react'
import { CheckCircle2, XCircle, Inbox } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { useLeaveRequests, useReviewLeaveRequest } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { formatDate, statusTone } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Textarea from '../../components/ui/Textarea'
import Checkbox from '../../components/ui/Checkbox'
import Modal from '../../components/ui/Modal'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'
import Avatar from '../../components/ui/Avatar'

export default function LeaveApprovals() {
  const { hasPerm } = useAuth()
  const canOverride = hasPerm('leave.manage')
  const [page, setPage] = useState(1)
  const requests = useLeaveRequests({ status: 'Pending', page, pageSize: 15 })
  const review = useReviewLeaveRequest()
  const toast = useToast()
  const [dialog, setDialog] = useState(null) // { request, approve }
  const [note, setNote] = useState('')
  const [override, setOverride] = useState(false)

  function openDialog(request, approve) {
    setDialog({ request, approve })
    setNote('')
    setOverride(false)
  }

  async function submit() {
    if (!dialog) return
    try {
      await review.mutateAsync({
        id: dialog.request.id,
        action: dialog.approve ? 'approve' : 'reject',
        approve: dialog.approve,
        reviewNote: note.trim() || undefined,
        isHrOverride: canOverride && override || undefined,
      })
      toast.success(dialog.approve ? 'Leave request approved' : 'Leave request rejected')
      setDialog(null)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

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
    { key: 'typeName', header: 'Type', render: (r) => (
      <span className="inline-flex items-center gap-1.5">
        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: r.colorHex || '#2563eb' }} aria-hidden="true" />
        {r.typeName}{!r.isPaid && <span className="text-xs text-slate-400">(unpaid)</span>}
      </span>
    ) },
    { key: 'startDate', header: 'Dates', render: (r) => `${formatDate(r.startDate)} → ${formatDate(r.endDate)}` },
    { key: 'days', header: 'Days', className: 'text-right', render: (r) => <span className="font-semibold">{r.days}</span> },
    { key: 'reason', header: 'Reason', render: (r) => <span className="max-w-56 truncate">{r.reason || '—'}</span> },
    { key: 'hrOverride', header: 'Flags', render: (r) => (r.hrOverride ? <Badge tone="purple">HR override</Badge> : '') },
    {
      key: 'actions',
      header: '',
      className: 'text-right',
      render: (r) => (
        <span className="flex justify-end gap-1.5">
          <Button size="sm" aria-label={`Approve ${r.employeeName}'s request`} onClick={() => openDialog(r, true)}>
            <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" /> Approve
          </Button>
          <Button size="sm" variant="secondary" aria-label={`Reject ${r.employeeName}'s request`} onClick={() => openDialog(r, false)}>
            <XCircle className="h-3.5 w-3.5" aria-hidden="true" /> Reject
          </Button>
        </span>
      ),
    },
  ]

  return (
    <div>
      <PageHeader title="Leave approvals" subtitle="Requests waiting for a decision." />
      <Card bodyClassName="!p-0">
        <DataTable
          columns={columns}
          rows={requests.data?.items || []}
          loading={requests.isLoading}
          page={requests.data?.page || page}
          pageSize={requests.data?.pageSize || 15}
          totalCount={requests.data?.totalCount || 0}
          totalPages={requests.data?.totalPages || 1}
          onPageChange={setPage}
          empty={<EmptyState icon={Inbox} title="Queue is clear" description="No pending leave requests right now." />}
        />
      </Card>

      <Modal
        open={!!dialog}
        onClose={() => setDialog(null)}
        title={dialog?.approve ? 'Approve leave request' : 'Reject leave request'}
        size="sm"
        footer={
          <>
            <Button variant="secondary" onClick={() => setDialog(null)}>Cancel</Button>
            <Button variant={dialog?.approve ? 'primary' : 'danger'} onClick={submit} loading={review.isPending}>
              {dialog?.approve ? 'Approve' : 'Reject'}
            </Button>
          </>
        }
      >
        {dialog && (
          <div className="space-y-4 text-sm text-slate-600 dark:text-slate-300">
            <div className="rounded-md bg-slate-50 p-3 dark:bg-slate-800/60">
              <p className="font-medium text-slate-900 dark:text-slate-100">{dialog.request.employeeName}</p>
              <p className="mt-0.5">
                {dialog.request.typeName} · {formatDate(dialog.request.startDate)} → {formatDate(dialog.request.endDate)} · {dialog.request.days} day(s)
              </p>
              {dialog.request.reason && <p className="mt-1 text-xs text-slate-400">“{dialog.request.reason}”</p>}
            </div>
            <Textarea label="Review note (optional)" value={note} onChange={(e) => setNote(e.target.value)} autoFocus />
            {canOverride && dialog.approve && (
              <Checkbox
                label="HR override"
                description="Force approval even if policy rules (balance, notice period, consecutive days) would block it."
                checked={override}
                onChange={(e) => setOverride(e.target.checked)}
              />
            )}
          </div>
        )}
      </Modal>
    </div>
  )
}
