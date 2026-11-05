import { useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import {
  Users, Building2, CalendarClock, ClipboardCheck, Clock, LogOut, ArrowRight,
  Megaphone, Wallet, CalendarDays, Bell,
} from 'lucide-react'
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, Tooltip, CartesianGrid, LineChart, Line, Legend,
} from 'recharts'
import { useAuth } from '../auth/AuthContext'
import {
  useOrgDashboard, useHeadcountReport, usePayrollExpenses, useLeaveRequests,
  useAnnouncements, useAttendanceToday, useClockIn, useClockOut, useLeaveBalances,
  usePayslips, useNotifications, useReviewLeaveRequest,
} from '../api/queries'
import { useToast } from '../components/ui/Toast'
import { apiErrorMessage } from '../api/client'
import { money, formatDate, formatTime, duration, statusTone } from '../lib/format'
import Card from '../components/ui/Card'
import StatCard from '../components/ui/StatCard'
import Badge from '../components/ui/Badge'
import Button from '../components/ui/Button'
import DataTable from '../components/ui/DataTable'
import EmptyState from '../components/ui/EmptyState'
import Spinner from '../components/ui/Spinner'
import Avatar from '../components/ui/Avatar'
import { useNow } from '../lib/hooks'

export default function Dashboard() {
  const { user, hasAnyPerm } = useAuth()

  // Platform admins without a tenant land in the console.
  if (user?.isPlatformAdmin && !user?.organizationId) return <Navigate to="/platform" replace />

  const isAdminView = hasAnyPerm(['employees.view', 'reports.view', 'leave.viewAll', 'payroll.view'])
  return isAdminView ? <AdminDashboard /> : <SelfServiceDashboard />
}

// ============================== Admin dashboard ==============================

