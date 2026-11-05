import { useState } from 'react'
import {
  Building2, Users, UserCheck, Wallet, CreditCard, Ban, PlayCircle,
  RefreshCw, Building, Search, ShieldCheck,
} from 'lucide-react'
import { usePlatformStats, useTenants, usePlans, useTenantAction, useSavePlan } from '../api/queries'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import { useToast } from '../components/ui/Toast'
import { apiErrorMessage, getSelectedTenantId, setSelectedTenantId } from '../api/client'
import { formatDate, statusTone, money } from '../lib/format'
import { useDebounce } from '../lib/hooks'
import PageHeader from '../components/ui/PageHeader'
import Card from '../components/ui/Card'
import Badge from '../components/ui/Badge'
import Button from '../components/ui/Button'
import Input from '../components/ui/Input'
import Select from '../components/ui/Select'
import Modal from '../components/ui/Modal'
import DataTable from '../components/ui/DataTable'
import EmptyState from '../components/ui/EmptyState'
import StatCard from '../components/ui/StatCard'
import Spinner from '../components/ui/Spinner'

const SUB_STATUSES = ['Trialing', 'Active', 'PastDue', 'Suspended', 'Cancelled']

export default function Platform() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [tenantSearch, setTenantSearch] = useState('')
  const [page, setPage] = useState(1)
  const debounced = useDebounce(tenantSearch)

  const stats = usePlatformStats()
  const tenants = useTenants({ page, pageSize: 10, search: debounced })
  const plans = usePlans()
  const action = useTenantAction()

  const selectedTenant = getSelectedTenantId()
  const [subFor, setSubFor] = useState(null)
  const [planEditor, setPlanEditor] = useState(null)

  function switchTenant(id) {
    setSelectedTenantId(id)
    // Data for /platform/* is tenant-scoped — refetch everything.
    queryClient.invalidateQueries()
    toast.success(id ? 'Tenant selected — platform actions now target it' : 'Tenant cleared')
  }

  async function doTenantAction(tenant, act) {
    try {
      await action.mutateAsync({ id: tenant.id, action: act })
      toast.success(`${tenant.name} ${act}d`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  const tenantColumns = [
    { key: 'name', header: 'Organization', render: (t) => (
      <span>
        <span className="block font-medium text-slate-900 dark:text-slate-100">{t.name}</span>
        <span className="text-xs text-slate-400">{t.slug}{t.country ? ` · ${t.country}` : ''}</span>
      </span>
    ) },
    { key: 'planName', header: 'Plan', render: (t) => (t.planName ? <Badge tone="blue">{t.planName}</Badge> : '—') },
    { key: 'subscriptionStatus', header: 'Subscription', render: (t) => (t.subscriptionStatus ? <Badge tone={statusTone(t.subscriptionStatus)}>{t.subscriptionStatus}</Badge> : '—') },
    { key: 'employeeCount', header: 'Employees', className: 'text-right' },
    { key: 'userCount', header: 'Users', className: 'text-right' },
    { key: 'isActive', header: 'Status', render: (t) => <Badge tone={t.isActive ? 'green' : 'red'}>{t.isActive ? 'Active' : 'Suspended'}</Badge> },
    { key: 'createdAtUtc', header: 'Created', render: (t) => formatDate(t.createdAtUtc) },
    {
      key: 'actions', header: 'Actions', className: 'text-right',
      render: (t) => (
        <span className="flex justify-end gap-1.5">
          <Button size="sm" variant="secondary" onClick={() => setSubFor(t)}>
            <CreditCard className="h-3.5 w-3.5" aria-hidden="true" /> Subscription
          </Button>
          {t.isActive ? (
            <Button size="sm" variant="ghost" aria-label={`Suspend ${t.name}`} loading={action.isPending && action.variables?.id === t.id}
              onClick={() => doTenantAction(t, 'suspend')}>
              <Ban className="h-4 w-4 text-red-500" aria-hidden="true" />
            </Button>
          ) : (
            <Button size="sm" variant="ghost" aria-label={`Activate ${t.name}`} loading={action.isPending && action.variables?.id === t.id}
              onClick={() => doTenantAction(t, 'activate')}>
              <PlayCircle className="h-4 w-4 text-emerald-500" aria-hidden="true" />
            </Button>
          )}
        </span>
      ),
    },
  ]

  return (
    <div>
      <PageHeader
        title="Platform console"
        subtitle="Signed in as a PeopleDesk platform administrator."
        actions={<Badge tone="purple"><ShieldCheck className="mr-1 h-3 w-3" aria-hidden="true" /> Super admin</Badge>}
      />

      {/* Tenant switcher — sends X-Tenant-Id on /platform calls when set */}
      <Card className="mb-4" title="Acting tenant" subtitle="Pick a tenant to manage it as if signed in there (X-Tenant-Id header).">
        <div className="flex flex-wrap items-end gap-3">
          <Select
            label="Selected tenant"
            className="w-80"
            placeholder="None — platform-wide"
            value={selectedTenant}
            onChange={(e) => switchTenant(e.target.value)}
            options={(tenants.data?.items || []).map((t) => ({ value: t.id, label: `${t.name} (${t.slug})` }))}
          />
          {selectedTenant && (
            <Button variant="ghost" onClick={() => switchTenant('')}>
              <RefreshCw className="h-4 w-4" aria-hidden="true" /> Clear
            </Button>
          )}
        </div>
      </Card>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4 xl:grid-cols-7">
        <StatCard label="Tenants" value={stats.data?.tenants ?? '—'} icon={Building} loading={stats.isLoading} />
        <StatCard label="Active" value={stats.data?.activeTenants ?? '—'} icon={Building2} tone="green" loading={stats.isLoading} />
        <StatCard label="Suspended" value={stats.data?.suspendedTenants ?? '—'} icon={Ban} tone="red" loading={stats.isLoading} />
        <StatCard label="Users" value={stats.data?.users ?? '—'} icon={Users} tone="purple" loading={stats.isLoading} />
        <StatCard label="Employees" value={stats.data?.employees ?? '—'} icon={UserCheck} tone="blue" loading={stats.isLoading} />
        <StatCard label="Payroll runs" value={stats.data?.finalizedPayrollRuns ?? '—'} icon={Wallet} tone="amber" loading={stats.isLoading} />
        <StatCard label="Trials" value={stats.data?.trialSubscriptions ?? '—'} icon={CreditCard} tone="slate" loading={stats.isLoading} />
      </div>

      <Card
        title="Tenants"
        className="mt-4"
        bodyClassName="!p-0"
        actions={(
          <Input
            placeholder="Search tenants…"
            aria-label="Search tenants"
            value={tenantSearch}
            onChange={(e) => { setTenantSearch(e.target.value); setPage(1) }}
            className="max-w-64"
          />
        )}
      >
        <DataTable
          columns={tenantColumns}
          rows={tenants.data?.items || []}
          loading={tenants.isLoading}
          page={tenants.data?.page || page}
          pageSize={tenants.data?.pageSize || 10}
          totalCount={tenants.data?.totalCount || 0}
          totalPages={tenants.data?.totalPages || 1}
          onPageChange={setPage}
          empty={<EmptyState icon={Search} title="No tenants" description="No organizations match your search." />}
        />
      </Card>

      <Card
        title="Subscription plans"
        className="mt-4"
        bodyClassName="!p-0"
        actions={plans.isLoading ? <Spinner size="h-4 w-4" /> : <Button size="sm" onClick={() => setPlanEditor({})}>New plan</Button>}
      >
        <DataTable
          columns={[
            { key: 'name', header: 'Plan', render: (p) => (
              <span>
                <span className="block font-medium text-slate-900 dark:text-slate-100">{p.name}</span>
                <span className="font-mono text-xs text-slate-400">{p.code}</span>
              </span>
            ) },
            { key: 'pricePerMonth', header: 'Price / month', render: (p) => money(p.pricePerMonth, 'USD') },
            { key: 'maxEmployees', header: 'Max employees', className: 'text-right' },
            { key: 'description', header: 'Description', render: (p) => <span className="max-w-64 truncate">{p.description || '—'}</span> },
            { key: 'isActive', header: 'Status', render: (p) => <Badge tone={p.isActive ? 'green' : 'slate'}>{p.isActive ? 'Active' : 'Retired'}</Badge> },
            {
              key: 'actions', header: '', className: 'text-right',
              render: (p) => <Button variant="ghost" size="sm" onClick={() => setPlanEditor(p)}>Edit</Button>,
            },
          ]}
          rows={plans.data || []}
          loading={plans.isLoading}
          empty={<EmptyState title="No plans" description="Create subscription plans for tenants." />}
        />
      </Card>

      {/* Subscription dialog */}
      {subFor && (
        <SubscriptionModal
          open
          tenant={subFor}
          plans={plans.data || []}
          onClose={() => setSubFor(null)}
          onSave={async (values) => {
            try {
              await action.mutateAsync({ id: subFor.id, action: 'subscription', body: values })
              toast.success(`${subFor.name} subscription updated`)
              setSubFor(null)
            } catch (e) {
              toast.error(await apiErrorMessage(e))
            }
          }}
          saving={action.isPending}
        />
      )}

      {/* Plan editor */}
      {planEditor !== null && (
        <PlanModal
          open
          plan={planEditor.id ? planEditor : null}
          onClose={() => setPlanEditor(null)}
        />
      )}
    </div>
  )
}

// Separate component so the save mutation lives at hook level.
function SubscriptionModal({ open, tenant, plans, onClose, onSave, saving }) {
  const [planId, setPlanId] = useState(tenant.planName ? (plans.find((p) => p.name === tenant.planName)?.id || '') : '')
  const [status, setStatus] = useState(tenant.subscriptionStatus || 'Active')
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={`Subscription — ${tenant.name}`}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button loading={saving} onClick={() => onSave({ planId, status })}>Save</Button>
        </>
      }
    >
      <div className="space-y-4">
        <Select
          label="Plan"
          value={planId}
          onChange={(e) => setPlanId(e.target.value)}
          options={plans.map((p) => ({ value: p.id, label: `${p.name} — max ${p.maxEmployees} employees` }))}
        />
        <Select
          label="Status"
          value={status}
          onChange={(e) => setStatus(e.target.value)}
          options={SUB_STATUSES.map((s) => ({ value: s, label: s }))}
        />
      </div>
    </Modal>
  )
}

