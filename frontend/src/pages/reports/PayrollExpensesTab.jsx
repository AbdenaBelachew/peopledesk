import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, Tooltip, CartesianGrid, Legend } from 'recharts'
import { Download } from 'lucide-react'
import { usePayrollExpenses, usePayrollPeriods } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage, downloadFile } from '../../api/client'
import { money } from '../../lib/format'
import Card from '../../components/ui/Card'
import StatCard from '../../components/ui/StatCard'
import Button from '../../components/ui/Button'
import Spinner from '../../components/ui/Spinner'
import EmptyState from '../../components/ui/EmptyState'

export default function PayrollExpensesTab() {
  const expenses = usePayrollExpenses()
  const periods = usePayrollPeriods()
  const toast = useToast()
  const [exporting, setExporting] = useState('')

  // Match report rows to periods so we can reuse the per-run export endpoint.
  const runIdByName = useMemo(
    () => new Map((periods.data || []).map((p) => [p.name, p.activeRunId])),
    [periods.data],
  )

  const rows = expenses.data?.periods || []
  const data = rows.map((p) => ({ ...p, Gross: p.gross, Deductions: p.deductions, Net: p.net }))

  async function exportRun(runId, format) {
    setExporting(`${runId}-${format}`)
    try {
      await downloadFile(`/reports/payroll/${runId}/export`, { format: format === 'xlsx' ? 'true' : 'false' }, `payroll-${runId}.${format}`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    } finally {
      setExporting('')
    }
  }

  return (
    <div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Total gross" value={expenses.data ? money(expenses.data.totalGross) : '—'} loading={expenses.isLoading} />
        <StatCard label="Total net paid" value={expenses.data ? money(expenses.data.totalNet) : '—'} loading={expenses.isLoading} tone="green" />
        <StatCard label="Overtime cost" value={expenses.data ? money(expenses.data.totalOvertimeCost) : '—'} loading={expenses.isLoading} tone="amber" />
      </div>

      <Card title="Expense trend" className="mt-4">
        <div className="h-72">
          {expenses.isLoading ? (
            <div className="flex h-full items-center justify-center"><Spinner /></div>
          ) : data.length === 0 ? (
            <EmptyState title="No payroll yet" description="Run payroll to see expense trends." />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="currentColor" className="text-slate-200 dark:text-slate-700" />
                <XAxis dataKey="period" tick={{ fontSize: 11 }} />
                <YAxis tick={{ fontSize: 11 }} width={64} />
                <Tooltip formatter={(v) => Number(v).toLocaleString()} />
                <Legend />
                <Line type="monotone" dataKey="Gross" stroke="#2563eb" strokeWidth={2} />
                <Line type="monotone" dataKey="Deductions" stroke="#f59e0b" strokeWidth={2} />
                <Line type="monotone" dataKey="Net" stroke="#10b981" strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>
      </Card>

      <Card title="By period" className="mt-4" bodyClassName="!p-0">
        {rows.length === 0 ? (
          <EmptyState title="No data" />
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left text-xs uppercase text-slate-400 dark:bg-slate-800/60">
                  <th className="px-3 py-2">Period</th>
                  <th className="px-3 py-2 text-right">Gross</th>
                  <th className="px-3 py-2 text-right">Deductions</th>
                  <th className="px-3 py-2 text-right">Net</th>
                  <th className="px-3 py-2 text-right">Employees</th>
                  <th className="px-3 py-2 text-right">Export</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {rows.map((p) => {
                  const runId = runIdByName.get(p.period)
                  return (
                    <tr key={p.period} className="hover:bg-slate-50 dark:hover:bg-slate-800/60">
                      <td className="px-3 py-2 font-medium text-slate-900 dark:text-slate-100">{p.period}</td>
                      <td className="px-3 py-2 text-right">{money(p.gross)}</td>
                      <td className="px-3 py-2 text-right text-red-600 dark:text-red-400">-{money(p.deductions)}</td>
                      <td className="px-3 py-2 text-right font-semibold">{money(p.net)}</td>
                      <td className="px-3 py-2 text-right">{p.employees}</td>
                      <td className="px-3 py-2 text-right">
                        {runId ? (
                          <span className="inline-flex gap-1.5">
                            <Button variant="ghost" size="sm" loading={exporting === `${runId}-csv`} onClick={() => exportRun(runId, 'csv')} aria-label={`Export ${p.period} CSV`}>
                              <Download className="h-3.5 w-3.5" aria-hidden="true" /> CSV
                            </Button>
                            <Button variant="ghost" size="sm" loading={exporting === `${runId}-xlsx`} onClick={() => exportRun(runId, 'xlsx')} aria-label={`Export ${p.period} XLSX`}>
                              XLSX
                            </Button>
                          </span>
                        ) : (
                          <Link to="/payroll" className="text-xs text-slate-400 hover:text-blue-500">no run</Link>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  )
}
