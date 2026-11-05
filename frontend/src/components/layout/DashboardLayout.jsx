import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  LayoutDashboard, Users, Clock, CalendarDays, Wallet, FileText, BarChart3,
  Settings, Shield, Bell, Sun, Moon, Menu, LogOut, X, ChevronLeft, CheckCheck,
} from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { useUnreadCount, useNotifications, useMarkNotificationRead, useMarkAllNotificationsRead } from '../../api/queries'
import { useToast } from '../ui/Toast'
import Avatar from '../ui/Avatar'
import Badge from '../ui/Badge'
import Button from '../ui/Button'
import { formatDateTime } from '../../lib/format'
import { isDark, setTheme } from '../../lib/theme'

// Sidebar navigation, filtered by permissions.
const NAV = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/employees', label: 'Employees', icon: Users, perm: 'employees.view' },
  { to: '/attendance', label: 'Attendance', icon: Clock, perm: 'attendance.viewOwn' },
  { to: '/leave', label: 'Leave', icon: CalendarDays, perm: 'leave.viewOwn' },
  { to: '/payroll', label: 'Payroll', icon: Wallet, perm: 'payroll.view' },
  { to: '/payslips', label: 'Payslips', icon: FileText, perm: 'payslips.viewOwn' },
  { to: '/reports', label: 'Reports', icon: BarChart3, perm: 'reports.view' },
  {
    to: '/settings',
    label: 'Settings',
    icon: Settings,
    perms: ['org.update', 'settings.manage', 'structure.view', 'leave.manage', 'announcements.manage', 'users.manage'],
  },
  { to: '/platform', label: 'Platform', icon: Shield, platformOnly: true },
]

function Brand({ collapsed }) {
  return (
    <Link to="/" className="flex items-center gap-2.5 px-4 py-4" aria-label="PeopleDesk home">
      <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-sm font-bold text-white">P</span>
      {!collapsed && (
        <span className="text-base font-bold tracking-tight text-white">
          People<span className="text-blue-400">Desk</span>
        </span>
      )}
    </Link>
  )
}

function NavItems({ collapsed, onNavigate, user }) {
  return (
    <nav className="flex-1 space-y-0.5 overflow-y-auto px-2 py-2" aria-label="Main navigation">
      {NAV.map((item) => {
        if (item.platformOnly && !user?.isPlatformAdmin) return null
        if (item.perm && !user?.isPlatformAdmin && !(user?.permissions || []).includes(item.perm)) return null
        if (item.perms && !user?.isPlatformAdmin && !item.perms.some((c) => (user?.permissions || []).includes(c))) return null
        const Icon = item.icon
        return (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.end}
            onClick={onNavigate}
            title={collapsed ? item.label : undefined}
            className={({ isActive }) =>
              `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
                isActive
                  ? 'bg-white/10 text-white'
                  : 'text-slate-400 hover:bg-white/5 hover:text-slate-100'
              } ${collapsed ? 'justify-center px-0' : ''}`
            }
          >
            <Icon className="h-5 w-5 shrink-0" aria-hidden="true" />
            {!collapsed && item.label}
          </NavLink>
        )
      })}
    </nav>
  )
}

function SidebarBody({ collapsed, onNavigate, user, mobile = false }) {
  return (
    <div className="flex h-full flex-col bg-[#0f172a]">
      <Brand collapsed={collapsed} />
      {mobile && user && (
        <div className="mx-4 mb-2 rounded-lg bg-white/5 px-3 py-2">
          <p className="truncate text-sm font-medium text-white">{user.organizationName || 'Platform'}</p>
          <p className="truncate text-xs text-slate-400">{user.email}</p>
        </div>
      )}
      <NavItems collapsed={collapsed} onNavigate={onNavigate} user={user} />
      {user?.subscription && !collapsed && (
        <div className="border-t border-white/10 px-4 py-3">
          <p className="text-[11px] uppercase tracking-wide text-slate-500">Plan</p>
          <div className="mt-1 flex items-center gap-2">
            <span className="text-sm font-medium text-slate-200">{user.subscription.planName}</span>
            <Badge tone={user.subscription.status === 'Active' ? 'green' : 'amber'} className="!text-[10px]">
              {user.subscription.status}
            </Badge>
          </div>
        </div>
      )}
    </div>
  )
}

