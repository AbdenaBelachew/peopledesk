import { useEffect } from 'react'
import { BrowserRouter, Routes, Route, Navigate, Link } from 'react-router-dom'
import { QueryClient, QueryCache, MutationCache, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from './auth/AuthContext'
import { RequireAuth, RequirePermission, RequirePlatform } from './auth/RequireAuth'
import { ToastProvider } from './components/ui/Toast'
import { toastBus } from './lib/toastBus'
import { apiErrorMessage } from './api/client'
import { initTheme } from './lib/theme'

import DashboardLayout from './components/layout/DashboardLayout'
import Login from './pages/auth/Login'
import Register from './pages/auth/Register'
import ForgotPassword from './pages/auth/ForgotPassword'
import ResetPassword from './pages/auth/ResetPassword'
import VerifyEmail from './pages/auth/VerifyEmail'
import AuthLayout from './pages/auth/AuthLayout'
import Onboarding from './pages/Onboarding'
import Dashboard from './pages/Dashboard'
import EmployeeList from './pages/employees/EmployeeList'
import EmployeeForm from './pages/employees/EmployeeForm'
import EmployeeDetail from './pages/employees/EmployeeDetail'
import AttendanceToday from './pages/attendance/AttendanceToday'
import Timesheets from './pages/attendance/Timesheets'
import Corrections from './pages/attendance/Corrections'
import LeaveCalendar from './pages/leave/LeaveCalendar'
import LeaveApprovals from './pages/leave/LeaveApprovals'
import MyLeave from './pages/leave/MyLeave'
import PayrollPeriods from './pages/payroll/PayrollPeriods'
import PayrollRunDetail from './pages/payroll/PayrollRunDetail'
import Payslips from './pages/payslips/Payslips'
import Reports from './pages/Reports'
import SettingsLayout from './pages/settings/SettingsLayout'
import OrganizationProfile from './pages/settings/OrganizationProfile'
import OrgSettings from './pages/settings/OrgSettings'
import StructurePage from './pages/settings/StructurePage'
import WorkSchedules from './pages/settings/WorkSchedules'
import Holidays from './pages/settings/Holidays'
import LeaveTypes from './pages/settings/LeaveTypes'
import Announcements from './pages/settings/Announcements'
import Users from './pages/settings/Users'
import TwoFactor from './pages/settings/TwoFactor'
import Platform from './pages/Platform'

// Single place for global react-query error toasts (422 business rules, validation, etc).
const onError = async (error) => {
  if (error?.response?.status === 401) return // auth redirect handles it
  toastBus.emit(await apiErrorMessage(error))
}

const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError }),
  mutationCache: new MutationCache({ onError }),
  defaultOptions: {
    queries: { refetchOnWindowFocus: false, retry: 1, staleTime: 15_000 },
  },
})

function NotFound() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-slate-50 p-6 dark:bg-slate-950">
      <p className="text-6xl font-bold text-blue-600">404</p>
      <h1 className="mt-2 text-lg font-semibold text-slate-900 dark:text-slate-100">Page not found</h1>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">The page you are looking for does not exist.</p>
      <Link to="/" className="mt-4 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700">
        Back to dashboard
      </Link>
    </div>
  )
}

