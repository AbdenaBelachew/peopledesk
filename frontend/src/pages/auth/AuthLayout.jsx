import { Link, Outlet, useNavigate } from 'react-router-dom'
import { CalendarCheck, Wallet, Users } from 'lucide-react'

/** Split auth shell: navy gradient pitch panel + centered form column. */
export default function AuthLayout() {
  const navigate = useNavigate()
  return (
    <div className="flex min-h-screen bg-white dark:bg-slate-950">
      {/* Pitch panel */}
      <div className="relative hidden w-1/2 flex-col justify-between overflow-hidden bg-gradient-to-br from-[#0f172a] via-[#1e293b] to-[#0b1120] p-10 text-white lg:flex">
        <button
          type="button"
          onClick={() => navigate('/')}
          className="flex items-center gap-2.5 text-left"
          aria-label="Go to home"
        >
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-600 text-base font-bold">P</span>
          <span className="text-lg font-bold tracking-tight">
            People<span className="text-blue-400">Desk</span>
          </span>
        </button>

        <div className="max-w-md">
          <h1 className="text-3xl font-bold leading-tight tracking-tight">
            HR &amp; payroll that runs itself.
          </h1>
          <p className="mt-3 text-sm leading-relaxed text-slate-300">
            Employees, attendance, leave, payroll and payslips in one place — with self-service
            built in for every person on your team.
          </p>
          <ul className="mt-8 space-y-4">
            <li className="flex items-start gap-3">
              <span className="rounded-lg bg-white/10 p-2"><Users className="h-5 w-5 text-blue-400" aria-hidden="true" /></span>
              <div>
                <p className="text-sm font-semibold">Employee directory</p>
                <p className="text-xs text-slate-400">Profiles, documents, salary history and org structure.</p>
              </div>
            </li>
            <li className="flex items-start gap-3">
              <span className="rounded-lg bg-white/10 p-2"><CalendarCheck className="h-5 w-5 text-blue-400" aria-hidden="true" /></span>
              <div>
                <p className="text-sm font-semibold">Attendance &amp; leave</p>
                <p className="text-xs text-slate-400">Clock in/out, corrections, approvals and a shared calendar.</p>
              </div>
            </li>
            <li className="flex items-start gap-3">
              <span className="rounded-lg bg-white/10 p-2"><Wallet className="h-5 w-5 text-blue-400" aria-hidden="true" /></span>
              <div>
                <p className="text-sm font-semibold">Payroll in clicks</p>
                <p className="text-xs text-slate-400">Calculate, approve, finalize and publish payslips.</p>
              </div>
            </li>
          </ul>
        </div>

        <p className="text-xs text-slate-500">© {new Date().getFullYear()} PeopleDesk. All rights reserved.</p>
      </div>

      {/* Form column */}
      <div className="flex w-full items-center justify-center bg-slate-50 p-6 dark:bg-slate-950 lg:w-1/2">
        <div className="w-full max-w-md">
          <Link to="/login" className="mb-6 flex items-center justify-center gap-2.5 lg:hidden" aria-label="PeopleDesk home">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-600 text-sm font-bold text-white">P</span>
            <span className="text-base font-bold text-slate-900 dark:text-white">
              People<span className="text-blue-600 dark:text-blue-400">Desk</span>
            </span>
          </Link>
          <Outlet />
        </div>
      </div>
    </div>
  )
}