function PlanModal({ open, plan, onClose, onSave }) {
  const save = useSavePlan()
  const [values, setValues] = useState({
    code: plan?.code || '',
    name: plan?.name || '',
    maxEmployees: plan?.maxEmployees ?? 50,
    pricePerMonth: plan?.pricePerMonth ?? 0,
    description: plan?.description || '',
    isActive: plan?.isActive ?? true,
  })
  const [errors, setErrors] = useState({})

  function set(key, value) { setValues((v) => ({ ...v, [key]: value })) }

  async function submit(e) {
    e.preventDefault()
    const next = {}
    if (!values.code.trim()) next.code = 'Code is required'
    if (!values.name.trim()) next.name = 'Name is required'
    setErrors(next)
    if (Object.keys(next).length) return
    try {
      await save.mutateAsync({
        ...(plan?.id ? { id: plan.id } : {}),
        code: values.code.trim().toUpperCase(),
        name: values.name.trim(),
        maxEmployees: Number(values.maxEmployees) || 0,
        pricePerMonth: Number(values.pricePerMonth) || 0,
        description: values.description.trim() || undefined,
        isActive: values.isActive,
      })
      onClose()
    } catch (err) {
      await apiErrorMessage(err).then((m) => setErrors({ code: m }))
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={plan ? `Edit plan ${plan.name}` : 'New plan'}
      footer={<><Button variant="secondary" onClick={onClose}>Cancel</Button><Button onClick={submit} loading={save.isPending}>Save plan</Button></>}
    >
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <Input label="Code" value={values.code} onChange={(e) => set('code', e.target.value)} error={errors.code} />
        <Input label="Name" value={values.name} onChange={(e) => set('name', e.target.value)} error={errors.name} />
        <Input label="Max employees" type="number" min="1" value={values.maxEmployees} onChange={(e) => set('maxEmployees', e.target.value)} />
        <Input label="Price per month (USD)" type="number" step="0.01" min="0" value={values.pricePerMonth} onChange={(e) => set('pricePerMonth', e.target.value)} />
        <Input label="Description" value={values.description} onChange={(e) => set('description', e.target.value)} className="sm:col-span-2" />
        <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
          <input
            type="checkbox"
            checked={values.isActive}
            onChange={(e) => set('isActive', e.target.checked)}
            className="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500 dark:border-slate-600 dark:bg-slate-800"
          />
          Plan is active (assignable to tenants)
        </label>
      </form>
    </Modal>
  )
}
