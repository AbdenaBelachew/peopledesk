import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { publicApi, apiErrorMessage } from '../../api/client'
import { useToast } from '../../components/ui/Toast'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import { CheckCircle2 } from 'lucide-react'

const schema = z.object({ email: z.email('Enter a valid email address') })

export default function ForgotPassword() {
  const toast = useToast()
  const [sent, setSent] = useState(false)
  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { email: '' } })

  async function onSubmit(values) {
    try {
      await publicApi.post('/auth/forgot-password', values)
      setSent(true)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  if (sent) {
    return (
      <div className="text-center">
        <CheckCircle2 className="mx-auto h-10 w-10 text-emerald-500" aria-hidden="true" />
        <h1 className="mt-3 text-xl font-bold text-slate-900 dark:text-white">Check your email</h1>
        <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
          If an account exists for <span className="font-medium">{getValues('email')}</span>, a reset
          link has been sent.
        </p>
        <Link to="/login" className="mt-6 inline-block text-sm font-medium text-blue-600 hover:underline dark:text-blue-400">
          Back to sign in
        </Link>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-xl font-bold text-slate-900 dark:text-white">Forgot your password?</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
          Enter your email and we will send you a reset link.
        </p>
      </div>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
        <Input label="Email" type="email" autoComplete="email" error={errors.email?.message} {...register('email')} />
        <Button type="submit" className="w-full" size="lg" loading={isSubmitting}>
          Send reset link
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
        <Link to="/login" className="font-medium text-blue-600 hover:underline dark:text-blue-400">
          Back to sign in
        </Link>
      </p>
    </div>
  )
}
