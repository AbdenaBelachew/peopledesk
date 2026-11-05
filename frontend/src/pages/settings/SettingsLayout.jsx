import { NavLink, Outlet } from 'react-router-dom'
import {
  Building2, SlidersHorizontal, Network, GitBranch, Briefcase, Clock4,
  CalendarClock, Palmtree, Gift, Megaphone, UsersRound, ShieldCheck,
} from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import PageHeader from '../../components/ui/PageHeader'

// Settings navigation, grouped the way admins think about them.
const GROUPS = [
  {
    label: 'Organization',
    items: [
      { to: '/settings/organization', label: 'Organization profile', icon: Building2, perm: 'org.update' },
      { to: '/settings/general', label: 'General settings', icon: SlidersHorizontal, perm: 'settings.manage' },
    ],
  },
  {
    label: 'Structure',
    items: [
      { to: '/settings/departments', label: 'Departments', icon: Network, perm: 'structure.view' },
      { to: '/settings/branches', label: 'Branches', icon: GitBranch, perm: 'structure.view' },
      { to: '/settings/positions', label: 'Positions', icon: Briefcase, perm: 'structure.view' },
      { to: '/settings/employment-types', label: 'Employment types', icon: Clock4, perm: 'structure.view' },
    ],
  },
  {
    label: 'Calendar & leave',
    items: [
      { to: '/settings/schedules', label: 'Work schedules', icon: CalendarClock, perm: 'settings.manage' },
      { to: '/settings/holidays', label: 'Holidays', icon: Palmtree, perm: 'settings.manage' },
      { to: '/settings/leave-types', label: 'Leave types', icon: Gift, perm: 'leave.manage' },
    ],
  },
  {
    label: 'Communication',
    items: [
      { to: '/settings/announcements', label: 'Announcements', icon: Megaphone, perm: 'announcements.manage' },
    ],
  },
  {
    label: 'Access',
    items: [
      { to: '/settings/users', label: 'Users', icon: UsersRound, perm: 'users.manage' },
      { to: '/settings/security', label: 'Security (2FA)', icon: ShieldCheck, perm: null }, // always visible
    ],
  },
]

export default function SettingsLayout() {
  const { user } = useAuth()
  const allowed = (perm) =>
    perm === null || user?.isPlatformAdmin || (user?.permissions || []).includes(perm)

  const groups = GROUPS
    .map((g) => ({ ...g, items: g.items.filter((i) => allowed(i.perm)) }))
    .filter((g) => g.items.length > 0)
  const flat = groups.flatMap((g) => g.items)

  const itemClass = ({ isActive }) =>
    `flex items-center gap-2.5 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
      isActive
        ? 'bg-blue-50 text-blue-700 dark:bg-blue-500/10 dark:text-blue-300'
        : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:hover:text-slate-200'
    }`

  return (
    <div>
      <PageHeader title="Settings" subtitle="Manage your organization's configuration and workspace." />

      <div className="flex flex-col gap-6 lg:flex-row lg:gap-10">
        {/* Mobile: horizontal pill strip */}
        <nav className="lg:hidden" aria-label="Settings sections">
          <ul className="-mx-1 flex gap-1.5 overflow-x-auto px-1 pb-1">
            {flat.map((s) => {
              const Icon = s.icon
              return (
                <li key={s.to} className="shrink-0">
                  <NavLink
                    to={s.to}
                    className={({ isActive }) =>
                      `flex items-center gap-1.5 whitespace-nowrap rounded-full border px-3 py-1.5 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
                        isActive
                          ? 'border-blue-600 bg-blue-600 text-white'
                          : 'border-slate-300 bg-white text-slate-600 hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300 dark:hover:bg-slate-800'
                      }`
                    }
                  >
                    <Icon className="h-3.5 w-3.5" aria-hidden="true" />
                    {s.label}
                  </NavLink>
                </li>
              )
            })}
          </ul>
        </nav>

        {/* Desktop: grouped sticky sidebar */}
        <nav className="hidden lg:block lg:w-60 lg:shrink-0" aria-label="Settings sections">
          <div className="sticky top-20 space-y-5">
            {groups.map((g) => (
              <div key={g.label}>
                <p className="mb-1 px-3 text-[11px] font-semibold uppercase tracking-wider text-slate-400 dark:text-slate-500">
                  {g.label}
                </p>
                <ul className="space-y-0.5">
                  {g.items.map((s) => {
                    const Icon = s.icon
                    return (
                      <li key={s.to}>
                        <NavLink to={s.to} className={itemClass}>
                          <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
                          {s.label}
                        </NavLink>
                      </li>
                    )
                  })}
                </ul>
              </div>
            ))}
          </div>
        </nav>

        <div className="min-w-0 flex-1">
          <Outlet />
        </div>
      </div>
    </div>
  )
}
