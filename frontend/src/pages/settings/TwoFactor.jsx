import { useState } from 'react'
import { KeyRound, ShieldCheck, ShieldOff, Copy, Check } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { useSetup2fa, useEnable2fa, useDisable2fa } from '../../api/queries'
import { useToast } from '../../components/ui/Toast'
import { apiErrorMessage } from '../../api/client'
import Card from '../../components/ui/Card'
import Button from '../../components/ui/Button'
import Input from '../../components/ui/Input'
import Badge from '../../components/ui/Badge'
import Spinner from '../../components/ui/Spinner'

export default function TwoFactor() {
  const { user, refreshUser } = useAuth()
  const toast = useToast()
  const setup = useSetup2fa()
  const enable = useEnable2fa()
  const disable = useDisable2fa()
  const [setupData, setSetupData] = useState(null)
  const [code, setCode] = useState('')
  const [password, setPassword] = useState('')

  async function startSetup() {
    try {
      const data = await setup.mutateAsync()
      setSetupData(data)
    } catch (e) {
      toast.error(await apiErrorMessage(e))
    }
  }

  async function confirmEnable(e) {
    e.preventDefault()
    try {
      await enable.mutateAsync(code)
      toast.success('Two-factor authentication enabled')
      setSetupData(null)
      setCode('')
      await refreshUser()
    } catch (err) {
      toast.error(await apiErrorMessage(err))
    }
  }

  async function confirmDisable(e) {
    e.preventDefault()
    try {
      await disable.mutateAsync(password)
      toast.success('Two-factor authentication disabled')
      setPassword('')
      await refreshUser()
    } catch (err) {
      toast.error(await apiErrorMessage(err))
    }
  }

  return (
    <Card title="Two-factor authentication" subtitle="Add a second step when signing in.">
      {!user ? (
        <div className="py-8 text-center"><Spinner className="mx-auto" /></div>
      ) : user.twoFactorEnabled ? (
        <div>
          <div className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5 text-emerald-500" aria-hidden="true" />
            <p className="text-sm font-medium text-slate-900 dark:text-slate-100">2FA is enabled on your account</p>
            <Badge tone="green">Secure</Badge>
          </div>
          <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
            You will be asked for a code from your authenticator app at every sign-in.
          </p>
          <form onSubmit={confirmDisable} className="mt-5 max-w-sm space-y-3 border-t border-slate-100 pt-4 dark:border-slate-800">
            <p className="text-sm font-medium text-slate-700 dark:text-slate-300">Disable 2FA</p>
            <Input
              label="Confirm your password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
            <Button type="submit" variant="danger" loading={disable.isPending}>
              <ShieldOff className="h-4 w-4" aria-hidden="true" /> Disable 2FA
            </Button>
          </form>
        </div>
      ) : setupData ? (
        <div>
          <p className="text-sm text-slate-600 dark:text-slate-300">
            1. Add this secret to your authenticator app (Google Authenticator, Authy, 1Password…).
          </p>
          <SecretBox secret={setupData.secret} />
          <p className="mt-4 text-sm text-slate-600 dark:text-slate-300">
            2. Or paste this setup URI into an app that supports <code className="rounded bg-slate-100 px-1 py-0.5 text-xs dark:bg-slate-800">otpauth://</code> links:
          </p>
          <SecretBox secret={setupData.otpAuthUri} multiline />
          <form onSubmit={confirmEnable} className="mt-5 max-w-sm space-y-3">
            <Input
              label="3. Enter the 6-digit code"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
              autoFocus
            />
            <div className="flex gap-2">
              <Button type="submit" loading={enable.isPending}>Verify and enable</Button>
              <Button type="button" variant="secondary" onClick={() => setSetupData(null)}>Cancel</Button>
            </div>
          </form>
        </div>
      ) : (
        <div>
          <div className="flex items-center gap-2">
            <KeyRound className="h-5 w-5 text-slate-400" aria-hidden="true" />
            <p className="text-sm font-medium text-slate-900 dark:text-slate-100">2FA is currently off</p>
            <Badge tone="amber">Not configured</Badge>
          </div>
          <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
            Protect your account with a time-based one-time password (TOTP) app.
          </p>
          <Button className="mt-4" onClick={startSetup} loading={setup.isPending}>
            Set up 2FA
          </Button>
        </div>
      )}
    </Card>
  )
}

function SecretBox({ secret, multiline = false }) {
  const [copied, setCopied] = useState(false)
  async function copy() {
    try {
      await navigator.clipboard.writeText(secret)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch { /* clipboard unavailable */ }
  }
  return (
    <div className="mt-2 flex items-start gap-2">
      <pre className={`flex-1 overflow-x-auto rounded-md bg-slate-100 p-3 font-mono text-xs text-slate-800 dark:bg-slate-800 dark:text-slate-200 ${multiline ? 'whitespace-pre-wrap break-all' : ''}`}>
        {secret}
      </pre>
      <Button variant="secondary" size="icon" aria-label="Copy to clipboard" onClick={copy}>
        {copied ? <Check className="h-4 w-4 text-emerald-500" aria-hidden="true" /> : <Copy className="h-4 w-4" aria-hidden="true" />}
      </Button>
    </div>
  )
}
