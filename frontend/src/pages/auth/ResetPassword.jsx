import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { publicApi, apiErrorMessage } from '../../api/client'
import { useToast } from '../../components/ui/Toast'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import { CheckCircle2, XCircle } from 'lucide-react'

const schema = z
  .object({
    newPassword: z.string().min(8, 'At least 8 characters'),
    confirm: z.string(),
  })
  .refine((v) => v.newPassword === v.confirm, {
    message: 'Passwords do not match',
    path: ['confirm'],
  })

export default function ResetPassword() {
  const [params] = useSearchParams()
  const token = params.get('token') || ''
  const toast = useToast()
  const [done, setDone] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { newPassword: '', confirm: '' } })

  async function onSubmit(values) {
    try {
      await publicApi.post('/auth/reset-password', { token, newPassword: values.newPassword })
      setDone(true)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  if (!token) {
    return (
      <div className="text-center">
        <XCircle className="mx-auto h-10 w-10 text-red-500" aria-hidden="true" />
        <h1 className="mt-3 text-xl font-bold text-slate-900 dark:text-white">Invalid link</h1>
        <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
          This reset link is missing its token. Request a new one from the sign-in page.
        </p>
        <Link to="/forgot-password" className="mt-6 inline-block text-sm font-medium text-blue-600 hover:underline dark:text-blue-400">
          Request new link
        </Link>
      </div>
    )
  }

  if (done) {
    return (
      <div className="text-center">
        <CheckCircle2 className="mx-auto h-10 w-10 text-emerald-500" aria-hidden="true" />
        <h1 className="mt-3 text-xl font-bold text-slate-900 dark:text-white">Password updated</h1>
        <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">You can now sign in with your new password.</p>
        <Link to="/login" className="mt-6 inline-block text-sm font-medium text-blue-600 hover:underline dark:text-blue-400">
          Sign in
        </Link>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-xl font-bold text-slate-900 dark:text-white">Set a new password</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Choose a strong password of at least 8 characters.</p>
      </div>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
        <Input label="New password" type="password" autoComplete="new-password" error={errors.newPassword?.message} {...register('newPassword')} />
        <Input label="Confirm password" type="password" autoComplete="new-password" error={errors.confirm?.message} {...register('confirm')} />
        <Button type="submit" className="w-full" size="lg" loading={isSubmitting}>
          Reset password
        </Button>
      </form>
    </div>
  )
}
