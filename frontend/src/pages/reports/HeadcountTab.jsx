import { ResponsiveContainer, PieChart, Pie, Cell, Tooltip, Legend } from 'recharts'
import { Users, UserCheck, Hourglass, UserMinus } from 'lucide-react'
import { useHeadcountReport } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage, downloadFile } from '../../api/client'
import Card from '../../components/ui/Card'
import StatCard from '../../components/ui/StatCard'
import Button from '../../components/ui/Button'
import Spinner from '../../components/ui/Spinner'
import EmptyState from '../../components/ui/EmptyState'
import { useState } from 'react'

const COLORS = ['#2563eb', '#0ea5e9', '#8b5cf6', '#10b981', '#f59e0b', '#ef4444', '#14b8a6', '#f472b6', '#84cc16', '#6366f1']

export default function HeadcountTab() {
  const report = useHeadcountReport()
  const toast = useToast()
  const [exporting, setExporting] = useState('')

  const d = report.data

  async function doExport(format) {
    setExporting(format)
    try {
      await downloadFile('/reports/headcount/export', { format: format === 'xlsx' ? 'true' : 'false' }, `headcount.${format}`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    } finally {
      setExporting('')
    }
  }

  return (
    <div>
      <div className="mb-4 flex justify-end gap-2">
        <Button variant="secondary" size="sm" loading={exporting === 'csv'} onClick={() => doExport('csv')}>Export CSV</Button>
        <Button variant="secondary" size="sm" loading={exporting === 'xlsx'} onClick={() => doExport('xlsx')}>Export XLSX</Button>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Total" value={d?.total ?? '—'} icon={Users} loading={report.isLoading} />
        <StatCard label="Active" value={d?.active ?? '—'} icon={UserCheck} tone="green" loading={report.isLoading} />
        <StatCard label="On probation" value={d?.onProbation ?? '—'} icon={Hourglass} tone="amber" loading={report.isLoading} />
        <StatCard label="Exited" value={d?.terminatedOrResigned ?? '—'} icon={UserMinus} tone="red" loading={report.isLoading} />
      </div>

      {report.isLoading ? (
        <div className="py-16 text-center"><Spinner className="mx-auto" /></div>
      ) : !d ? (
        <EmptyState title="No data" />
      ) : (
        <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-3">
          <PieCard title="By department" data={d.byDepartment} />
          <PieCard title="By branch" data={d.byBranch} />
          <PieCard title="By gender" data={d.byGender} />
        </div>
      )}
    </div>
  )
}

function PieCard({ title, data }) {
  const hasData = data && data.length > 0
  return (
    <Card title={title}>
      <div className="h-64">
        {!hasData ? (
          <EmptyState title="No data" description="Nothing to chart yet." className="!py-10" />
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie data={data} dataKey="count" nameKey="label" innerRadius={40} outerRadius={80} paddingAngle={2}>
                {data.map((entry, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
              </Pie>
              <Tooltip />
              <Legend wrapperStyle={{ fontSize: 12 }} />
            </PieChart>
          </ResponsiveContainer>
        )}
      </div>
    </Card>
  )
}
