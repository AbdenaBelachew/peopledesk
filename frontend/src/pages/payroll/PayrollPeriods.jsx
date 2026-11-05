import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Plus, Play, Trash2, FolderOpen, Wallet } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import {
  usePayrollPeriods, useCreatePayrollPeriod, useDeletePayrollPeriod, useCreatePayrollRun,
} from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { money, formatDate, statusTone, MONTHS } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Modal from '../../components/ui/Modal'
import ConfirmDialog from '../../components/ui/ConfirmDialog'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'

export default function PayrollPeriods() {
  const { hasPerm } = useAuth()
  const canRun = hasPerm('payroll.run')
  const navigate = useNavigate()
  const toast = useToast()
  const periods = usePayrollPeriods()
  const createPeriod = useCreatePayrollPeriod()
  const deletePeriod = useDeletePayrollPeriod()
  const createRun = useCreatePayrollRun()
  const [createOpen, setCreateOpen] = useState(false)
  const [deleteFor, setDeleteFor] = useState(null)

  const now = new Date()

  async function runPayroll(period) {
    try {
      const run = await createRun.mutateAsync({ payrollPeriodId: period.id })
      toast.success(`Payroll run started for ${period.name}`)
      navigate(`/payroll/runs/${run.id}`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  const columns = [
    { key: 'name', header: 'Period', render: (r) => <span className="font-medium text-slate-900 dark:text-slate-100">{r.name}</span> },
    { key: 'startDate', header: 'Range', render: (r) => `${formatDate(r.startDate)} → ${formatDate(r.endDate)}` },
    { key: 'payDate', header: 'Pay date', render: (r) => formatDate(r.payDate) },
    { key: 'runStatus', header: 'Run status', render: (r) => (r.runStatus ? <Badge tone={statusTone(r.runStatus)}>{r.runStatus}</Badge> : <span className="text-xs text-slate-400">No run</span>) },
    { key: 'totalNet', header: 'Total net', className: 'text-right', render: (r) => (r.totalNet != null ? <span className="font-semibold">{money(r.totalNet)}</span> : '—') },
    {
      key: 'actions', header: '', className: 'text-right',
      render: (r) => (
        <span className="flex justify-end gap-1.5">
          {r.activeRunId && (
            <Link to={`/payroll/runs/${r.activeRunId}`}>
              <Button size="sm" variant="secondary"><FolderOpen className="h-3.5 w-3.5" aria-hidden="true" /> Open run</Button>
            </Link>
          )}
          {canRun && !r.activeRunId && !r.closed && (
            <Button size="sm" loading={createRun.isPending && createRun.variables?.payrollPeriodId === r.id} onClick={() => runPayroll(r)}>
              <Play className="h-3.5 w-3.5" aria-hidden="true" /> Run payroll
            </Button>
          )}
          {canRun && !r.activeRunId && (
            <Button variant="ghost" size="icon" aria-label={`Delete period ${r.name}`} onClick={() => setDeleteFor(r)}>
              <Trash2 className="h-4 w-4 text-red-500" aria-hidden="true" />
            </Button>
          )}
        </span>
      ),
    },
  ]

  return (
    <div>
      <PageHeader
        title="Payroll"
        subtitle="Monthly payroll periods and runs."
        actions={canRun && <Button onClick={() => setCreateOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" /> New period</Button>}
      />
      <Card bodyClassName="!p-0">
        <DataTable
          columns={columns}
          rows={periods.data || []}
          loading={periods.isLoading}
          empty={
            <EmptyState
              icon={Wallet}
              title="No payroll periods"
              description="Create your first monthly period to start running payroll."
              action={canRun && <Button onClick={() => setCreateOpen(true)}>New period</Button>}
            />
          }
        />
      </Card>

      <CreatePeriodModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        defaultYear={now.getFullYear()}
        defaultMonth={now.getMonth() + 1}
        onCreate={async (values) => {
          try {
            await createPeriod.mutateAsync(values)
            toast.success('Payroll period created')
            setCreateOpen(false)
          } catch (e) {
            toast.error(await apiErrorMessage(e))
          }
        }}
        saving={createPeriod.isPending}
      />

      <ConfirmDialog
        open={!!deleteFor}
        onClose={() => setDeleteFor(null)}
        danger
        loading={deletePeriod.isPending}
        title="Delete payroll period?"
        message={deleteFor ? `${deleteFor.name} can only be deleted when it has no payroll run.` : ''}
        confirmLabel="Delete"
        onConfirm={async () => {
          try {
            await deletePeriod.mutateAsync(deleteFor.id)
            toast.success('Period deleted')
            setDeleteFor(null)
          } catch (e) {
            toast.error(await apiErrorMessage(e))
            setDeleteFor(null)
          }
        }}
      />
    </div>
  )
}

function CreatePeriodModal({ open, onClose, defaultYear, defaultMonth, onCreate, saving }) {
  const [type, setType] = useState('Monthly')
  const [year, setYear] = useState(defaultYear)
  const [month, setMonth] = useState(defaultMonth)
  const [payDate, setPayDate] = useState('')

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="New payroll period"
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button
            onClick={async (e) => {
              e.preventDefault()
              await onCreate({ type, year: Number(year), month: Number(month), payDate: payDate || undefined })
            }}
            loading={saving}
          >
            Create period
          </Button>
        </>
      }
    >
      <form className="space-y-4">
        <Select
          label="Type"
          value={type}
          onChange={(e) => setType(e.target.value)}
          options={[{ value: 'Monthly', label: 'Monthly' }]}
          hint="Semi-monthly and weekly cycles depend on your plan."
        />
        <div className="grid grid-cols-2 gap-3">
          <Select
            label="Year"
            value={year}
            onChange={(e) => setYear(e.target.value)}
            options={Array.from({ length: 7 }, (_, i) => ({ value: defaultYear - 3 + i, label: String(defaultYear - 3 + i) }))}
          />
          <Select
            label="Month"
            value={month}
            onChange={(e) => setMonth(e.target.value)}
            options={MONTHS.map((m, i) => ({ value: i + 1, label: m }))}
          />
        </div>
        <Input label="Pay date (optional)" type="date" value={payDate} onChange={(e) => setPayDate(e.target.value)} />
      </form>
    </Modal>
  )
}
