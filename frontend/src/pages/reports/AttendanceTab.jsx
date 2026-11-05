import { useState } from 'react'
import {
  ResponsiveContainer, ComposedChart, Bar, Line, XAxis, YAxis, Tooltip, CartesianGrid, Legend,
} from 'recharts'
import { chartTooltipProps, chartTick } from '../../lib/chartTheme'
import { dateInput } from '../../lib/format'
import { useAttendanceTrends } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage, downloadFile } from '../../api/client'
import Card from '../../components/ui/Card'
import StatCard from '../../components/ui/StatCard'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Spinner from '../../components/ui/Spinner'
import EmptyState from '../../components/ui/EmptyState'
import { duration } from '../../lib/format'

export default function AttendanceTab() {
  const today = new Date()
  const ago = new Date(today.getTime() - 29 * 86400_000)
  const [from, setFrom] = useState(dateInput(ago.toISOString()))
  const [to, setTo] = useState(dateInput(today.toISOString()))
  const trends = useAttendanceTrends(from, to)
  const toast = useToast()
  const [exporting, setExporting] = useState('')

  const d = trends.data
  const points = (d?.points || []).map((p) => ({
    ...p,
    label: String(p.date).slice(5), // MM-DD
  }))

  async function doExport(format) {
    setExporting(format)
    try {
      await downloadFile('/reports/attendance/export', { from, to, format: format === 'xlsx' ? 'true' : 'false' }, `attendance-${from}-${to}.${format}`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    } finally {
      setExporting('')
    }
  }

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <Input label="From" type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-44" />
          <Input label="To" type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-44" />
        </div>
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" loading={exporting === 'csv'} onClick={() => doExport('csv')}>Export CSV</Button>
          <Button variant="secondary" size="sm" loading={exporting === 'xlsx'} onClick={() => doExport('xlsx')}>Export XLSX</Button>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Avg late / day" value={d ? `${d.avgLateMinutesPerDay ?? 0}m` : '—'} loading={trends.isLoading} />
        <StatCard label="Total overtime" value={d ? duration(d.totalOvertimeMinutes) : '—'} loading={trends.isLoading} />
        <StatCard label="Range" value={d ? `${from} → ${to}` : '—'} loading={trends.isLoading} />
        <StatCard label="Days tracked" value={points.length} loading={trends.isLoading} />
      </div>

      <Card title="Daily attendance" className="mt-4">
        <div className="h-80">
          {trends.isLoading ? (
            <div className="flex h-full items-center justify-center"><Spinner /></div>
          ) : points.length === 0 ? (
            <EmptyState title="No attendance in this range" description="Try widening the date range." />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <ComposedChart data={points} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="currentColor" className="text-slate-200 dark:text-slate-700" />
                <XAxis dataKey="label" tick={chartTick} interval="preserveStartEnd" />
                <YAxis allowDecimals={false} tick={chartTick} />
                <Tooltip {...chartTooltipProps} />
                <Legend />
                <Bar dataKey="present" name="Present" stackId="a" fill="#2563eb" />
                <Bar dataKey="late" name="Late" stackId="a" fill="#f59e0b" />
                <Bar dataKey="onLeave" name="On leave" stackId="a" fill="#8b5cf6" />
                <Bar dataKey="absent" name="Absent" stackId="a" fill="#ef4444" radius={[3, 3, 0, 0]} />
                <Line type="monotone" dataKey="overtimeMinutes" name="Overtime (min)" stroke="#10b981" strokeWidth={2} dot={false} yAxisId={0} />
              </ComposedChart>
            </ResponsiveContainer>
          )}
        </div>
      </Card>
    </div>
  )
}
