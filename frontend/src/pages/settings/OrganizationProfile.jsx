import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { useOrg, useUpdateOrg } from '../../api/queries'
import { useAuth } from '../../auth/AuthContext'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import Card from '../../components/ui/Card'
import Input from '../../components/ui/Input'
import Button from '../../components/ui/Button'
import Spinner from '../../components/ui/Spinner'

const schema = z.object({
  name: z.string().min(2, 'Name is required'),
  email: z.email('Enter a valid email').or(z.literal('')),
  phone: z.string(),
  logoUrl: z.string(),
  brandColor: z.string(),
  addressLine: z.string(),
  city: z.string(),
  country: z.string().min(2, 'Country is required'),
  timeZone: z.string().min(1, 'Time zone is required'),
  currency: z.string().min(3, 'ISO currency code required').max(3, 'ISO currency code required'),
})

export default function OrganizationProfile() {
  const org = useOrg()
  const update = useUpdateOrg()
  const toast = useToast()
  const { refreshUser } = useAuth()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting, isDirty },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { name: '', email: '', phone: '', logoUrl: '', brandColor: '#2563eb', addressLine: '', city: '', country: '', timeZone: '', currency: '' },
  })

  useEffect(() => {
    if (org.data) {
      const o = org.data
      reset({
        name: o.name || '', email: o.email || '', phone: o.phone || '',
        logoUrl: o.logoUrl || '', brandColor: o.brandColor || '#2563eb',
        addressLine: o.addressLine || '', city: o.city || '', country: o.country || '',
        timeZone: o.timeZone || '', currency: o.currency || '',
      })
    }
  }, [org.data, reset])

  async function onSubmit(values) {
    try {
      await update.mutateAsync(values)
      toast.success('Organization profile updated')
      refreshUser() // org name shows in the topbar
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  if (org.isLoading) return <div className="py-16 text-center"><Spinner className="mx-auto" /></div>

  return (
    <Card title="Organization profile" subtitle="Used on payslips, documents and the workspace header.">
      <form onSubmit={handleSubmit(onSubmit)} className="grid gap-4 sm:grid-cols-2" noValidate>
        <Input label="Organization name" error={errors.name?.message} {...register('name')} />
        <Input label="Email" type="email" error={errors.email?.message} {...register('email')} />
        <Input label="Phone" error={errors.phone?.message} {...register('phone')} />
        <Input label="Logo URL" hint="Optional public image URL" error={errors.logoUrl?.message} {...register('logoUrl')} />
        <Input label="Address" error={errors.addressLine?.message} {...register('addressLine')} />
        <Input label="City" error={errors.city?.message} {...register('city')} />
        <Input label="Country" error={errors.country?.message} {...register('country')} />
        <Input label="Time zone" hint="IANA name, e.g. Africa/Addis_Ababa" error={errors.timeZone?.message} {...register('timeZone')} />
        <Input label="Currency" hint="ISO 4217 code, e.g. ETB" error={errors.currency?.message} {...register('currency')} />
        <Input label="Brand color" type="color" className="!mt-0 h-10" error={errors.brandColor?.message} {...register('brandColor')} />
        <div className="sm:col-span-2">
          <Button type="submit" loading={isSubmitting} disabled={!isDirty}>Save changes</Button>
        </div>
      </form>
    </Card>
  )
}
