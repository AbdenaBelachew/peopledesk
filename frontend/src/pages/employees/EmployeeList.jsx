import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Users, Eye } from 'lucide-react'
import { useEmployees, useStructure } from '../../api/queries'
import { formatDate, statusTone } from '../../lib/format'
import { useDebounce } from '../../lib/hooks'
import PageHeader from '../../components/ui/PageHeader'
import DataTable from '../../components/ui/DataTable'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Badge from '../../components/ui/Badge'
import Avatar from '../../components/ui/Avatar'
import EmptyState from '../../components/ui/EmptyState'
import { useAuth } from '../../auth/AuthContext'

const STATUSES = ['Active', 'Probation', 'Suspended', 'OnLeave', 'Terminated', 'Resigned', 'Retired']

export default function EmployeeList() {
  const { hasPerm } = useAuth()
  const [search, setSearch] = useState('')
  const [departmentId, setDepartmentId] = useState('')
  const [branchId, setBranchId] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState({ sortBy: undefined, sortDesc: false })

  const debouncedSearch = useDebounce(search)
  const departments = useStructure('departments')
  const branches = useStructure('branches')

  const employees = useEmployees({
    page,
    pageSize: 15,
    search: debouncedSearch,
    departmentId,
    branchId,
    status,
    sortBy: sort.sortBy,
    sortDesc: sort.sortBy ? sort.sortDesc : undefined,
  })

  function changeSort(next) {
    setSort(next)
    setPage(1)
  }

  const columns = [
    {
      key: 'employeeNumber',
      header: 'Employee',
      sortable: true,
      render: (r) => (
        <span className="flex items-center gap-2.5">
          <Avatar name={r.fullName} size="sm" />
          <span>
            <Link to={`/employees/${r.id}`} className="block font-medium text-slate-900 hover:text-blue-600 dark:text-slate-100 dark:hover:text-blue-400">
              {r.fullName}
            </Link>
            <span className="text-xs text-slate-400">{r.employeeNumber}</span>
          </span>
        </span>
      ),
    },
    { key: 'department', header: 'Department' },
    { key: 'position', header: 'Position' },
    { key: 'branch', header: 'Branch' },
    { key: 'employmentType', header: 'Type' },
    {
      key: 'status',
      header: 'Status',
      sortable: true,
      render: (r) => <Badge tone={statusTone(r.status)}>{r.status}</Badge>,
    },
    { key: 'hireDate', header: 'Hired', sortable: true, render: (r) => formatDate(r.hireDate) },
    {
      key: 'actions',
      header: '',
      className: 'text-right',
      render: (r) => (
        <Link
          to={`/employees/${r.id}`}
          aria-label={`View ${r.fullName}`}
          className="inline-flex rounded-md p-1.5 text-slate-400 hover:bg-slate-100 hover:text-blue-600 dark:hover:bg-slate-800"
        >
          <Eye className="h-4 w-4" aria-hidden="true" />
        </Link>
      ),
    },
  ]

  return (
    <div>
      <PageHeader
        title="Employees"
        subtitle={`${employees.data?.totalCount ?? 0} people in your organization`}
        actions={
          hasPerm('employees.create') && (
            <Link to="/employees/new">
              <Button><Plus className="h-4 w-4" aria-hidden="true" /> Add employee</Button>
            </Link>
          )
        }
      />

      <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Input
          placeholder="Search name, email, number…"
          aria-label="Search employees"
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <Select
          aria-label="Filter by department"
          placeholder="All departments"
          value={departmentId}
          onChange={(e) => { setDepartmentId(e.target.value); setPage(1) }}
          options={(departments.data || []).map((d) => ({ value: d.id, label: d.name }))}
        />
        <Select
          aria-label="Filter by branch"
          placeholder="All branches"
          value={branchId}
          onChange={(e) => { setBranchId(e.target.value); setPage(1) }}
          options={(branches.data || []).map((b) => ({ value: b.id, label: b.name }))}
        />
        <Select
          aria-label="Filter by status"
          placeholder="All statuses"
          value={status}
          onChange={(e) => { setStatus(e.target.value); setPage(1) }}
          options={STATUSES.map((s) => ({ value: s, label: s }))}
        />
      </div>

      <DataTable
        columns={columns}
        rows={employees.data?.items || []}
        loading={employees.isLoading}
        stickyHeader
        sortBy={sort.sortBy}
        sortDesc={sort.sortDesc}
        onSortChange={changeSort}
        page={employees.data?.page || page}
        pageSize={employees.data?.pageSize || 15}
        totalCount={employees.data?.totalCount || 0}
        totalPages={employees.data?.totalPages || 1}
        onPageChange={setPage}
        empty={
          <EmptyState
            icon={Users}
            title="No employees found"
            description={debouncedSearch || departmentId || branchId || status ? 'Try adjusting your filters.' : 'Add your first employee to get started.'}
            action={hasPerm('employees.create') && !debouncedSearch && <Link to="/employees/new"><Button>Add employee</Button></Link>}
          />
        }
      />
    </div>
  )
}
