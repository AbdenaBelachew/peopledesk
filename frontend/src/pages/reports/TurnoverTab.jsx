import { useState } from 'react'
import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip, CartesianGrid } from 'recharts'
import { Users, UserPlus, UserMinus, Percent } from 'lucide-react'
import { useTurnover } from '../../api/queries'
import Card from '../../components/ui/Card'
import StatCard from '../../components/ui/StatCard'
import Select from '../../components/ui/Select'
import Spinner from '../../components/ui/Spinner'
import EmptyState from '../../components/ui/EmptyState'

export default function TurnoverTab() {
  const year = new Date().getFullYear()
  const [selected, setSelected] = useState(year)
  const turnover = useTurnover(selected)
  const d = turnover.data

  return (
    <div>
      <div className="mb-4 flex justify-end">
        <Select
          aria-label="Year"
          value={selected}
          onChange={(e) => setSelected(Number(e.target.value))}
          className="!mt-0 w-32"
          options={Array.from({ length: 6 }, (_, i) => ({ value: year - i, label: String(year - i) }))}
        />
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Starting headcount" value={d?.startingHeadcount ?? '—'} icon={Users} loading={turnover.isLoading} />
        <StatCard label="New hires" value={d?.newHires ?? '—'} icon={UserPlus} tone="green" loading={turnover.isLoading} />
        <StatCard label="Exits" value={d?.exits ?? '—'} icon={UserMinus} tone="red" loading={turnover.isLoading} />
        <StatCard label="Turnover rate" value={d != null ? `${d.turnoverRate ?? 0}%` : '—'} icon={Percent} tone="amber" loading={turnover.isLoading} />
      </div>

      <Card title={`Exits by month — ${selected}`} className="mt-4">
        <div className="h-72">
          {turnover.isLoading ? (
            <div className="flex h-full items-center justify-center"><Spinner /></div>
          ) : !d?.exitsByMonth?.length ? (
            <EmptyState title="No exits recorded" description="Great retention! Exits will chart here over time." />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={d.exitsByMonth} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="currentColor" className="text-slate-200 dark:text-slate-700" />
                <XAxis dataKey="label" tick={{ fontSize: 11 }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                <Tooltip />
                <Bar dataKey="count" name="Exits" fill="#ef4444" radius={[4, 4, 0, 0]} maxBarSize={36} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>
      </Card>
    </div>
  )
}