function Dropdown({ button, children, align = 'right', width = 'w-72' }) {
  const [open, setOpen] = useState(false)
  const ref = useRef(null)
  useEffect(() => {
    if (!open) return undefined
    const onClick = (e) => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false)
    }
    const onKey = (e) => e.key === 'Escape' && setOpen(false)
    document.addEventListener('mousedown', onClick)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onClick)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])
  return (
    <div className="relative" ref={ref}>
      {button({ open, toggle: () => setOpen((o) => !o) })}
      {open && (
        <div
          className={`anim-scale-in absolute ${align === 'right' ? 'right-0' : 'left-0'} mt-2 ${width} rounded-xl border border-slate-200 bg-white shadow-lg dark:border-slate-700 dark:bg-slate-900`}
        >
          {children({ close: () => setOpen(false) })}
        </div>
      )}
    </div>
  )
}

function NotificationBell() {
  const { data: unread } = useUnreadCount(true)
  const { data, isLoading } = useNotifications({ page: 1, pageSize: 8 })
  const markRead = useMarkNotificationRead()
  const markAll = useMarkAllNotificationsRead()
  const navigate = useNavigate()
  const toast = useToast()
  const count = Number(unread) || 0

  return (
    <Dropdown
      width="w-80"
      button={({ toggle }) => (
        <button
          type="button"
          onClick={toggle}
          aria-label={`Notifications${count ? `, ${count} unread` : ''}`}
          className="relative rounded-md p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-slate-400 dark:hover:bg-slate-800"
        >
          <Bell className="h-5 w-5" aria-hidden="true" />
          {count > 0 && (
            <span className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold text-white">
              {count > 99 ? '99+' : count}
            </span>
          )}
        </button>
      )}
    >
      {({ close }) => (
        <div>
          <div className="flex items-center justify-between border-b border-slate-100 px-3 py-2 dark:border-slate-800">
            <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">Notifications</p>
            <button
              type="button"
              className="inline-flex items-center gap-1 text-xs text-blue-600 hover:underline dark:text-blue-400"
              onClick={() => markAll.mutate(undefined, { onError: (e) => toast.error(e.message) })}
            >
              <CheckCheck className="h-3.5 w-3.5" aria-hidden="true" /> Mark all read
            </button>
          </div>
          <div className="max-h-80 overflow-y-auto">
            {isLoading ? (
              <p className="px-3 py-6 text-center text-sm text-slate-400">Loading…</p>
            ) : !data?.items?.length ? (
              <p className="px-3 py-6 text-center text-sm text-slate-400">No notifications yet.</p>
            ) : (
              data.items.map((n) => (
                <button
                  key={n.id}
                  type="button"
                  onClick={() => {
                    if (!n.readAtUtc) markRead.mutate(n.id)
                    if (n.link) navigate(n.link)
                    close()
                  }}
                  className={`block w-full border-b border-slate-50 px-3 py-2.5 text-left hover:bg-slate-50 dark:border-slate-800/60 dark:hover:bg-slate-800/60 ${!n.readAtUtc ? 'bg-blue-50/50 dark:bg-blue-900/10' : ''}`}
                >
                  <p className="flex items-center gap-1.5 text-sm font-medium text-slate-800 dark:text-slate-200">
                    {!n.readAtUtc && <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-blue-500" aria-hidden="true" />}
                    {n.title}
                  </p>
                  {n.body && <p className="mt-0.5 line-clamp-2 text-xs text-slate-500 dark:text-slate-400">{n.body}</p>}
                  <p className="mt-0.5 text-[11px] text-slate-400">{formatDateTime(n.createdAtUtc)}</p>
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </Dropdown>
  )
}

function DarkModeToggle() {
  const [dark, setDark] = useState(isDark())
  return (
    <button
      type="button"
      aria-label="Toggle dark mode"
      onClick={() => {
        const next = !dark
        setDark(next)
        setTheme(next)
      }}
      className="rounded-md p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-slate-400 dark:hover:bg-slate-800"
    >
      {dark ? <Sun className="h-5 w-5" aria-hidden="true" /> : <Moon className="h-5 w-5" aria-hidden="true" />}
    </button>
  )
}

function UserMenu() {
  const { user, logout } = useAuth()
  const toast = useToast()
  const navigate = useNavigate()
  if (!user) return null
  const name = `${user.firstName || ''} ${user.lastName || ''}`.trim() || user.email

  return (
    <Dropdown
      width="w-56"
      button={({ toggle }) => (
        <button
          type="button"
          onClick={toggle}
          aria-label="User menu"
          className="flex items-center gap-2 rounded-md p-1 pr-2 hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:hover:bg-slate-800"
        >
          <Avatar name={name} size="sm" />
          <span className="hidden max-w-28 truncate text-sm font-medium text-slate-700 dark:text-slate-200 sm:block">
            {user.firstName || user.email}
          </span>
        </button>
      )}
    >
      {({ close }) => (
        <div className="py-1">
          <div className="border-b border-slate-100 px-3 py-2 dark:border-slate-800">
            <p className="truncate text-sm font-medium text-slate-900 dark:text-slate-100">{name}</p>
            <p className="truncate text-xs text-slate-500 dark:text-slate-400">{user.email}</p>
            {user.organizationName && (
              <p className="mt-1 truncate text-xs text-slate-400 dark:text-slate-500">{user.organizationName}</p>
            )}
          </div>
          <button
            type="button"
            onClick={() => {
              close()
              navigate('/settings/security')
            }}
            className="block w-full px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            Account security
          </button>
          <button
            type="button"
            onClick={async () => {
              close()
              await logout()
              navigate('/login')
            }}
            className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
          >
            <LogOut className="h-4 w-4" aria-hidden="true" /> Sign out
          </button>
        </div>
      )}
    </Dropdown>
  )
}

/** App shell: navy sidebar (collapsible / mobile drawer), sticky topbar, content outlet. */
export default function DashboardLayout() {
  const { user } = useAuth()
  const location = useLocation()
  const [collapsed, setCollapsed] = useState(() => {
    try { return localStorage.getItem('pd_sidebar') === 'collapsed' } catch { return false }
  })
  const [mobileOpen, setMobileOpen] = useState(false)

  useEffect(() => {
    try { localStorage.setItem('pd_sidebar', collapsed ? 'collapsed' : 'open') } catch { /* ignore */ }
  }, [collapsed])

  return (
    <div className="min-h-screen">
      {/* Desktop sidebar */}
      <aside
        className={`fixed inset-y-0 left-0 z-40 hidden md:flex md:flex-col transition-[width] duration-200 ${collapsed ? 'w-16' : 'w-64'}`}
      >
        <SidebarBody collapsed={collapsed} user={user} />
        <button
          type="button"
          onClick={() => setCollapsed((c) => !c)}
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          className="absolute -right-3 top-16 hidden h-6 w-6 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 shadow-md hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-400 dark:hover:text-slate-200 md:flex"
        >
          <ChevronLeft className={`h-3.5 w-3.5 transition-transform ${collapsed ? 'rotate-180' : ''}`} aria-hidden="true" />
        </button>
      </aside>

      {/* Mobile drawer */}
      {mobileOpen && (
        <div className="fixed inset-0 z-50 md:hidden">
          <div className="absolute inset-0 bg-slate-900/60" onClick={() => setMobileOpen(false)} aria-hidden="true" />
          <aside className="absolute inset-y-0 left-0 w-64 shadow-xl">
            <button
              type="button"
              onClick={() => setMobileOpen(false)}
              aria-label="Close menu"
              className="absolute right-2 top-4 z-10 rounded-md p-1.5 text-slate-400 hover:bg-white/10"
            >
              <X className="h-5 w-5" />
            </button>
            <SidebarBody collapsed={false} user={user} mobile onNavigate={() => setMobileOpen(false)} />
          </aside>
        </div>
      )}

      <div className={`flex min-h-screen flex-col transition-[padding] duration-200 ${collapsed ? 'md:pl-16' : 'md:pl-64'}`}>
        <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b border-slate-200 bg-white px-3 dark:border-slate-800 dark:bg-slate-900 sm:px-4">
          <button
            type="button"
            onClick={() => setMobileOpen(true)}
            aria-label="Open menu"
            className="rounded-md p-2 text-slate-500 hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-slate-400 dark:hover:bg-slate-800 md:hidden"
          >
            <Menu className="h-5 w-5" aria-hidden="true" />
          </button>
          <Link to="/" className="truncate text-sm font-semibold text-slate-900 dark:text-slate-100">
            {user?.organizationName || 'PeopleDesk'}
          </Link>
          <div className="ml-auto flex items-center gap-1">
            <NotificationBell />
            <DarkModeToggle />
            <UserMenu />
          </div>
        </header>
        <main className="mx-auto w-full max-w-7xl flex-1 p-4 sm:p-6">
          <div key={location.pathname} className="anim-fade-up">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
