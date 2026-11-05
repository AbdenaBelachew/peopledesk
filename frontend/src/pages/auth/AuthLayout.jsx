import { Link, Outlet } from 'react-router-dom'

/** Clean centered auth shell: brand + tagline above a centered form card. */
export default function AuthLayout() {
  return (
    <div className="flex min-h-screen flex-col bg-slate-50 dark:bg-slate-950">
      <div className="flex flex-1 flex-col items-center justify-center px-6 py-10">
        <Link to="/" className="flex items-center gap-2.5" aria-label="PeopleDesk home">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-600 text-base font-bold text-white">P</span>
          <span className="text-lg font-bold tracking-tight text-slate-900 dark:text-white">
            People<span className="text-blue-600 dark:text-blue-400">Desk</span>
          </span>
        </Link>
        <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
          HR &amp; payroll that runs itself.
        </p>
        <div className="mt-8 w-full max-w-md">
          <Outlet />
        </div>
      </div>
      <p className="pb-6 text-center text-xs text-slate-400 dark:text-slate-500">
        © {new Date().getFullYear()} PeopleDesk. All rights reserved.
      </p>
    </div>
  )
}
