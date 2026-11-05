import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import { ShieldCheck } from 'lucide-react'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import { useAuth } from '../../auth/AuthContext'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'

const schema = z.object({
  email: z.email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
})

export default function Login() {
  const { login, verify2fa } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const toast = useToast()
  const [pendingToken, setPendingToken] = useState(null)
  const [code, setCode] = useState('')
  const [twoFaLoading, setTwoFaLoading] = useState(false)
  const from = location.state?.from || '/'

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { email: '', password: '' } })

  async function onSubmit(values) {
    try {
      const data = await login(values.email, values.password)
      if (data?.twoFactorRequired) {
        setPendingToken(data.pendingToken)
        return
      }
      navigate(from, { replace: true })
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  async function onVerify(e) {
    e.preventDefault()
    setTwoFaLoading(true)
    try {
      await verify2fa(pendingToken, code)
      navigate(from, { replace: true })
    } catch (err) {
      toast.error(await apiErrorMessage(err))
    } finally {
      setTwoFaLoading(false)
    }
  }

  if (pendingToken) {
    return (
      <div>
        <div className="mb-6 text-center">
          <ShieldCheck className="mx-auto h-10 w-10 text-blue-600" aria-hidden="true" />
          <h1 className="mt-3 text-xl font-bold text-slate-900 dark:text-white">Two-factor verification</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Enter the 6-digit code from your authenticator app.
          </p>
        </div>
        <form onSubmit={onVerify} className="space-y-4">
          <Input
            label="Authentication code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
            inputMode="numeric"
            autoComplete="one-time-code"
            autoFocus
            required
          />
          <Button type="submit" className="w-full" size="lg" loading={twoFaLoading}>
            Verify and sign in
          </Button>
        </form>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-xl font-bold text-slate-900 dark:text-white">Sign in</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
          Welcome back. Enter your work credentials.
        </p>
      </div>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
        <Input label="Email" type="email" autoComplete="email" placeholder="you@company.com" error={errors.email?.message} {...register('email')} />
        <Input label="Password" type="password" autoComplete="current-password" error={errors.password?.message} {...register('password')} />
        <div className="flex justify-end">
          <Link to="/forgot-password" className="text-sm text-blue-600 hover:underline dark:text-blue-400">
            Forgot password?
          </Link>
        </div>
        <Button type="submit" className="w-full" size="lg" loading={isSubmitting}>
          Sign in
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-slate-500 dark:text-slate-400">
        New to PeopleDesk?{' '}
        <Link to="/register" className="font-medium text-blue-600 hover:underline dark:text-blue-400">
          Create your organization
        </Link>
      </p>
    </div>
  )
}
