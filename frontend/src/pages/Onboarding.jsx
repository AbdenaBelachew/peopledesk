import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import {
  Building2, Plus, Trash2, Check, ArrowRight, ArrowLeft, Users, Sparkles,
} from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import {
  useOrg, useUpdateOrg, useStructure, useSaveStructure, useCreateEmployee, useCreateSalaryStructure,
} from '../api/queries'
import { useToast } from '../components/ui/Toast'
import { apiErrorMessage } from '../api/client'
import { todayInput, money } from '../lib/format'
import Button from '../components/ui/Button'
import Input from '../components/ui/Input'
import Select from '../components/ui/Select'
import Checkbox from '../components/ui/Checkbox'
import Spinner from '../components/ui/Spinner'
import Badge from '../components/ui/Badge'

const orgSchema = z.object({
  name: z.string().min(2, 'Name is required'),
  email: z.email('Enter a valid email').or(z.literal('')),
  phone: z.string(),
  addressLine: z.string(),
  city: z.string(),
  country: z.string().min(2, 'Country is required'),
  currency: z.string().min(3, 'Currency is required'),
  timeZone: z.string().min(1, 'Time zone is required'),
})

const employeeSchema = z.object({
  firstName: z.string().min(1, 'Required'),
  lastName: z.string().min(1, 'Required'),
  gender: z.enum(['Male', 'Female', 'Other']),
  workEmail: z.email('Enter a valid email'),
  hireDate: z.string().min(1, 'Required'),
  basicSalary: z.coerce.number().positive('Enter an amount greater than 0'),
  createUserAccount: z.boolean(),
  initialPassword: z.string().min(8, 'At least 8 characters'),
})

const STEPS = ['Organization', 'Departments', 'First employee']

