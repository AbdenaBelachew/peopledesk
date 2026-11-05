import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { CheckCircle2, Info, AlertTriangle, XCircle, X } from 'lucide-react'
import { toastBus } from '../../lib/toastBus'

const ToastContext = createContext(null)

export function useToast() {
  return useContext(ToastContext)
}

const ICONS = {
  success: CheckCircle2,
  error: XCircle,
  warning: AlertTriangle,
  info: Info,
}

const COLORS = {
  success: 'text-emerald-500 border-l-emerald-500',
  error: 'text-red-500 border-l-red-500',
  warning: 'text-amber-500 border-l-amber-500',
  info: 'text-blue-500 border-l-blue-500',
}

let counter = 0

/** Toasts stack top-right and auto-dismiss after 4 seconds. */
export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([])

  const dismiss = useCallback((id) => {
    setToasts((t) => t.filter((x) => x.id !== id))
  }, [])

  const push = useCallback(
    (message, type = 'info') => {
      const id = ++counter
      setToasts((t) => [...t.slice(-4), { id, message: String(message), type }])
      setTimeout(() => dismiss(id), 4000)
    },
    [dismiss],
  )

  // Non-React modules (react-query caches) emit through this bridge.
  useEffect(() => {
    toastBus.set(push)
    return () => toastBus.set(null)
  }, [push])

  const api = useMemo(
    () => ({
      success: (m) => push(m, 'success'),
      error: (m) => push(m, 'error'),
      warning: (m) => push(m, 'warning'),
      info: (m) => push(m, 'info'),
    }),
    [push],
  )

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="fixed right-4 top-4 z-[100] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2" aria-live="polite">
        {toasts.map((t) => {
          const Icon = ICONS[t.type] || Info
          return (
            <div
              key={t.id}
              className={`anim-fade-in flex items-start gap-2.5 rounded-xl border border-l-4 border-slate-200 bg-white p-3 shadow-lg dark:border-slate-700 dark:bg-slate-800 ${(COLORS[t.type] || COLORS.info).split(' ')[1] || 'border-l-blue-500'}`}
              role="status"
            >
              <Icon className={`mt-0.5 h-5 w-5 shrink-0 ${(COLORS[t.type] || COLORS.info).split(' ')[0]}`} aria-hidden="true" />
              <p className="flex-1 text-sm text-slate-700 dark:text-slate-200">{t.message}</p>
              <button
                type="button"
                onClick={() => dismiss(t.id)}
                aria-label="Dismiss notification"
                className="rounded p-0.5 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          )
        })}
      </div>
    </ToastContext.Provider>
  )
}
