import { NavLink, Outlet } from 'react-router-dom'
import {
  Building2, SlidersHorizontal, Network, GitBranch, Briefcase, Clock4,
  CalendarOff, Palmtree, Megaphone, UsersRound, ShieldCheck,
} from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'

const SECTIONS = [
  { to: '/settings/organization', label: 'Organization profile', icon: Building2, perm: 'org.update' },
  { to: '/settings/general', label: 'General settings', icon: SlidersHorizontal, perm: 'settings.manage' },
  { to: '/settings/departments', label: 'Departments', icon: Network, perm: 'structure.view' },
  { to: '/settings/branches', label: 'Branches', icon: GitBranch, perm: 'structure.view' },
  { to: '/settings/positions', label: 'Positions', icon: Briefcase, perm: 'structure.view' },
  { to: '/settings/employment-types', label: 'Employment types', icon: Clock4, perm: 'structure.view' },
  { to: '/settings/schedules', label: 'Work schedules', icon: CalendarOff, perm: 'settings.manage' },
  { to: '/settings/holidays', label: 'Holidays', icon: Palmtree, perm: 'settings.manage' },
  { to: '/settings/leave-types', label: 'Leave types', icon: Palmtree, perm: 'leave.manage', icon2: true },
  { to: '/settings/announcements', label: 'Announcements', icon: Megaphone, perm: 'announcements.manage' },
  { to: '/settings/users', label: 'Users', icon: UsersRound, perm: 'users.manage' },
  { to: '/settings/security', label: 'Security (2FA)', icon: ShieldCheck, perm: null }, // always
]

export default function SettingsLayout() {
  const { user } = useAuth()
  const visible = SECTIONS.filter(
    (s) =>
      s.perm === null ||
      user?.isPlatformAdmin ||
      (user?.permissions || []).includes(s.perm),
  )

  return (
    <div>
      <h1 className="mb-4 text-xl font-bold tracking-tight text-slate-900 dark:text-slate-100">Settings</h1>
      <div className="flex flex-col gap-6 lg:flex-row">
        <nav className="lg:w-56 lg:shrink-0" aria-label="Settings sections">
          <ul className="flex gap-1 overflow-x-auto lg:flex-col lg:overflow-visible">
            {visible.map((s) => {
              const Icon = s.icon
              return (
                <li key={s.to} className="shrink-0">
                  <NavLink
                    to={s.to}
                    className={({ isActive }) =>
                      `flex items-center gap-2.5 whitespace-nowrap rounded-md px-3 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
                        isActive
                          ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'
                          : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
                      }`
                    }
                  >
                    <Icon className="h-4 w-4" aria-hidden="true" />
                    {s.label}
                  </NavLink>
                </li>
              )
            })}
          </ul>
        </nav>
        <div className="min-w-0 flex-1">
          <Outlet />
        </div>
      </div>
    </div>
  )
}
