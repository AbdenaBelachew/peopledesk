import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { publicApi, apiErrorMessage } from '../../api/client'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Spinner from '../../components/ui/Spinner'
import { useToast } from '../../components/ui/Toast'
import { CheckCircle2, XCircle, MailCheck } from 'lucide-react'

export default function VerifyEmail() {
  const [params] = useSearchParams()
  const token = params.get('token') || ''
  const toast = useToast()
  const [state, setState] = useState(token ? 'loading' : 'missing')
  const [errorDetail, setErrorDetail] = useState('')
  const [email, setEmail] = useState('')
  const [resent, setResent] = useState(false)

  useEffect(() => {
    if (!token) return undefined
    let cancelled = false
    ;(async () => {
      try {
        await publicApi.post('/auth/verify-email', { token })
        if (!cancelled) setState('success')
      } catch (e) {
        if (!cancelled) {
          setErrorDetail(await apiErrorMessage(e))
          setState('error')
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [token])

  async function resend(e) {
    e.preventDefault()
    try {
      await publicApi.post('/auth/resend-verification', { email })
      setResent(true)
    } catch (err) {
      toast.error(await apiErrorMessage(err))
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 p-6 dark:bg-slate-950">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-8 text-center shadow-sm dark:border-slate-800 dark:bg-slate-900">
        {state === 'loading' && (
          <div>
            <Spinner className="mx-auto h-10 w-10" />
            <h1 className="mt-3 text-lg font-semibold text-slate-900 dark:text-white">Verifying your email…</h1>
          </div>
        )}
        {state === 'success' && (
          <div>
            <CheckCircle2 className="mx-auto h-10 w-10 text-emerald-500" aria-hidden="true" />
            <h1 className="mt-3 text-lg font-semibold text-slate-900 dark:text-white">Email verified</h1>
            <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Your email address is confirmed. You are all set.</p>
            <Link to="/login" className="mt-5 inline-block rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700">
              Sign in
            </Link>
          </div>
        )}
        {(state === 'error' || state === 'missing') && (
          <div>
            <XCircle className="mx-auto h-10 w-10 text-red-500" aria-hidden="true" />
            <h1 className="mt-3 text-lg font-semibold text-slate-900 dark:text-white">
              {state === 'missing' ? 'No token provided' : 'Verification failed'}
            </h1>
            <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
              {errorDetail || 'The link may have expired or was already used. Request a new verification email below.'}
            </p>
            {resent ? (
              <p className="mt-4 inline-flex items-center gap-1.5 text-sm text-emerald-600 dark:text-emerald-400">
                <MailCheck className="h-4 w-4" aria-hidden="true" /> Verification email sent.
              </p>
            ) : (
              <form onSubmit={resend} className="mt-4 flex items-end gap-2 text-left">
                <Input
                  label="Email"
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="flex-1"
                />
                <Button type="submit">Resend</Button>
              </form>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
