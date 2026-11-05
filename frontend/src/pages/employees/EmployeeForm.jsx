import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { UserRound, Briefcase, Landmark, UserPlus, ArrowLeft } from 'lucide-react'
import {
  useEmployee, useCreateEmployee, useUpdateEmployee, useStructure, useWorkSchedules, useEmployees,
} from '../../api/queries'
import { useAuth } from '../../auth/AuthContext'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { dateInput } from '../../lib/format'
import Card from '../../components/ui/Card'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'
import Checkbox from '../../components/ui/Checkbox'
import Button from '../../components/ui/Button'
import Spinner from '../../components/ui/Spinner'

const schema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  middleName: z.string(),
  lastName: z.string().min(1, 'Last name is required'),
  gender: z.enum(['Male', 'Female', 'Other']),
  dateOfBirth: z.string(),
  nationalId: z.string(),
  workEmail: z.email('Enter a valid email').or(z.literal('')),
  personalEmail: z.email('Enter a valid email').or(z.literal('')),
  phone: z.string(),
  addressLine: z.string(),
  city: z.string(),
  departmentId: z.string(),
  positionId: z.string(),
  branchId: z.string(),
  employmentTypeId: z.string(),
  scheduleId: z.string(),
  managerId: z.string(),
  hireDate: z.string().min(1, 'Hire date is required'),
  contractEndDate: z.string(),
  status: z.string(),
  paymentMethod: z.string(),
  bankName: z.string(),
  bankAccount: z.string(),
  taxIdentificationNumber: z.string(),
  createUserAccount: z.boolean(),
  initialPassword: z.string(),
})
  .refine((v) => !v.createUserAccount || v.workEmail.length > 0, {
    message: 'A work email is required to create a user account',
    path: ['workEmail'],
  })
  .refine((v) => !v.createUserAccount || v.initialPassword.length >= 8, {
    message: 'Initial password needs at least 8 characters',
    path: ['initialPassword'],
  })

