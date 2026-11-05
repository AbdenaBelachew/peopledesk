import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, publicApi, setAccessToken, clearAccessToken } from '../api/client'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState(null)
  const [bootstrapping, setBootstrapping] = useState(true)

  // Silent bootstrap: try refresh cookie -> /auth/me.
  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const { data } = await api.get('/auth/me')
        if (!cancelled) setUser(data)
      } catch {
        if (!cancelled) setUser(null)
      } finally {
        if (!cancelled) setBootstrapping(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const refreshUser = useCallback(async () => {
    const { data } = await api.get('/auth/me')
    setUser(data)
    return data
  }, [])

  const login = useCallback(
    async (email, password, code) => {
      const body = { email, password }
      if (code) body.code = code
      const { data } = await publicApi.post('/auth/login', body)
      if (data?.twoFactorRequired) return data // caller shows the 2FA code step
      setAccessToken(data.accessToken)
      await refreshUser()
      return data
    },
    [refreshUser],
  )

  const verify2fa = useCallback(
    async (pendingToken, code) => {
      const { data } = await publicApi.post('/auth/verify-2fa', { pendingToken, code })
      setAccessToken(data.accessToken)
      await refreshUser()
      return data
    },
    [refreshUser],
  )

  const register = useCallback(
    async (body) => {
      const { data } = await publicApi.post('/auth/register', body)
      setAccessToken(data.accessToken)
      await refreshUser()
      return data
    },
    [refreshUser],
  )

  const logout = useCallback(async () => {
    try {
      await api.post('/auth/logout?all=false')
    } catch {
      // best-effort: clear local state regardless
    }
    clearAccessToken()
    setUser(null)
    try { localStorage.removeItem('pd_onboarding_pending') } catch { /* ignore */ }
    queryClient.clear()
  }, [queryClient])

  const hasPerm = useCallback(
    (code) =>
      !!user && (user.isPlatformAdmin || (user.permissions || []).includes(code)),
    [user],
  )

  const hasAnyPerm = useCallback(
    (codes) =>
      !!user &&
      (user.isPlatformAdmin || (codes || []).some((c) => (user.permissions || []).includes(c))),
    [user],
  )

  const value = useMemo(
    () => ({ user, bootstrapping, login, verify2fa, register, logout, refreshUser, hasPerm, hasAnyPerm }),
    [user, bootstrapping, login, verify2fa, register, logout, refreshUser, hasPerm, hasAnyPerm],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  return useContext(AuthContext)
}
