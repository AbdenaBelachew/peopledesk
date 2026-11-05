import { useState } from 'react'
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip, CartesianGrid, Legend } from 'recharts'
import { useLeaveUtilization } from '../../api/queries'
import { colorFromString } from '../../lib/format'
import Card from '../../components/ui/Card'
import Select from '../../components/ui/Select'
import Spinner from '../../components/ui/Spinner'
import EmptyState from '../../components/ui/EmptyState'

export default function LeaveTab() {
  const year = new Date().getFullYear()
  const [selected, setSelected] = useState(year)
  const report = useLeaveUtilization(selected)

  const data = (report.data?.byType || []).map((t) => ({
    ...t,
    label: t.typeName,
    Entitled: t.entitled,
    Used: t.used,
    Pending: t.pending,
  }))

  return (
    <div>
      <div className="mb-4 flex justify-end">
        <Select
          aria-label="Year"
          value={selected}
          onChange={(e) => setSelected(Number(e.target.value))}
          className="!mt-0 w-32"
          options={Array.from({ length: 5 }, (_, i) => ({ value: year - i, label: String(year - i) }))}
        />
      </div>

      <Card title={`Leave utilization ${selected}`}>
        <div className="h-80">
          {report.isLoading ? (
            <div className="flex h-full items-center justify-center"><Spinner /></div>
          ) : data.length === 0 ? (
            <EmptyState title="No leave data" description="Utilization appears once balances and requests exist." />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={data} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="currentColor" className="text-slate-200 dark:text-slate-700" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                <Tooltip />
                <Legend />
                <Bar dataKey="Entitled" fill="#94a3b8" radius={[3, 3, 0, 0]} maxBarSize={48} />
                <Bar dataKey="Used" fill="#2563eb" radius={[3, 3, 0, 0]} maxBarSize={48} />
                <Bar dataKey="Pending" fill="#f59e0b" radius={[3, 3, 0, 0]} maxBarSize={48} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
        {data.length > 0 && (
          <div className="mt-4 overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="bg-slate-50 text-left text-xs uppercase text-slate-400 dark:bg-slate-800/60">
                  <th className="px-3 py-2">Type</th>
                  <th className="px-3 py-2 text-right">Entitled</th>
                  <th className="px-3 py-2 text-right">Used</th>
                  <th className="px-3 py-2 text-right">Pending</th>
                  <th className="px-3 py-2 text-right">Utilization</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {report.data.byType.map((t) => (
                  <tr key={t.typeCode || t.typeName}>
                    <td className="px-3 py-2">
                      <span className="inline-flex items-center gap-1.5 font-medium text-slate-900 dark:text-slate-100">
                        <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: t.colorHex || colorFromString(t.typeName) }} aria-hidden="true" />
                        {t.typeName}
                      </span>
                    </td>
                    <td className="px-3 py-2 text-right">{t.entitled}</td>
                    <td className="px-3 py-2 text-right">{t.used}</td>
                    <td className="px-3 py-2 text-right">{t.pending}</td>
                    <td className="px-3 py-2 text-right font-medium">
                      {t.entitled ? `${Math.round((t.used / t.entitled) * 100)}%` : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  )
}
