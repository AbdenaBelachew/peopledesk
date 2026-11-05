import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Info, UsersRound, Pencil, ShieldCheck } from 'lucide-react'
import { useEmployees } from '../../api/queries'
import { useDebounce } from '../../lib/hooks'
import { formatDate, statusTone } from '../../lib/format'
import PageHeader from '../../components/ui/PageHeader'
import Card from '../../components/ui/Card'
import Badge from '../../components/ui/Badge'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import DataTable from '../../components/ui/DataTable'
import EmptyState from '../../components/ui/EmptyState'

/**
 * User account administration. The v1 API does not expose a dedicated users
 * endpoint yet, so accounts are managed through employee records (which flag
 * hasUserAccount). Password resets use the public forgot-password flow.
 */
export default function Users() {
  const [search, setSearch] = useState('')
  const [onlyWithAccount, setOnlyWithAccount] = useState(false)
  const [page, setPage] = useState(1)
  const debounced = useDebounce(search)

  const employees = useEmployees({ page, pageSize: 15, search: debounced })

  const rows = onlyWithAccount
    ? (employees.data?.items || []).filter((e) => e.hasUserAccount)
    : employees.data?.items || []

  const columns = [
    { key: 'fullName', header: 'Employee', render: (r) => (
      <span>
        <span className="block font-medium text-slate-900 dark:text-slate-100">{r.fullName}</span>
        <span className="text-xs text-slate-400">{r.workEmail || 'no email'}</span>
      </span>
    ) },
    { key: 'employeeNumber', header: 'Number' },
    { key: 'department', header: 'Department', render: (r) => r.department || '—' },
    { key: 'status', header: 'Status', render: (r) => <Badge tone={statusTone(r.status)}>{r.status}</Badge> },
    { key: 'hasUserAccount', header: 'Account', render: (r) => (
      r.hasUserAccount ? <Badge tone="purple">Active</Badge> : <span className="text-xs text-slate-400">None</span>
    ) },
    { key: 'hireDate', header: 'Hired', render: (r) => formatDate(r.hireDate) },
    {
      key: 'actions', header: '', className: 'text-right',
      render: (r) => (
        <Link
          to={`/employees/${r.id}/edit`}
          aria-label={`Manage ${r.fullName}`}
          className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs font-medium text-blue-600 hover:bg-blue-50 dark:text-blue-400 dark:hover:bg-blue-900/20"
        >
          <Pencil className="h-3.5 w-3.5" aria-hidden="true" /> {r.hasUserAccount ? 'Manage' : 'Set up'}
        </Link>
      ),
    },
  ]

  return (
    <div>
      <PageHeader title="Users" subtitle="Employee sign-in accounts and access." />

      <div className="mb-4 flex items-start gap-2.5 rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800 dark:border-blue-900/50 dark:bg-blue-900/20 dark:text-blue-300">
        <Info className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
        <div>
          <p>
            User accounts are created from an employee's record (Edit → User account). Password resets are
            done through the <span className="font-semibold">“Forgot password”</span> link on the sign-in page.
            Role assignments live with your organization's roles (Organization Owner, HR Manager, Payroll Manager, etc.).
          </p>
        </div>
      </div>

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Input
          placeholder="Search employees…"
          aria-label="Search users"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
          className="max-w-xs"
        />
        <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
          <input
            type="checkbox"
            checked={onlyWithAccount}
            onChange={(e) => setOnlyWithAccount(e.target.checked)}
            className="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500 dark:border-slate-600 dark:bg-slate-800"
          />
          Only employees with accounts
        </label>
        <Link to="/settings/security" className="ml-auto inline-flex items-center gap-1.5 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400">
          <ShieldCheck className="h-4 w-4" aria-hidden="true" /> Two-factor authentication
        </Link>
      </div>

      <Card bodyClassName="!p-0">
        <DataTable
          columns={columns}
          rows={rows}
          loading={employees.isLoading}
          page={employees.data?.page || page}
          pageSize={employees.data?.pageSize || 15}
          totalCount={onlyWithAccount ? rows.length : employees.data?.totalCount || 0}
          totalPages={onlyWithAccount ? 1 : employees.data?.totalPages || 1}
          onPageChange={setPage}
          empty={<EmptyState icon={UsersRound} title="No employees" description="Employee accounts appear here once people are added." />}
        />
      </Card>
    </div>
  )
}