function AdminDashboard() {
  const dashboard = useOrgDashboard()
  const headcount = useHeadcountReport()
  const expenses = usePayrollExpenses()
  const pendingLeaves = useLeaveRequests({ status: 'Pending', page: 1, pageSize: 5 })
  const announcements = useAnnouncements(false)

  const d = dashboard.data
  const deptData = (headcount.data?.byDepartment || []).slice(0, 8)
  const expenseData = (expenses.data?.periods || []).map((p) => ({
    period: p.period,
    Gross: p.gross,
    Deductions: p.deductions,
    Net: p.net,
  }))

  return (
    <div>
      <div className="mb-5">
        <h1 className="text-xl font-bold tracking-tight text-slate-900 dark:text-slate-100">
          Good day{d ? `, ${d.totalEmployees} people strong` : ''}
        </h1>
        <p className="mt-0.5 text-sm text-slate-500 dark:text-slate-400">Organization overview and pending work.</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Total employees" value={d?.totalEmployees ?? '—'} hint={`${d?.activeEmployees ?? 0} active`} icon={Users} tone="blue" loading={dashboard.isLoading} />
        <StatCard label="Departments" value={d?.departments ?? '—'} hint={`${d?.branches ?? 0} branches`} icon={Building2} tone="purple" loading={dashboard.isLoading} />
        <StatCard label="Pending leave requests" value={d?.pendingLeaveRequests ?? '—'} icon={CalendarClock} tone="amber" loading={dashboard.isLoading} />
        <StatCard label="Pending corrections" value={d?.pendingCorrections ?? '—'} hint={`${d?.pendingProfileChanges ?? 0} profile changes`} icon={ClipboardCheck} tone="green" loading={dashboard.isLoading} />
      </div>

      <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
        <Card title="Headcount by department">
          <div className="h-64">
            {headcount.isLoading ? (
              <div className="flex h-full items-center justify-center"><Spinner /></div>
            ) : deptData.length === 0 ? (
              <EmptyState icon={Building2} title="No departments yet" description="Create departments to see the distribution here." />
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={deptData} margin={{ top: 8, right: 8, left: -16, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="currentColor" className="text-slate-200 dark:text-slate-700" />
                  <XAxis dataKey="label" tick={{ fontSize: 11 }} interval={0} angle={-20} textAnchor="end" height={50} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 11 }} />
                  <Tooltip />
                  <Bar dataKey="count" name="Employees" fill="#2563eb" radius={[4, 4, 0, 0]} maxBarSize={40} />
                </BarChart>
              </ResponsiveContainer>
            )}
          </div>
        </Card>

        <Card title="Payroll expenses by period">
          <div className="h-64">
            {expenses.isLoading ? (
              <div className="flex h-full items-center justify-center"><Spinner /></div>
            ) : expenseData.length === 0 ? (
              <EmptyState icon={Wallet} title="No payroll runs yet" description="Run your first payroll to see expense trends." />
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={expenseData} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="currentColor" className="text-slate-200 dark:text-slate-700" />
                  <XAxis dataKey="period" tick={{ fontSize: 11 }} />
                  <YAxis tick={{ fontSize: 11 }} width={56} />
                  <Tooltip formatter={(v) => Number(v).toLocaleString()} />
                  <Legend />
                  <Line type="monotone" dataKey="Gross" stroke="#2563eb" strokeWidth={2} dot={false} />
                  <Line type="monotone" dataKey="Deductions" stroke="#f59e0b" strokeWidth={2} dot={false} />
                  <Line type="monotone" dataKey="Net" stroke="#10b981" strokeWidth={2} dot={false} />
                </LineChart>
              </ResponsiveContainer>
            )}
          </div>
        </Card>
      </div>

      <div className="mt-4 grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <RecentLeaveRequests requests={pendingLeaves.data?.items} loading={pendingLeaves.isLoading} />
        </div>
        <Card
          title="Announcements"
          actions={announcements.data?.length ? <Megaphone className="h-4 w-4 text-slate-400" aria-hidden="true" /> : null}
        >
          {announcements.isLoading ? (
            <div className="py-6 text-center"><Spinner className="mx-auto" /></div>
          ) : !announcements.data?.length ? (
            <EmptyState icon={Megaphone} title="No announcements" description="Company-wide announcements will appear here." />
          ) : (
            <ul className="space-y-3">
              {announcements.data.slice(0, 4).map((a) => (
                <li key={a.id} className="rounded-md bg-slate-50 p-3 dark:bg-slate-800/60">
                  <p className="text-sm font-medium text-slate-900 dark:text-slate-100">{a.title}</p>
                  <p className="mt-0.5 line-clamp-2 text-xs text-slate-500 dark:text-slate-400">{a.body}</p>
                  <p className="mt-1 text-[11px] text-slate-400">{formatDate(a.publishedAtUtc)}</p>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  )
}

function RecentLeaveRequests({ requests, loading }) {
  const { hasPerm } = useAuth()
  const toast = useToast()
  const review = useReviewLeaveRequest()
  const canReview = hasPerm('leave.approve')

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
        {r.typeName}
      </span>
    ) },
    { key: 'days', header: 'Dates', render: (r) => `${formatDate(r.startDate)} → ${formatDate(r.endDate)} (${r.days}d)` },
    { key: 'actions', header: '', className: 'text-right', render: (r) => (
      canReview ? (
        <span className="flex justify-end gap-1.5">
          <Button size="sm" loading={review.isPending && review.variables?.id === r.id && review.variables?.approve}
            onClick={async (e) => {
              e.stopPropagation()
              try { await review.mutateAsync({ id: r.id, action: 'approve', approve: true }); toast.success('Leave approved') }
              catch (err) { toast.error(await apiErrorMessage(err)) }
            }}>Approve</Button>
          <Button size="sm" variant="secondary"
            onClick={async (e) => {
              e.stopPropagation()
              try { await review.mutateAsync({ id: r.id, action: 'reject', approve: false }); toast.info('Leave rejected') }
              catch (err) { toast.error(await apiErrorMessage(err)) }
            }}>Reject</Button>
        </span>
      ) : <Badge tone={statusTone(r.status)}>{r.status}</Badge>
    ) },
  ]

  return (
    <Card title="Pending leave requests" bodyClassName="!p-0" actions={<Link to="/leave/approvals" className="text-xs font-medium text-blue-600 hover:underline dark:text-blue-400">View all</Link>}>
      <DataTable
        columns={columns}
        rows={requests || []}
        loading={loading}
        empty={<EmptyState icon={CalendarDays} title="Nothing pending" description="No leave requests are waiting for review." className="!py-8" />}
      />
    </Card>
  )
}

// ============================ Self-service dashboard ============================

function SelfServiceDashboard() {
  const { user } = useAuth()
  const today = useAttendanceToday()
  const balances = useLeaveBalances(user?.employeeId)
  const payslips = usePayslips({ page: 1, pageSize: 5 })
  const notifications = useNotifications({ page: 1, pageSize: 5 })
  const announcements = useAnnouncements(false)
  const clockIn = useClockIn()
  const clockOut = useClockOut()
  const toast = useToast()
  const now = useNow()

  const record = today.data

  async function doClockIn() {
    try {
      const res = await clockIn.mutateAsync()
      toast.success(res?.lateMinutes > 0 ? `Clocked in — ${res.lateMinutes}m late` : 'Clocked in. Have a great day!')
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  async function doClockOut() {
    try {
      const res = await clockOut.mutateAsync()
      toast.success(res?.workedMinutes ? `Clocked out — worked ${duration(res.workedMinutes)}` : 'Clocked out')
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  return (
    <div>
      <div className="mb-5">
        <h1 className="text-xl font-bold tracking-tight text-slate-900 dark:text-slate-100">My workspace</h1>
        <p className="mt-0.5 text-sm text-slate-500 dark:text-slate-400">Your attendance, leave and payslips at a glance.</p>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {/* Clock widget */}
        <Card title="Attendance today" className="lg:row-span-2">
          <div className="flex flex-col items-center py-4 text-center">
            <p className="text-3xl font-bold tabular-nums text-slate-900 dark:text-white">
              {now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}
            </p>
            <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
              {now.toLocaleDateString([], { weekday: 'long', month: 'long', day: 'numeric' })}
            </p>

            <div className="mt-5 w-full">
              {!record ? (
                <>
                  <p className="text-sm text-slate-500 dark:text-slate-400">You have not clocked in yet.</p>
                  <Button size="lg" className="mt-3 w-full" loading={clockIn.isPending} onClick={doClockIn}>
                    <Clock className="h-4 w-4" aria-hidden="true" /> Clock in
                  </Button>
                </>
              ) : !record.clockOutUtc ? (
                <>
                  <p className="text-sm text-slate-500 dark:text-slate-400">
                    Clocked in at <span className="font-medium text-slate-900 dark:text-slate-100">{formatTime(record.clockInUtc)}</span>
                    {record.lateMinutes > 0 && <span className="ml-1 text-amber-600 dark:text-amber-400">({record.lateMinutes}m late)</span>}
                  </p>
                  <Button size="lg" variant="danger" className="mt-3 w-full" loading={clockOut.isPending} onClick={doClockOut}>
                    <LogOut className="h-4 w-4" aria-hidden="true" /> Clock out
                  </Button>
                </>
              ) : (
                <>
                  <p className="text-sm text-slate-500 dark:text-slate-400">
                    {formatTime(record.clockInUtc)} → {formatTime(record.clockOutUtc)}
                  </p>
                  <p className="mt-2 text-2xl font-bold text-emerald-600 dark:text-emerald-400">{duration(record.workedMinutes)}</p>
                  <p className="text-xs text-slate-400">worked today — see you tomorrow</p>
                </>
              )}
            </div>

            {record && (
              <div className="mt-5 grid w-full grid-cols-2 gap-2 text-center">
                <div className="rounded-md bg-slate-50 p-2.5 dark:bg-slate-800/60">
                  <p className="text-[11px] uppercase text-slate-400">Late</p>
                  <p className={`text-sm font-semibold ${record.lateMinutes > 0 ? 'text-amber-600 dark:text-amber-400' : 'text-slate-700 dark:text-slate-200'}`}>{record.lateMinutes || 0}m</p>
                </div>
                <div className="rounded-md bg-slate-50 p-2.5 dark:bg-slate-800/60">
                  <p className="text-[11px] uppercase text-slate-400">Overtime</p>
                  <p className="text-sm font-semibold text-slate-700 dark:text-slate-200">{record.overtimeMinutes || 0}m</p>
                </div>
              </div>
            )}
          </div>
        </Card>

        {/* Leave balances */}
        <Card title="My leave balances" className="lg:col-span-2">
          {balances.isLoading ? (
            <div className="py-6 text-center"><Spinner className="mx-auto" /></div>
          ) : !balances.data?.length ? (
            <EmptyState icon={CalendarDays} title="No leave balances" description="Balances appear once your organization sets up leave types." />
          ) : (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {balances.data.map((b) => (
                <div key={b.leaveTypeId} className="rounded-lg border border-slate-200 p-3 dark:border-slate-700">
                  <div className="flex items-center justify-between">
                    <span className="flex items-center gap-1.5 text-sm font-medium text-slate-800 dark:text-slate-200">
                      <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: b.colorHex || '#2563eb' }} aria-hidden="true" />
                      {b.typeName}
                    </span>
                    <span className="text-lg font-bold text-slate-900 dark:text-white">{b.available}</span>
                  </div>
                  <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
                    <div
                      className="h-full rounded-full bg-blue-500"
                      style={{ width: `${Math.min(100, b.entitledDays ? ((b.usedDays + b.pendingDays) / (b.entitledDays + b.carriedOverDays)) * 100 : 0)}%` }}
                      aria-hidden="true"
                    />
                  </div>
                  <p className="mt-1.5 text-xs text-slate-400">
                    {b.usedDays} used · {b.pendingDays} pending · {b.entitledDays + b.carriedOverDays} entitled
                  </p>
                </div>
              ))}
            </div>
          )}
          <Link to="/leave/my" className="mt-3 inline-flex items-center gap-1 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400">
            Request leave <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
          </Link>
        </Card>

        {/* Latest payslips */}
        <Card title="My payslips" className="lg:col-span-2" bodyClassName="!p-0">
          <DataTable
            columns={[
              { key: 'periodName', header: 'Period' },
              { key: 'netPay', header: 'Net pay', render: (r) => <span className="font-semibold">{money(r.netPay, r.currency)}</span> },
              { key: 'grossPay', header: 'Gross', render: (r) => money(r.grossPay, r.currency) },
              { key: 'publishedAtUtc', header: 'Published', render: (r) => formatDate(r.publishedAtUtc || r.issuedAtUtc) },
            ]}
            rows={payslips.data?.items || []}
            loading={payslips.isLoading}
            empty={<EmptyState icon={Wallet} title="No payslips yet" description="Payslips appear here after your first payroll run." className="!py-8" />}
          />
          {(payslips.data?.totalCount || 0) > 5 && (
            <div className="border-t border-slate-100 px-4 py-2 dark:border-slate-800">
              <Link to="/payslips" className="text-xs font-medium text-blue-600 hover:underline dark:text-blue-400">View all payslips</Link>
            </div>
          )}
        </Card>

        {/* Notifications + announcements */}
        <div className="space-y-4">
          <Card title="Notifications" bodyClassName="!p-0">
            {notifications.isLoading ? (
              <div className="py-6 text-center"><Spinner className="mx-auto" /></div>
            ) : !notifications.data?.items?.length ? (
              <EmptyState icon={Bell} title="All caught up" className="!py-8" />
            ) : (
              <ul className="divide-y divide-slate-100 dark:divide-slate-800">
                {notifications.data.items.map((n) => (
                  <li key={n.id} className="px-4 py-2.5">
                    <p className={`text-sm ${!n.readAtUtc ? 'font-semibold text-slate-900 dark:text-white' : 'text-slate-600 dark:text-slate-300'}`}>{n.title}</p>
                    <p className="mt-0.5 line-clamp-1 text-xs text-slate-400">{n.body}</p>
                    <p className="mt-0.5 text-[11px] text-slate-400">{formatDate(n.createdAtUtc)}</p>
                  </li>
                ))}
              </ul>
            )}
          </Card>
          <Card title="Announcements">
            {announcements.isLoading ? (
              <div className="py-6 text-center"><Spinner className="mx-auto" /></div>
            ) : !announcements.data?.length ? (
              <EmptyState icon={Megaphone} title="No announcements" className="!py-8" />
            ) : (
              <ul className="space-y-3">
                {announcements.data.slice(0, 3).map((a) => (
                  <li key={a.id} className="rounded-md bg-slate-50 p-3 dark:bg-slate-800/60">
                    <p className="text-sm font-medium text-slate-900 dark:text-slate-100">{a.title}</p>
                    <p className="mt-0.5 line-clamp-2 text-xs text-slate-500 dark:text-slate-400">{a.body}</p>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </div>
      </div>
    </div>
  )
}
