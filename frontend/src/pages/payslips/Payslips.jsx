import { useEffect, useState } from 'react'
import { Download, FileText } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { usePayslips, usePayrollPeriods, useEmployees } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage, downloadFile, openBlobUrl } from '../../api/client'
import { money, formatDateTime } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Button from '../../components/ui/Button'
import Select from '../../components/ui/Select'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'

export default function Payslips() {
  const { user, hasPerm } = useAuth()
  const canViewAll = hasPerm('payslips.viewAll')
  const toast = useToast()
  const [page, setPage] = useState(1)
  const [periodId, setPeriodId] = useState('')
  const [employeeId, setEmployeeId] = useState('')
  const [selectedId, setSelectedId] = useState(null)

  const periods = usePayrollPeriods()
  const employees = useEmployees({ page: 1, pageSize: 300 }, { enabled: canViewAll })
  const payslips = usePayslips({
    page,
    pageSize: 10,
    periodId,
    employeeId: canViewAll ? employeeId : undefined,
  })

  const columns = [
    { key: 'payslipNumber', header: 'Payslip', render: (r) => <span className="font-mono text-xs">{r.payslipNumber}</span> },
    { key: 'employeeName', header: 'Employee', render: (r) => (
      <span>
        <span className="block font-medium text-slate-900 dark:text-slate-100">{r.employeeName}</span>
        <span className="text-xs text-slate-400">{r.employeeNumber}</span>
      </span>
    ) },
    { key: 'periodName', header: 'Period' },
    { key: 'grossPay', header: 'Gross', className: 'text-right', render: (r) => money(r.grossPay, r.currency) },
    { key: 'totalDeductions', header: 'Deductions', className: 'text-right', render: (r) => <span className="text-red-600 dark:text-red-400">-{money(r.totalDeductions, r.currency)}</span> },
    { key: 'netPay', header: 'Net', className: 'text-right', render: (r) => <span className="font-bold text-slate-900 dark:text-white">{money(r.netPay, r.currency)}</span> },
    { key: 'publishedAtUtc', header: 'Issued', render: (r) => formatDateTime(r.publishedAtUtc || r.issuedAtUtc) },
    {
      key: 'actions', header: '', className: 'text-right',
      render: (r) => (
        <span className="flex justify-end gap-1.5">
          <Button size="sm" variant={selectedId === r.id ? 'primary' : 'secondary'} onClick={() => setSelectedId(r.id)}>
            View
          </Button>
          <DownloadButton payslip={r} />
        </span>
      ),
    },
  ]

  return (
    <div>
      <PageHeader title="Payslips" subtitle={canViewAll ? 'All employee payslips.' : 'Your payslips.'} />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <Select
          aria-label="Filter by period"
          placeholder="All periods"
          value={periodId}
          onChange={(e) => { setPeriodId(e.target.value); setPage(1) }}
          options={(periods.data || []).map((p) => ({ value: p.id, label: p.name }))}
        />
        {canViewAll && (
          <Select
            aria-label="Filter by employee"
            placeholder="All employees"
            value={employeeId}
            onChange={(e) => { setEmployeeId(e.target.value); setPage(1) }}
            options={(employees.data?.items || []).map((x) => ({ value: x.id, label: `${x.fullName} (${x.employeeNumber})` }))}
          />
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Card bodyClassName="!p-0" className="self-start">
          <DataTable
            columns={columns}
            rows={payslips.data?.items || []}
            loading={payslips.isLoading}
            page={payslips.data?.page || page}
            pageSize={payslips.data?.pageSize || 10}
            totalCount={payslips.data?.totalCount || 0}
            totalPages={payslips.data?.totalPages || 1}
            onPageChange={setPage}
            empty={<EmptyState icon={FileText} title="No payslips" description="Payslips appear after a payroll run is finalized." />}
          />
        </Card>

        <PdfViewer payslipId={selectedId} onClose={() => setSelectedId(null)} />
      </div>
    </div>
  )
}

function DownloadButton({ payslip }) {
  const toast = useToast()
  const [loading, setLoading] = useState(false)
  return (
    <Button
      size="sm"
      variant="ghost"
      aria-label={`Download payslip ${payslip.payslipNumber}`}
      loading={loading}
      onClick={async () => {
        setLoading(true)
        try {
          await downloadFile(`/payslips/${payslip.id}/download`, undefined, `${payslip.payslipNumber || 'payslip'}.pdf`)
        } catch (e) {
          toast.error(await apiErrorMessage(e))
        } finally {
          setLoading(false)
        }
      }}
    >
      <Download className="h-4 w-4" aria-hidden="true" />
    </Button>
  )
}

/** Inline PDF preview: authenticated fetch -> blob -> object URL -> iframe. */
function PdfViewer({ payslipId, onClose }) {
  const toast = useToast()
  const [url, setUrl] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!payslipId) {
      setUrl(null)
      return undefined
    }
    let cancelled = false
    let objectUrl = null
    ;(async () => {
      setLoading(true)
      setError('')
      try {
        objectUrl = await openBlobUrl(`/payslips/${payslipId}/download`)
        if (!cancelled) setUrl(objectUrl)
      } catch (e) {
        if (!cancelled) {
          setError(await apiErrorMessage(e))
          toast.error('Could not load payslip preview')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
      if (objectUrl) setTimeout(() => URL.revokeObjectURL(objectUrl), 1000)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [payslipId])

  if (!payslipId) {
    return (
      <Card className="hidden min-h-80 place-items-center xl:grid">
        <EmptyState icon={FileText} title="Select a payslip" description="Choose a payslip from the list to preview it here." />
      </Card>
    )
  }

  return (
    <Card bodyClassName="!p-0" className="overflow-hidden">
      <div className="flex items-center justify-between border-b border-slate-100 px-4 py-2.5 dark:border-slate-800">
        <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">Preview</p>
        <Button variant="ghost" size="sm" onClick={onClose}>Close</Button>
      </div>
      {loading ? (
        <div className="flex h-96 items-center justify-center text-sm text-slate-400">Loading preview…</div>
      ) : error ? (
        <div className="flex h-96 items-center justify-center px-6 text-center text-sm text-red-500">{error}</div>
      ) : url ? (
        <iframe
          title="Payslip PDF preview"
          src={url}
          className="h-[600px] w-full border-0 bg-slate-100 dark:bg-slate-950"
        />
      ) : null}
    </Card>
  )
}
