import { useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useAuth } from '../../auth/AuthContext'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import { slugify } from '../../lib/format'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Select from '../../components/ui/Select'

const schema = z.object({
  organizationName: z.string().min(2, 'Organization name is required'),
  slug: z
    .string()
    .min(3, 'At least 3 characters')
    .max(40, 'Too long')
    .regex(/^[a-z0-9](-?[a-z0-9])*$/, 'Lowercase letters, numbers and dashes only'),
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  email: z.email('Enter a valid email address'),
  password: z.string().min(8, 'At least 8 characters'),
  country: z.string().min(2, 'Country is required'),
})

const COUNTRIES = [
  'Ethiopia', 'Kenya', 'Uganda', 'Tanzania', 'Rwanda', 'Nigeria', 'Ghana', 'Egypt',
  'South Africa', 'United Arab Emirates', 'Saudi Arabia', 'United States', 'United Kingdom',
  'Germany', 'France', 'Netherlands', 'Canada', 'Other',
]

export default function Register() {
  const { register: registerOrg } = useAuth()
  const navigate = useNavigate()
  const toast = useToast()
  const slugTouched = useRef(false)
  const [submitError, setSubmitError] = useState('')

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { organizationName: '', slug: '', firstName: '', lastName: '', email: '', password: '', country: 'Ethiopia' },
  })

  const orgName = watch('organizationName')

  // Auto-suggest the slug from the org name until the user edits it.
  function onOrgNameChange(e) {
    if (!slugTouched.current) {
      setValue('slug', slugify(e.target.value), { shouldValidate: false })
    }
  }

  async function onSubmit(values) {
    setSubmitError('')
    try {
      await registerOrg(values)
      // Trigger the first-run wizard after registration.
      try { localStorage.setItem('pd_onboarding_pending', '1') } catch { /* ignore */ }
      navigate('/onboarding', { replace: true })
    } catch (e) {
      setSubmitError(await apiErrorMessage(e))
    }
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-xl font-bold text-slate-900 dark:text-white">Create your organization</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
          14-day trial. No credit card required. You will be the Organization Owner.
        </p>
      </div>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
        <Input
          label="Organization name"
          placeholder="Acme Trading PLC"
          error={errors.organizationName?.message}
          {...register('organizationName', { onChange: onOrgNameChange })}
        />
        <Input
          label="Workspace URL slug"
          hint={`peopleesk.app/${watch('slug') || 'your-company'}`}
          placeholder="acme-trading"
          error={errors.slug?.message}
          {...register('slug', { onChange: () => { slugTouched.current = true } })}
        />
        <div className="grid grid-cols-2 gap-3">
          <Input label="First name" placeholder="Sara" error={errors.firstName?.message} {...register('firstName')} />
          <Input label="Last name" placeholder="Bekele" error={errors.lastName?.message} {...register('lastName')} />
        </div>
        <Input label="Work email" type="email" placeholder="you@company.com" error={errors.email?.message} {...register('email')} />
        <Input label="Password" type="password" autoComplete="new-password" hint="At least 8 characters" error={errors.password?.message} {...register('password')} />
        <Select
          label="Country"
          options={COUNTRIES.map((c) => ({ value: c, label: c }))}
          error={errors.country?.message}
          {...register('country')}
        />
        {submitError && (
          <p className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-600 dark:bg-red-900/30 dark:text-red-300" role="alert">
            {submitError}
          </p>
        )}
        <Button type="submit" className="w-full" size="lg" loading={isSubmitting}>
          Create organization
        </Button>
      </form>
      <p className="mt-4 text-center text-xs text-slate-400 dark:text-slate-500">
        {orgName ? `You'll set up departments and invite your team next.` : 'By continuing you agree to our terms of service.'}
      </p>
      <p className="mt-4 text-center text-sm text-slate-500 dark:text-slate-400">
        Already have an account?{' '}
        <Link to="/login" className="font-medium text-blue-600 hover:underline dark:text-blue-400">
          Sign in
        </Link>
      </p>
    </div>
  )
}
