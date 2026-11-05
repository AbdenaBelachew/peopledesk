import { useState } from 'react'
import { CalendarPlus, XCircle } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAuth } from '../../auth/AuthContext'
import {
  useLeaveTypes, useLeaveBalances, useLeaveRequests, useCreateLeaveRequest, useCancelLeaveRequest,
} from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { formatDate, statusTone, colorFromString, todayInput } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Textarea from '../../components/ui/Textarea'
import Checkbox from '../../components/ui/Checkbox'
import ConfirmDialog from '../../components/ui/ConfirmDialog'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'

const schema = z.object({
  leaveTypeId: z.string().min(1, 'Choose a leave type'),
  startDate: z.string().min(1, 'Start date is required'),
  endDate: z.string().min(1, 'End date is required'),
  isHalfDay: z.boolean(),
  reason: z.string(),
}).refine((v) => v.endDate >= v.startDate, {
  message: 'End date must be on or after the start date',
  path: ['endDate'],
})

export default function MyLeave() {
  const { user, hasPerm } = useAuth()
  const canViewAll = hasPerm('leave.viewAll')
  const toast = useToast()
  const year = new Date().getFullYear()
  const [page, setPage] = useState(1)

  const leaveTypes = useLeaveTypes()
  const balances = useLeaveBalances(user?.employeeId, year)
  // For reviewers the unfiltered endpoint returns everyone — scope to self explicitly.
  const requests = useLeaveRequests({ employeeId: canViewAll ? user?.employeeId : undefined, page, pageSize: 10 })
  const createRequest = useCreateLeaveRequest()
  const cancelRequest = useCancelLeaveRequest()
  const [cancelFor, setCancelFor] = useState(null)

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { leaveTypeId: '', startDate: '', endDate: '', isHalfDay: false, reason: '' },
  })

  const isHalfDay = watch('isHalfDay')

  async function onSubmit(values) {
    try {
      await createRequest.mutateAsync({
        // Send the caller's own employeeId explicitly (admins without a linked
        // employee account are rejected server-side with a clear message).
        employeeId: user?.employeeId || undefined,
        leaveTypeId: values.leaveTypeId,
        startDate: values.startDate,
        endDate: isHalfDay ? values.startDate : values.endDate,
        isHalfDay: values.isHalfDay,
        reason: values.reason.trim() || undefined,
      })
      toast.success('Leave request submitted')
      reset({ leaveTypeId: '', startDate: '', endDate: '', isHalfDay: false, reason: '' })
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  const columns = [
    { key: 'typeName', header: 'Type', render: (r) => (
      <span className="inline-flex items-center gap-1.5">
        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: r.colorHex || colorFromString(r.typeName) }} aria-hidden="true" />
        {r.typeName}
      </span>
    ) },
    { key: 'startDate', header: 'Dates', render: (r) => `${formatDate(r.startDate)} → ${formatDate(r.endDate)}` },
    { key: 'days', header: 'Days', className: 'text-right', render: (r) => `${r.days}${r.isPaid ? '' : ' (unpaid)'}` },
    { key: 'status', header: 'Status', render: (r) => <Badge tone={statusTone(r.status)}>{r.status}</Badge> },
    { key: 'reviewNote', header: 'Review', render: (r) => r.reviewNote ? <span className="max-w-48 truncate text-xs">{r.reviewNote}</span> : '—' },
    {
      key: 'actions', header: '', className: 'text-right',
      render: (r) =>
        ['Pending', 'Approved'].includes(r.status) && new Date(r.endDate) >= new Date(todayInput()) ? (
          <Button size="sm" variant="ghost" aria-label={`Cancel request for ${r.typeName}`} onClick={() => setCancelFor(r)}>
            <XCircle className="h-4 w-4 text-red-500" aria-hidden="true" /> Cancel
          </Button>
        ) : null,
    },
  ]

  return (
    <div>
      <PageHeader title="My leave" subtitle="Request time off and track your balances." />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <Card title="Request leave" className="lg:col-span-1">
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
            <Select
              label="Leave type"
              placeholder="Choose type…"
              error={errors.leaveTypeId?.message}
              options={(leaveTypes.data || []).map((t) => ({
                value: t.id,
                label: `${t.name}${t.isPaid ? '' : ' (unpaid)'}`,
              }))}
              {...register('leaveTypeId')}
            />
            <div className="grid grid-cols-2 gap-3">
              <Input label="Start date" type="date" error={errors.startDate?.message} {...register('startDate')} />
              <Input label="End date" type="date" disabled={isHalfDay} error={errors.endDate?.message} {...register('endDate')} />
            </div>
            <Checkbox label="Half day" description="Single morning/afternoon off." {...register('isHalfDay')} />
            <Textarea label="Reason (optional)" rows={3} error={errors.reason?.message} {...register('reason')} />
            <Button type="submit" className="w-full" loading={isSubmitting}>
              <CalendarPlus className="h-4 w-4" aria-hidden="true" /> Submit request
            </Button>
          </form>
        </Card>

        <div className="space-y-4 lg:col-span-2">
          <Card title={`Balances ${year}`}>
            {balances.isLoading ? (
              <div className="py-6 text-center text-sm text-slate-400">Loading…</div>
            ) : !balances.data?.length ? (
              <EmptyState title="No balances" description="Leave types configured by HR will appear here." />
            ) : (
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                {balances.data.map((b) => (
                  <div key={b.leaveTypeId} className="rounded-lg border border-slate-200 p-3 dark:border-slate-700">
                    <div className="flex items-center justify-between">
                      <span className="flex items-center gap-1.5 text-sm font-medium text-slate-800 dark:text-slate-200">
                        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: b.colorHex || colorFromString(b.typeName) }} aria-hidden="true" />
                        {b.typeName}
                      </span>
                      <span className="text-lg font-bold text-slate-900 dark:text-white">{b.available}</span>
                    </div>
                    <p className="mt-1 text-xs text-slate-400">
                      {b.usedDays} used · {b.pendingDays} pending · {b.entitledDays + b.carriedOverDays + b.accruedDays} entitled
                    </p>
                  </div>
                ))}
              </div>
            )}
          </Card>

          <Card title="My requests" bodyClassName="!p-0">
            <DataTable
              columns={columns}
              rows={requests.data?.items || []}
              loading={requests.isLoading}
              page={requests.data?.page || page}
              pageSize={requests.data?.pageSize || 10}
              totalCount={requests.data?.totalCount || 0}
              totalPages={requests.data?.totalPages || 1}
              onPageChange={setPage}
              empty={<EmptyState icon={CalendarPlus} title="No requests yet" description="Submit your first leave request using the form." />}
            />
          </Card>
        </div>
      </div>

      <ConfirmDialog
        open={!!cancelFor}
        onClose={() => setCancelFor(null)}
        danger
        loading={cancelRequest.isPending}
        title="Cancel this leave request?"
        message={cancelFor ? `${cancelFor.typeName} · ${formatDate(cancelFor.startDate)} → ${formatDate(cancelFor.endDate)}` : ''}
        confirmLabel="Cancel request"
        onConfirm={async () => {
          try {
            await cancelRequest.mutateAsync(cancelFor.id)
            toast.success('Request cancelled')
            setCancelFor(null)
          } catch (e) {
            toast.error(await apiErrorMessage(e))
            setCancelFor(null)
          }
        }}
      />
    </div>
  )
}
