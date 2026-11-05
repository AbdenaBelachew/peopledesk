import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  ChevronLeft, ChevronRight, Calculator, CheckCircle2, Lock, Undo2, Download,
  Plus, AlertTriangle, Send, Users,
} from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import {
  usePayrollRun, usePayrollRunItems, usePayrollAction, useAddAdjustment, usePublishPayslips,
  useEmployees,
} from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage, downloadFile } from '../../api/client'
import { money, formatDate, statusTone } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Textarea from '../../components/ui/Textarea'
import Modal from '../../components/ui/Modal'
import ConfirmDialog from '../../components/ui/ConfirmDialog'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'
import Spinner from '../../components/ui/Spinner'
import StatCard from '../../components/ui/StatCard'

const STEPS = ['Draft', 'Calculated', 'Approved', 'Finalized']

export default function PayrollRunDetail() {
  const { id } = useParams()
  const { hasPerm } = useAuth()
  const toast = useToast()
  const run = usePayrollRun(id)
  const [page, setPage] = useState(1)
  const items = usePayrollRunItems(id, page, 20)
  const action = usePayrollAction()
  const [reverseOpen, setReverseOpen] = useState(false)
  const [adjustOpen, setAdjustOpen] = useState(false)
  const [finalizeOpen, setFinalizeOpen] = useState(false)
  const [exporting, setExporting] = useState('')

  if (run.isLoading) return <div className="py-16 text-center"><Spinner className="mx-auto" /></div>
  if (!run.data) return <EmptyState title="Run not found" />

  const r = run.data
  const status = r.status
  const stepIndex = STEPS.indexOf(status)
  const reversed = status === 'Reversed'

  async function doAction(actionName, body) {
    try {
      await action.mutateAsync({ id, action: actionName, body })
      toast.success(`Run ${actionName}d`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  async function exportRun(format) {
    setExporting(format)
    try {
      await downloadFile(`/reports/payroll/${id}/export`, { format: format === 'xlsx' ? 'true' : 'false' }, `payroll-run-${id}.${format}`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    } finally {
      setExporting('')
    }
  }

  return (
    <div>
      <Link to="/payroll" className="mb-3 inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200">
        <ChevronLeft className="h-4 w-4" aria-hidden="true" /> Back to payroll
      </Link>

      <PageHeader
        title={r.periodName || 'Payroll run'}
        subtitle={`${r.employeeCount} employees · created ${formatDate(r.calculatedAtUtc || undefined)}`}
        actions={
          <>
            {hasPerm('payroll.run') && ['Draft', 'Calculated'].includes(status) && (
              <Button variant="secondary" loading={action.isPending && action.variables?.action === 'calculate'} onClick={() => doAction('calculate')}>
                <Calculator className="h-4 w-4" aria-hidden="true" /> {status === 'Draft' ? 'Calculate' : 'Recalculate'}
              </Button>
            )}
            {hasPerm('payroll.approve') && status === 'Calculated' && (
              <Button loading={action.isPending && action.variables?.action === 'approve'} onClick={() => doAction('approve')}>
                <CheckCircle2 className="h-4 w-4" aria-hidden="true" /> Approve
              </Button>
            )}
            {hasPerm('payroll.finalize') && status === 'Approved' && (
              <Button onClick={() => setFinalizeOpen(true)}>
                <Lock className="h-4 w-4" aria-hidden="true" /> Finalize
              </Button>
            )}
            {hasPerm('payroll.reverse') && status === 'Finalized' && (
              <Button variant="danger" onClick={() => setReverseOpen(true)}>
                <Undo2 className="h-4 w-4" aria-hidden="true" /> Reverse
              </Button>
            )}
            {hasPerm('payslips.publish') && status === 'Finalized' && (
              <PublishButton runId={id} />
            )}
            <Button variant="secondary" loading={exporting === 'csv'} onClick={() => exportRun('csv')}>
              <Download className="h-4 w-4" aria-hidden="true" /> CSV
            </Button>
            <Button variant="secondary" loading={exporting === 'xlsx'} onClick={() => exportRun('xlsx')}>
              <Download className="h-4 w-4" aria-hidden="true" /> XLSX
            </Button>
          </>
        }
      />

      {/* Status stepper */}
      <div className="mb-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        {reversed ? (
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone="red">Reversed</Badge>
            {r.reversalNote && <p className="text-sm text-slate-500 dark:text-slate-400">Note: {r.reversalNote}</p>}
          </div>
        ) : (
          <ol className="flex items-center">
            {STEPS.map((s, i) => (
              <li key={s} className={`flex items-center ${i < STEPS.length - 1 ? 'flex-1' : ''}`}>
                <div className="flex items-center gap-2">
                  <span
                    className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${
                      i < stepIndex
                        ? 'bg-emerald-500 text-white'
                        : i === stepIndex
                          ? 'bg-blue-600 text-white'
                          : 'bg-slate-200 text-slate-500 dark:bg-slate-700 dark:text-slate-400'
                    }`}
                  >
                    {i < stepIndex ? <CheckCircle2 className="h-4 w-4" aria-hidden="true" /> : i + 1}
                  </span>
                  <span className={`text-sm font-medium ${i <= stepIndex ? 'text-slate-900 dark:text-white' : 'text-slate-400'}`}>{s}</span>
                </div>
                {i < STEPS.length - 1 && <div className={`mx-3 h-0.5 flex-1 ${i < stepIndex ? 'bg-emerald-400' : 'bg-slate-200 dark:bg-slate-700'}`} aria-hidden="true" />}
              </li>
            ))}
          </ol>
        )}
      </div>

      {/* Skipped employees warning */}
      {r.skippedEmployees?.length > 0 && ['Draft', 'Calculated'].includes(status) && (
        <div className="mb-4 flex items-start gap-2.5 rounded-lg border border-amber-200 bg-amber-50 p-3 dark:border-amber-900/50 dark:bg-amber-900/20" role="alert">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600 dark:text-amber-400" aria-hidden="true" />
          <div className="text-sm text-amber-800 dark:text-amber-300">
            <p className="font-semibold">{r.skippedEmployees.length} employee(s) were skipped — they have no valid salary structure:</p>
            <p className="mt-0.5">{r.skippedEmployees.map((s) => (typeof s === 'string' ? s : s.employeeName || s.fullName || s.id)).join(', ')}</p>
          </div>
        </div>
      )}

      {/* Totals */}
      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Gross" value={money(r.totalGross)} icon={null} tone="blue" loading={run.isLoading} />
        <StatCard label="Deductions" value={money(r.totalDeductions)} tone="red" loading={run.isLoading} />
        <StatCard label="Net payable" value={money(r.totalNet)} tone="green" loading={run.isLoading} />
        <StatCard label="Employees" value={r.employeeCount} tone="purple" loading={run.isLoading} />
      </div>

      <Card
        title="Payroll items"
        subtitle="Expand a row to see the per-employee earnings and deductions."
        bodyClassName="!p-0"
        actions={hasPerm('payroll.run') && ['Draft', 'Calculated'].includes(status) && (
          <Button size="sm" variant="secondary" onClick={() => setAdjustOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden="true" /> Adjustment
          </Button>
        )}
      >
        <RunItemsTable runId={id} items={items} page={page} setPage={setPage} />
      </Card>

      {/* Finalize confirm */}
      <ConfirmDialog
        open={finalizeOpen}
        onClose={() => setFinalizeOpen(false)}
        title="Finalize this payroll run?"
        message="Payslips will be generated and the period and its attendance records will be locked. Finalized runs can only be corrected by reversal."
        confirmLabel="Finalize"
        loading={action.isPending && action.variables?.action === 'finalize'}
        onConfirm={async () => {
          await doAction('finalize')
          setFinalizeOpen(false)
        }}
      />

      {/* Reverse dialog */}
      <ReverseDialog
        open={reverseOpen}
        onClose={() => setReverseOpen(false)}
        onSubmit={async (note) => {
          await doAction('reverse', { note })
          setReverseOpen(false)
        }}
        loading={action.isPending && action.variables?.action === 'reverse'}
      />

      {/* Adjustments dialog */}
      {['Draft', 'Calculated'].includes(status) && (
        <AdjustmentDialog
          open={adjustOpen}
          onClose={() => setAdjustOpen(false)}
          runId={id}
        />
      )}
    </div>
  )
}

function PublishButton({ runId }) {
  const publish = usePublishPayslips(runId)
  const toast = useToast()
  return (
    <Button
      variant="subtle"
      loading={publish.isPending}
      onClick={async () => {
        try {
          await publish.mutateAsync()
          toast.success('Payslips published to employees')
        } catch (e) {
          toast.error(await apiErrorMessage(e))
        }
      }}
    >
      <Send className="h-4 w-4" aria-hidden="true" /> Publish payslips
    </Button>
  )
}

function RunItemsTable({ runId, items, page, setPage }) {
  const [expanded, setExpanded] = useState(null)

  const columns = [
    {
      key: 'expander',
      header: '',
      className: 'w-8',
      render: (row) => (
        <button
          type="button"
          aria-label={expanded === row.id ? 'Collapse details' : 'Expand details'}
          onClick={() => setExpanded(expanded === row.id ? null : row.id)}
          className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800"
        >
          <ChevronRight className={`h-4 w-4 transition-transform ${expanded === row.id ? 'rotate-90' : ''}`} aria-hidden="true" />
        </button>
      ),
    },
    { key: 'employeeName', header: 'Employee', render: (row) => (
      <span>
        <span className="block font-medium text-slate-900 dark:text-slate-100">{row.employeeName}</span>
        <span className="text-xs text-slate-400">{row.employeeNumber} · {row.currency}</span>
      </span>
    ) },
    { key: 'daysWorked', header: 'Days', className: 'text-right', render: (row) => row.daysWorked },
    { key: 'unpaidLeaveDays', header: 'Unpaid', className: 'text-right', render: (row) => row.unpaidLeaveDays || 0 },
    { key: 'overtimeHours', header: 'OT (h)', className: 'text-right', render: (row) => row.overtimeHours || 0 },
    { key: 'basicSalary', header: 'Basic', className: 'text-right', render: (row) => money(row.basicSalary, row.currency) },
    { key: 'grossSalary', header: 'Gross', className: 'text-right', render: (row) => money(row.grossSalary, row.currency) },
    { key: 'totalDeductions', header: 'Deductions', className: 'text-right', render: (row) => <span className="text-red-600 dark:text-red-400">-{money(row.totalDeductions, row.currency)}</span> },
    { key: 'netSalary', header: 'Net', className: 'text-right', render: (row) => <span className="font-bold text-slate-900 dark:text-white">{money(row.netSalary, row.currency)}</span> },
  ]

  return (
    <>
      <DataTable
        columns={columns}
        rows={items.data?.items || []}
        loading={items.isLoading}
        page={items.data?.page || page}
        pageSize={items.data?.pageSize || 20}
        totalCount={items.data?.totalCount || 0}
        totalPages={items.data?.totalPages || 1}
        onPageChange={setPage}
        empty={<EmptyState icon={Users} title="No items" description="Calculate the run to generate payroll items." />}
      />
      {/* Expanded row detail is rendered under the table for the selected employee */}
      {expanded && (() => {
        const row = (items.data?.items || []).find((x) => x.id === expanded)
        if (!row) return null
        return (
          <div className="border-t border-slate-100 bg-slate-50 p-4 dark:border-slate-800 dark:bg-slate-800/40">
            <div className="mb-2 flex items-center justify-between">
              <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">{row.employeeName} — breakdown</p>
              <Button variant="ghost" size="sm" onClick={() => setExpanded(null)}>Close</Button>
            </div>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <p className="mb-1.5 text-xs font-semibold uppercase text-emerald-600 dark:text-emerald-400">Earnings</p>
                <ul className="space-y-1">
                  {(row.earnings || []).map((e2, i) => (
                    <li key={i} className="flex justify-between text-sm text-slate-600 dark:text-slate-300">
                      <span>{e2.label} <span className="font-mono text-xs text-slate-400">{e2.code}</span></span>
                      <span className="font-medium">{money(e2.amount, row.currency)}</span>
                    </li>
                  ))}
                  {!row.earnings?.length && <li className="text-sm text-slate-400">None</li>}
                </ul>
              </div>
              <div>
                <p className="mb-1.5 text-xs font-semibold uppercase text-red-600 dark:text-red-400">Deductions</p>
                <ul className="space-y-1">
                  {(row.deductions || []).map((d, i) => (
                    <li key={i} className="flex justify-between text-sm text-slate-600 dark:text-slate-300">
                      <span>{d.label} <span className="font-mono text-xs text-slate-400">{d.code}</span></span>
                      <span className="font-medium">-{money(d.amount, row.currency)}</span>
                    </li>
                  ))}
                  {!row.deductions?.length && <li className="text-sm text-slate-400">None</li>}
                </ul>
              </div>
            </div>
          </div>
        )
      })()}
    </>
  )
}

function ReverseDialog({ open, onClose, onSubmit, loading }) {
  const [note, setNote] = useState('')
  const [error, setError] = useState('')

  function submit(e) {
    e.preventDefault()
    if (!note.trim()) {
      setError('A reversal note is required')
      return
    }
    setError('')
    onSubmit(note.trim())
  }

  return (
    <Modal open={open} onClose={onClose} title="Reverse payroll run" size="sm"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button variant="danger" onClick={submit} loading={loading}>Reverse run</Button></>}>
      <form onSubmit={submit} className="space-y-4">
        <p className="text-sm text-slate-600 dark:text-slate-300">
          Reversing regenerates the run as a correction and unlocks the period. Explain why this run is being reversed.
        </p>
        <Textarea label="Reversal note (required)" value={note} onChange={(e) => setNote(e.target.value)} error={error} autoFocus />
      </form>
    </Modal>
  )
}

function AdjustmentDialog({ open, onClose, runId }) {
  const toast = useToast()
  const add = useAddAdjustment(runId)
  const employees = useEmployees({ page: 1, pageSize: 300 })
  const [values, setValues] = useState({ employeeId: '', kind: 'Earning', code: '', label: '', amount: '', note: '' })
  const [errors, setErrors] = useState({})

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  async function submit(e) {
    e.preventDefault()
    const next = {}
    if (!values.employeeId) next.employeeId = 'Select an employee'
    if (!values.code.trim()) next.code = 'Code is required'
    if (!values.label.trim()) next.label = 'Label is required'
    if (!values.amount || Number(values.amount) <= 0) next.amount = 'Amount must be greater than 0'
    setErrors(next)
    if (Object.keys(next).length) return
    try {
      await add.mutateAsync({
        employeeId: values.employeeId,
        kind: values.kind,
        code: values.code.trim().toUpperCase(),
        label: values.label.trim(),
        amount: Number(values.amount),
        note: values.note.trim() || undefined,
      })
      toast.success('Adjustment added')
      setValues({ employeeId: '', kind: 'Earning', code: '', label: '', amount: '', note: '' })
      onClose()
    } catch (err) {
      toast.error(await apiErrorMessage(err))
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Add adjustment"
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={add.isPending}>Add</Button></>}>
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Select
          label="Employee"
          placeholder="Select…"
          className="sm:col-span-2"
          value={values.employeeId}
          onChange={(e) => set('employeeId', e.target.value)}
          options={(employees.data?.items || []).map((x) => ({ value: x.id, label: `${x.fullName} (${x.employeeNumber})` }))}
          error={errors.employeeId}
        />
        <Select
          label="Kind"
          value={values.kind}
          onChange={(e) => set('kind', e.target.value)}
          options={[{ value: 'Earning', label: 'Earning (add to pay)' }, { value: 'Deduction', label: 'Deduction (subtract)' }]}
        />
        <Input label="Code" placeholder="BONUS / LOAN" value={values.code} onChange={(e) => set('code', e.target.value)} error={errors.code} />
        <Input label="Label" placeholder="Performance bonus" value={values.label} onChange={(e) => set('label', e.target.value)} error={errors.label} />
        <Input label="Amount" type="number" step="0.01" min="0" value={values.amount} onChange={(e) => set('amount', e.target.value)} error={errors.amount} />
        <Input label="Note (optional)" value={values.note} onChange={(e) => set('note', e.target.value)} className="sm:col-span-2" />
      </form>
    </Modal>
  )
}
