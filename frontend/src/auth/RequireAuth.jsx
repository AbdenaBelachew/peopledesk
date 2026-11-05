import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { FullPageSpinner } from '../components/ui/Spinner'
import Card from '../components/ui/Card'
import { ShieldOff } from 'lucide-react'

/** Blocks rendering until the session bootstrap (refresh -> /auth/me) resolves. */
export function RequireAuth({ children }) {
  const { user, bootstrapping } = useAuth()
  const location = useLocation()

  if (bootstrapping) return <FullPageSpinner />
  if (!user) {
    return <Navigate to="/login" state={{ from: location.pathname + location.search }} replace />
  }
  // First-run wizard enforcement after registration.
  try {
    if (localStorage.getItem('pd_onboarding_pending') === '1' && !location.pathname.startsWith('/onboarding')) {
      return <Navigate to="/onboarding" replace />
    }
  } catch { /* ignore */ }
  return children
}

/** Permission gate: hides content (shows a friendly card) when the code is missing. */
export function RequirePermission({ perm, perms, children }) {
  const { hasPerm, hasAnyPerm } = useAuth()
  const allowed = perm ? hasPerm(perm) : hasAnyPerm(perms || [])
  if (!allowed) {
    return (
      <Card className="mx-auto max-w-lg text-center">
        <div className="py-8">
          <ShieldOff className="mx-auto h-10 w-10 text-slate-400" aria-hidden="true" />
          <h2 className="mt-3 text-base font-semibold text-slate-900 dark:text-slate-100">Access denied</h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            You do not have permission to view this page. Contact your organization owner if you
            believe this is a mistake.
          </p>
        </div>
      </Card>
    )
  }
  return children
}

/** Platform console gate: only me.isPlatformAdmin. */
export function RequirePlatform({ children }) {
  const { user } = useAuth()
  if (!user?.isPlatformAdmin) {
    return (
      <Card className="mx-auto max-w-lg text-center">
        <div className="py-8">
          <ShieldOff className="mx-auto h-10 w-10 text-slate-400" aria-hidden="true" />
          <h2 className="mt-3 text-base font-semibold text-slate-900 dark:text-slate-100">Platform access only</h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            This console is restricted to PeopleDesk platform administrators.
          </p>
        </div>
      </Card>
    )
  }
  return children
}