function Stepper({ step }) {
  return (
    <ol className="flex items-center gap-2" aria-label="Setup steps">
      {STEPS.map((label, i) => (
        <li key={label} className="flex items-center gap-2">
          <span
            className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${
              i < step
                ? 'bg-emerald-500 text-white'
                : i === step
                  ? 'bg-blue-600 text-white'
                  : 'bg-slate-200 text-slate-500 dark:bg-slate-700 dark:text-slate-400'
            }`}
          >
            {i < step ? <Check className="h-4 w-4" aria-hidden="true" /> : i + 1}
          </span>
          <span className={`hidden text-sm font-medium sm:block ${i === step ? 'text-slate-900 dark:text-white' : 'text-slate-500 dark:text-slate-400'}`}>
            {label}
          </span>
          {i < STEPS.length - 1 && <span className="mx-1 h-px w-6 bg-slate-300 dark:bg-slate-600" aria-hidden="true" />}
        </li>
      ))}
    </ol>
  )
}

export default function Onboarding() {
  const navigate = useNavigate()
  const [step, setStep] = useState(0)

  useEffect(() => {
    // The guard in RequireAuth only lets users with the pending flag through.
    document.title = 'Set up your organization — PeopleDesk'
  }, [])

  async function finish() {
    try { localStorage.removeItem('pd_onboarding_pending') } catch { /* ignore */ }
    try { localStorage.setItem('pd_onboarding_done', '1') } catch { /* ignore */ }
    navigate('/', { replace: true })
  }

  return (
    <div className="min-h-screen bg-slate-100 dark:bg-slate-950">
      <header className="border-b border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
        <div className="mx-auto flex max-w-4xl items-center justify-between px-4 py-3">
          <span className="flex items-center gap-2.5">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-600 text-sm font-bold text-white">P</span>
            <span className="font-bold text-slate-900 dark:text-white">People<span className="text-blue-600 dark:text-blue-400">Desk</span></span>
          </span>
          <Button variant="ghost" size="sm" onClick={finish}>Skip for now</Button>
        </div>
      </header>

      <main className="mx-auto max-w-4xl px-4 py-8">
        <div className="mb-6 flex justify-center">
          <Stepper step={step} />
        </div>

        {step === 0 && <StepOrg onNext={() => setStep(1)} />}
        {step === 1 && <StepDepartments onBack={() => setStep(0)} onNext={() => setStep(2)} />}
        {step === 2 && <StepEmployee onBack={() => setStep(1)} onFinish={finish} />}
      </main>
    </div>
  )
}

// ----------------------------- Step 1: Organization -----------------------------

function StepOrg({ onNext }) {
  const { data: org, isLoading } = useOrg()
  const updateOrg = useUpdateOrg()
  const toast = useToast()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(orgSchema),
    defaultValues: { name: '', email: '', phone: '', addressLine: '', city: '', country: 'Ethiopia', currency: 'ETB', timeZone: 'Africa/Addis_Ababa' },
  })

  useEffect(() => {
    if (org) reset({ name: org.name || '', email: org.email || '', phone: org.phone || '', addressLine: org.addressLine || '', city: org.city || '', country: org.country || '', currency: org.currency || 'ETB', timeZone: org.timeZone || 'Africa/Addis_Ababa' })
  }, [org, reset])

  async function onSubmit(values) {
    try {
      await updateOrg.mutateAsync(values)
      toast.success('Organization profile saved')
      onNext()
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  if (isLoading) return <div className="py-16 text-center"><Spinner className="mx-auto" /></div>

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900" noValidate>
      <div className="mb-4 flex items-center gap-2">
        <Building2 className="h-5 w-5 text-blue-600" aria-hidden="true" />
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Review your organization profile</h2>
      </div>
      <p className="mb-5 text-sm text-slate-500 dark:text-slate-400">This appears on payslips and official documents.</p>
      <div className="grid gap-4 sm:grid-cols-2">
        <Input label="Organization name" error={errors.name?.message} {...register('name')} />
        <Input label="Email" type="email" error={errors.email?.message} {...register('email')} />
        <Input label="Phone" error={errors.phone?.message} {...register('phone')} />
        <Input label="Address" error={errors.addressLine?.message} {...register('addressLine')} />
        <Input label="City" error={errors.city?.message} {...register('city')} />
        <Input label="Country" error={errors.country?.message} {...register('country')} />
        <Input label="Currency code" hint="ISO code, e.g. ETB, USD" error={errors.currency?.message} {...register('currency')} />
        <Input label="Time zone" hint="IANA name, e.g. Africa/Addis_Ababa" error={errors.timeZone?.message} {...register('timeZone')} />
      </div>
      <div className="mt-6 flex justify-end">
        <Button type="submit" size="lg" loading={updateOrg.isPending}>
          Save and continue <ArrowRight className="h-4 w-4" aria-hidden="true" />
        </Button>
      </div>
    </form>
  )
}

// ----------------------------- Step 2: Departments -----------------------------

function StepDepartments({ onBack, onNext }) {
  const { data: departments = [], isLoading } = useStructure('departments')
  const save = useSaveStructure('departments')
  const toast = useToast()
  const [rows, setRows] = useState([{ name: '', code: '' }])
  const [saving, setSaving] = useState(false)

  function updateRow(i, key, value) {
    setRows((r) => r.map((row, idx) => (idx === i ? { ...row, [key]: value } : row)))
  }

  async function saveAll() {
    const valid = rows.filter((r) => r.name.trim())
    if (!valid.length) { onNext(); return }
    setSaving(true)
    try {
      for (const row of valid) {
        await save.mutateAsync({ name: row.name.trim(), code: row.code.trim() || undefined })
      }
      toast.success(`${valid.length} department${valid.length > 1 ? 's' : ''} created`)
      setRows([{ name: '', code: '' }])
      onNext()
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="mb-4 flex items-center gap-2">
        <Sparkles className="h-5 w-5 text-blue-600" aria-hidden="true" />
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Add your departments</h2>
      </div>
      <p className="mb-5 text-sm text-slate-500 dark:text-slate-400">
        Departments organize your directory and reports. You can add more later in Settings.
      </p>

      {isLoading ? (
        <div className="py-8 text-center"><Spinner className="mx-auto" /></div>
      ) : (
        <>
          {departments.length > 0 && (
            <div className="mb-4 flex flex-wrap gap-2">
              {departments.map((d) => (
                <Badge key={d.id} tone="blue">{d.name}</Badge>
              ))}
            </div>
          )}
          <div className="space-y-3">
            {rows.map((row, i) => (
              <div key={i} className="flex items-end gap-2">
                <Input
                  label={i === 0 ? 'Department name' : ''}
                  placeholder="e.g. Finance"
                  value={row.name}
                  onChange={(e) => updateRow(i, 'name', e.target.value)}
                  className="flex-1"
                />
                <Input
                  label={i === 0 ? 'Code (optional)' : ''}
                  placeholder="FIN"
                  value={row.code}
                  onChange={(e) => updateRow(i, 'code', e.target.value)}
                  className="w-32"
                />
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Remove row"
                  onClick={() => setRows((r) => (r.length > 1 ? r.filter((_, idx) => idx !== i) : [{ name: '', code: '' }]))}
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                </Button>
              </div>
            ))}
          </div>
          <button
            type="button"
            onClick={() => setRows((r) => [...r, { name: '', code: '' }])}
            className="mt-3 inline-flex items-center gap-1 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400"
          >
            <Plus className="h-4 w-4" aria-hidden="true" /> Add another
          </button>

          <div className="mt-6 flex justify-between">
            <Button variant="secondary" onClick={onBack}><ArrowLeft className="h-4 w-4" aria-hidden="true" /> Back</Button>
            <div className="flex gap-2">
              <Button variant="ghost" onClick={onNext}>Skip</Button>
              <Button onClick={saveAll} loading={saving}>Save and continue</Button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

// ----------------------------- Step 3: First employee -----------------------------

function StepEmployee({ onBack, onFinish }) {
  const { user } = useAuth()
  const { data: org } = useOrg()
  const createEmployee = useCreateEmployee()
  const createStructure = useCreateSalaryStructure()
  const toast = useToast()
  const [created, setCreated] = useState(null)
  const [busy, setBusy] = useState(false)

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(employeeSchema),
    defaultValues: {
      firstName: '', lastName: '', gender: 'Male',
      workEmail: '', hireDate: todayInput(),
      basicSalary: '', createUserAccount: true,
      initialPassword: 'Welcome@2026',
    },
  })

  const createAccount = watch('createUserAccount')

  async function onSubmit(values) {
    setBusy(true)
    try {
      const employee = await createEmployee.mutateAsync({
        firstName: values.firstName,
        lastName: values.lastName,
        gender: values.gender,
        workEmail: values.workEmail,
        hireDate: values.hireDate,
        status: 'Active',
        createUserAccount: values.createUserAccount,
        initialPassword: values.createUserAccount ? values.initialPassword : undefined,
      })
      await createStructure.mutateAsync({
        employeeId: employee.id,
        effectiveFrom: values.hireDate,
        currency: org?.currency || user?.currency || 'ETB',
        components: [
          {
            kind: 'Earning', code: 'BASIC', label: 'Basic Salary', type: 'Basic',
            calculationType: 'FixedAmount', amount: values.basicSalary, isTaxable: true, isActive: true,
          },
        ],
      })
      setCreated(employee)
      toast.success(`${values.firstName} has been added with a basic salary`)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="mb-4 flex items-center gap-2">
        <Users className="h-5 w-5 text-blue-600" aria-hidden="true" />
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Invite your first employee</h2>
      </div>
      <p className="mb-5 text-sm text-slate-500 dark:text-slate-400">
        Add one person now — you can bulk-import the rest of your team from the Employees page.
      </p>

      {created ? (
        <div className="py-6 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-emerald-50 dark:bg-emerald-900/30">
            <Check className="h-6 w-6 text-emerald-500" aria-hidden="true" />
          </div>
          <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">{created.fullName || `${created.firstName} ${created.lastName}`}</h3>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Employee record created{createAccount && created.hasUserAccount ? ' with a user account' : ''}. Basic salary: {money(watch('basicSalary'), org?.currency)}
          </p>
          <Button className="mt-5" size="lg" onClick={onFinish}>
            Go to dashboard <ArrowRight className="h-4 w-4" aria-hidden="true" />
          </Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            <Input label="First name" error={errors.firstName?.message} {...register('firstName')} />
            <Input label="Last name" error={errors.lastName?.message} {...register('lastName')} />
            <Select
              label="Gender"
              options={[{ value: 'Male', label: 'Male' }, { value: 'Female', label: 'Female' }, { value: 'Other', label: 'Other' }]}
              error={errors.gender?.message}
              {...register('gender')}
            />
            <Input label="Work email" type="email" error={errors.workEmail?.message} {...register('workEmail')} />
            <Input label="Hire date" type="date" error={errors.hireDate?.message} {...register('hireDate')} />
            <Input
              label={`Basic salary (${org?.currency || user?.currency || 'ETB'})`}
              type="number"
              step="0.01"
              min="0"
              error={errors.basicSalary?.message}
              {...register('basicSalary')}
            />
          </div>
          <Checkbox
            label="Create a user account"
            description="The employee can sign in with the initial password below (they should change it after first login)."
            {...register('createUserAccount')}
          />
          {createAccount && <Input label="Initial password" error={errors.initialPassword?.message} {...register('initialPassword')} />}
          <div className="flex justify-between pt-2">
            <Button variant="secondary" onClick={onBack} disabled={busy}><ArrowLeft className="h-4 w-4" aria-hidden="true" /> Back</Button>
            <Button type="submit" loading={busy}>Create employee</Button>
          </div>
        </form>
      )}
    </div>
  )
}