export default function EmployeeForm() {
  const { id } = useParams()
  const isEdit = !!id
  const navigate = useNavigate()
  const toast = useToast()
  const { user, hasPerm } = useAuth()
  const [submitError, setSubmitError] = useState('')

  const departments = useStructure('departments')
  const branches = useStructure('branches')
  const positions = useStructure('positions')
  const employmentTypes = useStructure('employment-types')
  const schedules = useWorkSchedules()
  const colleagues = useEmployees({ page: 1, pageSize: 200 })
  const existing = useEmployee(id, isEdit)
  const createEmployee = useCreateEmployee()
  const updateEmployee = useUpdateEmployee()

  const canSensitive = hasPerm('employees.viewSensitive')

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: '', middleName: '', lastName: '', gender: 'Male', dateOfBirth: '',
      nationalId: '', workEmail: '', personalEmail: '', phone: '', addressLine: '', city: '',
      departmentId: '', positionId: '', branchId: '', employmentTypeId: '', scheduleId: '',
      managerId: '', hireDate: '', contractEndDate: '', status: 'Active',
      paymentMethod: 'Bank', bankName: '', bankAccount: '', taxIdentificationNumber: '',
      createUserAccount: false, initialPassword: 'Welcome@2026',
    },
  })

  useEffect(() => {
    if (isEdit && existing.data) {
      const e = existing.data
      reset({
        firstName: e.firstName || '', middleName: e.middleName || '', lastName: e.lastName || '',
        gender: e.gender || 'Male', dateOfBirth: dateInput(e.dateOfBirth), nationalId: e.nationalId || '',
        workEmail: e.workEmail || '', personalEmail: e.personalEmail || '', phone: e.phone || '',
        addressLine: e.addressLine || '', city: e.city || '',
        departmentId: e.departmentId || '', positionId: e.positionId || '', branchId: e.branchId || '',
        employmentTypeId: e.employmentTypeId || '', scheduleId: e.scheduleId || '', managerId: e.managerId || '',
        hireDate: dateInput(e.hireDate), contractEndDate: dateInput(e.contractEndDate),
        status: e.status || 'Active', paymentMethod: e.paymentMethod || 'Bank',
        bankName: e.bankName || '', bankAccount: '', taxIdentificationNumber: e.taxIdentificationNumber || '',
        createUserAccount: false, initialPassword: '',
      })
    }
  }, [isEdit, existing.data, reset])

  const createUserAccount = watch('createUserAccount')

  const structureOptions = (list) => (list || []).map((x) => ({ value: x.id, label: x.name }))
  const managerOptions = useMemo(
    () =>
      (colleagues.data?.items || [])
        .filter((e) => e.id !== id)
        .map((e) => ({ value: e.id, label: `${e.fullName} (${e.employeeNumber})` })),
    [colleagues.data, id],
  )

  async function onSubmit(values) {
    setSubmitError('')
    const payload = {
      firstName: values.firstName,
      middleName: values.middleName || undefined,
      lastName: values.lastName,
      gender: values.gender,
      dateOfBirth: values.dateOfBirth || undefined,
      workEmail: values.workEmail || undefined,
      personalEmail: values.personalEmail || undefined,
      phone: values.phone || undefined,
      addressLine: values.addressLine || undefined,
      city: values.city || undefined,
      departmentId: values.departmentId || undefined,
      positionId: values.positionId || undefined,
      branchId: values.branchId || undefined,
      employmentTypeId: values.employmentTypeId || undefined,
      scheduleId: values.scheduleId || undefined,
      managerId: values.managerId || undefined,
      hireDate: values.hireDate,
      contractEndDate: values.contractEndDate || undefined,
      paymentMethod: values.paymentMethod || undefined,
    }
    // Sensitive fields only travel when the viewer can see them.
    if (canSensitive) {
      payload.nationalId = values.nationalId || undefined
      payload.taxIdentificationNumber = values.taxIdentificationNumber || undefined
    }
    try {
      if (isEdit) {
        await updateEmployee.mutateAsync({ id, ...payload })
        toast.success('Employee updated')
        navigate(`/employees/${id}`)
      } else {
        payload.status = values.status
        payload.bankName = values.bankName || undefined
        payload.bankAccount = values.bankAccount || undefined
        if (values.createUserAccount) {
          payload.createUserAccount = true
          payload.initialPassword = values.initialPassword
        }
        const created = await createEmployee.mutateAsync(payload)
        toast.success('Employee created')
        navigate(`/employees/${created.id}`)
      }
    } catch (e) {
      setSubmitError(await apiErrorMessage(e))
    }
  }

  if (isEdit && existing.isLoading) {
    return <div className="py-16 text-center"><Spinner className="mx-auto" /></div>
  }

  return (
    <div>
      <Link to={isEdit ? `/employees/${id}` : '/employees'} className="mb-3 inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" /> {isEdit ? 'Back to employee' : 'Back to directory'}
      </Link>
      <h1 className="mb-5 text-xl font-bold tracking-tight text-slate-900 dark:text-slate-100">
        {isEdit ? `Edit ${existing.data?.fullName || 'employee'}` : 'Add employee'}
      </h1>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
        {/* Personal */}
        <Card title="Personal information" bodyClassName="p-4">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Input label="First name" error={errors.firstName?.message} {...register('firstName')} />
            <Input label="Middle name" error={errors.middleName?.message} {...register('middleName')} />
            <Input label="Last name" error={errors.lastName?.message} {...register('lastName')} />
            <Select
              label="Gender"
              options={[{ value: 'Male', label: 'Male' }, { value: 'Female', label: 'Female' }, { value: 'Other', label: 'Other' }]}
              error={errors.gender?.message}
              {...register('gender')}
            />
            <Input label="Date of birth" type="date" error={errors.dateOfBirth?.message} {...register('dateOfBirth')} />
            {canSensitive && <Input label="National ID" error={errors.nationalId?.message} {...register('nationalId')} />}
            <Input label="Personal email" type="email" error={errors.personalEmail?.message} {...register('personalEmail')} />
            <Input label="Phone" error={errors.phone?.message} {...register('phone')} />
            <Input label="Address" error={errors.addressLine?.message} {...register('addressLine')} />
            <Input label="City" error={errors.city?.message} {...register('city')} />
          </div>
        </Card>

        {/* Employment */}
        <Card title="Employment">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Select
              label="Department"
              placeholder="—"
              options={structureOptions(departments.data)}
              error={errors.departmentId?.message}
              {...register('departmentId')}
            />
            <Select
              label="Position"
              placeholder="—"
              options={structureOptions(positions.data)}
              error={errors.positionId?.message}
              {...register('positionId')}
            />
            <Select
              label="Branch"
              placeholder="—"
              options={structureOptions(branches.data)}
              error={errors.branchId?.message}
              {...register('branchId')}
            />
            <Select
              label="Employment type"
              placeholder="—"
              options={structureOptions(employmentTypes.data)}
              error={errors.employmentTypeId?.message}
              {...register('employmentTypeId')}
            />
            <Select
              label="Work schedule"
              placeholder="—"
              options={(schedules.data || []).map((s) => ({ value: s.id, label: s.name }))}
              error={errors.scheduleId?.message}
              {...register('scheduleId')}
            />
            <Select
              label="Manager"
              placeholder="—"
              options={managerOptions}
              error={errors.managerId?.message}
              {...register('managerId')}
            />
            <Input label="Work email" type="email" error={errors.workEmail?.message} {...register('workEmail')} />
            <Input label="Hire date" type="date" error={errors.hireDate?.message} {...register('hireDate')} />
            <Input label="Contract end date" type="date" error={errors.contractEndDate?.message} {...register('contractEndDate')} />
            {!isEdit && (
              <Select
                label="Status"
                options={[{ value: 'Active', label: 'Active' }, { value: 'Probation', label: 'Probation' }, { value: 'Suspended', label: 'Suspended' }]}
                error={errors.status?.message}
                {...register('status')}
              />
            )}
            <Select
              label="Payment method"
              options={[{ value: 'Bank', label: 'Bank transfer' }, { value: 'Cash', label: 'Cash' }]}
              error={errors.paymentMethod?.message}
              {...register('paymentMethod')}
            />
          </div>
        </Card>

        {/* Bank */}
        <Card title="Compensation & bank">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Input label="Bank name" error={errors.bankName?.message} {...register('bankName')} />
            {!isEdit && <Input label="Bank account number" error={errors.bankAccount?.message} {...register('bankAccount')} />}
            {isEdit && existing.data?.bankAccountMasked && (
              <Input label="Bank account" value={existing.data.bankAccountMasked} disabled hint="Masked — contact HR to change" />
            )}
            {canSensitive && <Input label="Tax identification number (TIN)" error={errors.taxIdentificationNumber?.message} {...register('taxIdentificationNumber')} />}
          </div>
        </Card>

        {/* User account (create only) */}
        {!isEdit && (
          <Card
            title="User account"
            subtitle="Optional — lets this person sign in to self-service."
            actions={<UserPlus className="h-4 w-4 text-slate-400" aria-hidden="true" />}
          >
            <Checkbox
              label="Create a user account for this employee"
              description="Requires a work email above. The initial password should be changed after first login."
              {...register('createUserAccount')}
            />
            {createUserAccount && (
              <div className="mt-3 max-w-xs">
                <Input label="Initial password" error={errors.initialPassword?.message} {...register('initialPassword')} />
              </div>
            )}
          </Card>
        )}

        {submitError && (
          <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-600 dark:bg-red-900/30 dark:text-red-300" role="alert">
            {submitError}
          </p>
        )}

        <div className="flex justify-end gap-2">
          <Button variant="secondary" type="button" onClick={() => navigate(-1)}>Cancel</Button>
          <Button type="submit" loading={isSubmitting}>
            {isEdit ? 'Save changes' : 'Create employee'}
          </Button>
        </div>
      </form>
    </div>
  )
}