export default function App() {
  useEffect(() => {
    initTheme()
  }, [])

  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthProvider>
          <BrowserRouter>
            <Routes>
              {/* Public (redirects to / when already signed in) */}
              <Route element={<AuthLayout />}>
                <Route path="/login" element={<Login />} />
                <Route path="/register" element={<Register />} />
                <Route path="/forgot-password" element={<ForgotPassword />} />
              </Route>
              <Route path="/reset-password" element={<ResetPassword />} />
              <Route path="/verify-email" element={<VerifyEmail />} />

              {/* First-run wizard */}
              <Route
                path="/onboarding"
                element={
                  <RequireAuth>
                    <Onboarding />
                  </RequireAuth>
                }
              />

              {/* App */}
              <Route
                element={
                  <RequireAuth>
                    <DashboardLayout />
                  </RequireAuth>
                }
              >
                <Route path="/" element={<Dashboard />} />
                <Route
                  path="/employees"
                  element={
                    <RequirePermission perm="employees.view">
                      <EmployeeList />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/employees/new"
                  element={
                    <RequirePermission perm="employees.create">
                      <EmployeeForm />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/employees/:id"
                  element={
                    <RequirePermission perm="employees.view">
                      <EmployeeDetail />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/employees/:id/edit"
                  element={
                    <RequirePermission perm="employees.update">
                      <EmployeeForm />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/attendance"
                  element={
                    <RequirePermission perm="attendance.viewOwn">
                      <AttendanceToday />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/attendance/timesheets"
                  element={
                    <RequirePermission perm="attendance.viewOwn">
                      <Timesheets />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/attendance/corrections"
                  element={
                    <RequirePermission perm="attendance.viewOwn">
                      <Corrections />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/leave"
                  element={
                    <RequirePermission perm="leave.viewOwn">
                      <LeaveCalendar />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/leave/calendar"
                  element={
                    <RequirePermission perm="leave.viewOwn">
                      <LeaveCalendar />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/leave/approvals"
                  element={
                    <RequirePermission perm="leave.viewAll">
                      <LeaveApprovals />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/leave/my"
                  element={
                    <RequirePermission perm="leave.viewOwn">
                      <MyLeave />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/payroll"
                  element={
                    <RequirePermission perm="payroll.view">
                      <PayrollPeriods />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/payroll/runs/:id"
                  element={
                    <RequirePermission perm="payroll.view">
                      <PayrollRunDetail />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/payslips"
                  element={
                    <RequirePermission perm="payslips.viewOwn">
                      <Payslips />
                    </RequirePermission>
                  }
                />
                <Route
                  path="/reports"
                  element={
                    <RequirePermission perm="reports.view">
                      <Reports />
                    </RequirePermission>
                  }
                />

                {/* Settings */}
                <Route
                  path="/settings"
                  element={
                    <RequirePermission
                      perms={['org.update', 'settings.manage', 'structure.view', 'leave.manage', 'announcements.manage', 'users.manage']}
                    >
                      <SettingsLayout />
                    </RequirePermission>
                  }
                >
                  <Route index element={<Navigate to="/settings/organization" replace />} />
                  <Route
                    path="organization"
                    element={
                      <RequirePermission perm="org.update">
                        <OrganizationProfile />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="general"
                    element={
                      <RequirePermission perm="settings.manage">
                        <OrgSettings />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="departments"
                    element={
                      <RequirePermission perm="structure.view">
                        <StructurePage resource="departments" />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="branches"
                    element={
                      <RequirePermission perm="structure.view">
                        <StructurePage resource="branches" />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="positions"
                    element={
                      <RequirePermission perm="structure.view">
                        <StructurePage resource="positions" />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="employment-types"
                    element={
                      <RequirePermission perm="structure.view">
                        <StructurePage resource="employment-types" />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="schedules"
                    element={
                      <RequirePermission perm="settings.manage">
                        <WorkSchedules />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="holidays"
                    element={
                      <RequirePermission perm="settings.manage">
                        <Holidays />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="leave-types"
                    element={
                      <RequirePermission perm="leave.manage">
                        <LeaveTypes />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="announcements"
                    element={
                      <RequirePermission perm="announcements.manage">
                        <Announcements />
                      </RequirePermission>
                    }
                  />
                  <Route
                    path="users"
                    element={
                      <RequirePermission perm="users.manage">
                        <Users />
                      </RequirePermission>
                    }
                  />
                  <Route path="security" element={<TwoFactor />} />
                </Route>

                {/* Platform super-admin console */}
                <Route
                  path="/platform"
                  element={
                    <RequirePlatform>
                      <Platform />
                    </RequirePlatform>
                  }
                />
              </Route>

              <Route path="*" element={<NotFound />} />
            </Routes>
          </BrowserRouter>
        </AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
  )
}
