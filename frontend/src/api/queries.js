// React Query hooks, grouped per module. Query keys follow ["module", ...scope].
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query'
import { api } from './client'

const qc = () => useQueryClient()

// Drop empty params so URLs stay clean.
function clean(params) {
  const out = {}
  Object.entries(params || {}).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== '') out[k] = v
  })
  return out
}

const paged = (res) => res.data // { items, page, pageSize, totalCount, totalPages, ... }

// ===========================================================================
// Organizations
// ===========================================================================

export function useOrgDashboard() {
  return useQuery({
    queryKey: ['org', 'dashboard'],
    queryFn: async () => (await api.get('/organizations/dashboard')).data,
  })
}

export function useOrg(enabled = true) {
  return useQuery({
    queryKey: ['org', 'current'],
    queryFn: async () => (await api.get('/organizations/current')).data,
    enabled,
  })
}

export function useUpdateOrg() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.put('/organizations/current', body)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['org'] })
    },
  })
}

export function useOrgSettings() {
  return useQuery({
    queryKey: ['org', 'settings'],
    queryFn: async () => (await api.get('/organizations/settings')).data,
  })
}

export function useUpdateOrgSettings() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.put('/organizations/settings', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['org', 'settings'] }),
  })
}

// Generic structure CRUD: departments | branches | positions | employment-types
export function useStructure(resource, options = {}) {
  return useQuery({
    queryKey: ['org', 'structure', resource],
    queryFn: async () => (await api.get(`/organizations/${resource}`)).data,
    ...options,
  })
}

export function useSaveStructure(resource) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => {
      if (body.id) return (await api.put(`/organizations/${resource}/${body.id}`, body)).data
      return (await api.post(`/organizations/${resource}`, body)).data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['org', 'structure', resource] }),
  })
}

export function useDeleteStructure(resource) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (id) => api.delete(`/organizations/${resource}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['org', 'structure', resource] }),
  })
}

export function useWorkSchedules() {
  return useQuery({
    queryKey: ['org', 'schedules'],
    queryFn: async () => (await api.get('/organizations/work-schedules')).data,
  })
}

export function useSaveWorkSchedule() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/organizations/work-schedules', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['org', 'schedules'] }),
  })
}

export function useHolidays(year) {
  return useQuery({
    queryKey: ['org', 'holidays', year],
    queryFn: async () => (await api.get('/organizations/holidays', { params: clean({ year }) })).data,
    enabled: !!year,
  })
}

export function useSaveHoliday() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/organizations/holidays', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['org', 'holidays'] }),
  })
}

export function useDeleteHoliday() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (id) => api.delete(`/organizations/holidays/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['org', 'holidays'] }),
  })
}

export function useAnnouncements(includeExpired = false) {
  return useQuery({
    queryKey: ['org', 'announcements', includeExpired],
    queryFn: async () => (await api.get('/organizations/announcements', { params: clean({ includeExpired }) })).data,
  })
}

export function useSaveAnnouncement() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/organizations/announcements', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['org', 'announcements'] }),
  })
}

// ===========================================================================
// Employees
// ===========================================================================

export function useEmployees(params = {}, options = {}) {
  return useQuery({
    queryKey: ['employees', clean(params)],
    queryFn: async () => paged(await api.get('/employees', { params: clean(params) })),
    placeholderData: keepPreviousData,
    ...options,
  })
}

export function useMyEmployee(enabled = true) {
  return useQuery({
    queryKey: ['employee', 'me'],
    queryFn: async () => (await api.get('/employees/me')).data,
    enabled,
  })
}

export function useEmployee(id, enabled = true) {
  return useQuery({
    queryKey: ['employee', id],
    queryFn: async () => (await api.get(`/employees/${id}`)).data,
    enabled: !!id && enabled,
  })
}

export function useCreateEmployee() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/employees', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employees'] }),
  })
}

export function useUpdateEmployee() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, ...body }) => (await api.put(`/employees/${id}`, body)).data,
    onSuccess: (_data, vars) => {
      queryClient.invalidateQueries({ queryKey: ['employees'] })
      queryClient.invalidateQueries({ queryKey: ['employee', vars.id] })
    },
  })
}

export function useOffboardEmployee() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, ...body }) => (await api.post(`/employees/${id}/offboard`, body)).data,
    onSuccess: (_data, vars) => {
      queryClient.invalidateQueries({ queryKey: ['employees'] })
      queryClient.invalidateQueries({ queryKey: ['employee', vars.id] })
    },
  })
}

export function useDeleteEmployee() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (id) => api.delete(`/employees/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employees'] }),
  })
}

export function useEmployeeContacts(id) {
  return useQuery({
    queryKey: ['employee', id, 'contacts'],
    queryFn: async () => (await api.get(`/employees/${id}/contacts`)).data,
    enabled: !!id,
  })
}

export function useSaveContact(id) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => {
      if (body.id) return (await api.put(`/employees/${id}/contacts/${body.id}`, body)).data
      return (await api.post(`/employees/${id}/contacts`, body)).data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employee', id, 'contacts'] }),
  })
}

export function useDeleteContact(id) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (contactId) => api.delete(`/employees/${id}/contacts/${contactId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employee', id, 'contacts'] }),
  })
}

export function useEmployeeDocuments(id) {
  return useQuery({
    queryKey: ['employee', id, 'documents'],
    queryFn: async () => (await api.get(`/employees/${id}/documents`)).data,
    enabled: !!id,
  })
}

export function useUploadDocument(id) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ file, kind, name }) => {
      const form = new FormData()
      form.append('file', file)
      form.append('kind', kind)
      form.append('name', name)
      return (await api.post(`/employees/${id}/documents`, form)).data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employee', id, 'documents'] }),
  })
}

export function useDeleteDocument(id) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (docId) => api.delete(`/employees/${id}/documents/${docId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employee', id, 'documents'] }),
  })
}

export function useEmployeeHistory(id) {
  return useQuery({
    queryKey: ['employee', id, 'history'],
    queryFn: async () => (await api.get(`/employees/${id}/history`)).data,
    enabled: !!id,
  })
}

// Salary structures (employees.viewSensitive)
export function useSalaryStructures(id, enabled = true) {
  return useQuery({
    queryKey: ['employee', id, 'salary'],
    queryFn: async () => (await api.get(`/employees/${id}/salary-structures`)).data,
    enabled: !!id && enabled,
  })
}

export function useCreateSalaryStructure(id) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post(`/employees/${id}/salary-structures`, body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employee', id, 'salary'] }),
  })
}

export function useDeleteSalaryStructure(id) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (structureId) => api.delete(`/employees/${id}/salary-structures/${structureId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['employee', id, 'salary'] }),
  })
}

// ===========================================================================
// Attendance
// ===========================================================================

export function useAttendanceToday() {
  return useQuery({
    queryKey: ['attendance', 'today'],
    queryFn: async () => (await api.get('/attendance/today')).data, // record or null
  })
}

export function useClockIn() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async () => (await api.post('/attendance/clock-in')).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attendance'] }),
  })
}

export function useClockOut() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async () => (await api.post('/attendance/clock-out')).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attendance'] }),
  })
}

export function useAttendance(params = {}) {
  return useQuery({
    queryKey: ['attendance', 'list', clean(params)],
    queryFn: async () => paged(await api.get('/attendance', { params: clean(params) })),
    placeholderData: keepPreviousData,
  })
}

export function useManualAttendance() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/attendance/manual', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attendance'] }),
  })
}

export function useAttendanceSummary(employeeId, year, month) {
  return useQuery({
    queryKey: ['attendance', 'summary', employeeId, year, month],
    queryFn: async () => (await api.get(`/attendance/summary/${employeeId}/${year}/${month}`)).data,
    enabled: !!employeeId && !!year && !!month,
  })
}

export function useCorrections(status) {
  return useQuery({
    queryKey: ['attendance', 'corrections', status || 'All'],
    queryFn: async () => (await api.get('/attendance/corrections', { params: clean({ status }) })).data,
  })
}

export function useCreateCorrection() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/attendance/corrections', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attendance', 'corrections'] }),
  })
}

export function useReviewCorrection() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, ...body }) => (await api.post(`/attendance/corrections/${id}/review`, body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attendance', 'corrections'] }),
  })
}

// ===========================================================================
// Leave
// ===========================================================================

export function useLeaveTypes() {
  return useQuery({
    queryKey: ['leave', 'types'],
    queryFn: async () => (await api.get('/leave/types')).data,
  })
}

export function useSaveLeaveType() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/leave/types', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['leave'] }),
  })
}

export function useLeaveBalances(employeeId, year) {
  return useQuery({
    queryKey: ['leave', 'balances', employeeId, year],
    queryFn: async () =>
      (await api.get(`/leave/balances/${employeeId}`, { params: clean({ year }) })).data,
    enabled: !!employeeId,
  })
}

export function useAdjustBalance(employeeId) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ leaveTypeId, ...body }) =>
      (await api.post(`/leave/balances/${employeeId}/${leaveTypeId}/adjust`, body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['leave', 'balances'] }),
  })
}

export function useLeaveTransactions(employeeId, { leaveTypeId, year } = {}) {
  return useQuery({
    queryKey: ['leave', 'transactions', employeeId, leaveTypeId, year],
    queryFn: async () =>
      (await api.get(`/leave/transactions/${employeeId}`, { params: clean({ leaveTypeId, year }) })).data,
    enabled: !!employeeId,
  })
}

export function useLeaveRequests(params = {}) {
  return useQuery({
    queryKey: ['leave', 'requests', clean(params)],
    queryFn: async () => paged(await api.get('/leave/requests', { params: clean(params) })),
    placeholderData: keepPreviousData,
  })
}

export function useCreateLeaveRequest() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/leave/requests', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['leave'] }),
  })
}

export function useReviewLeaveRequest() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, action, ...body }) =>
      (await api.post(`/leave/requests/${id}/${action}`, body)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['leave'] })
      queryClient.invalidateQueries({ queryKey: ['org', 'dashboard'] })
    },
  })
}

export function useCancelLeaveRequest() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (id) => (await api.post(`/leave/requests/${id}/cancel`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['leave'] }),
  })
}

export function useLeaveCalendar({ from, to, departmentId }) {
  return useQuery({
    queryKey: ['leave', 'calendar', from, to, departmentId],
    queryFn: async () =>
      (await api.get('/leave/calendar', { params: clean({ from, to, departmentId }) })).data,
    enabled: !!from && !!to,
  })
}

// ===========================================================================
// Payroll
// ===========================================================================

export function useRuleSets() {
  return useQuery({
    queryKey: ['payroll', 'rule-sets'],
    queryFn: async () => (await api.get('/payroll/rule-sets')).data,
  })
}

export function useRuleSet(id) {
  return useQuery({
    queryKey: ['payroll', 'rule-sets', id],
    queryFn: async () => (await api.get(`/payroll/rule-sets/${id}`)).data,
    enabled: !!id,
  })
}

export function usePayrollPeriods() {
  return useQuery({
    queryKey: ['payroll', 'periods'],
    queryFn: async () => (await api.get('/payroll/periods')).data,
  })
}

export function useCreatePayrollPeriod() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/payroll/periods', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payroll', 'periods'] }),
  })
}

export function useDeletePayrollPeriod() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (id) => api.delete(`/payroll/periods/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payroll', 'periods'] }),
  })
}

export function usePayrollRun(id) {
  return useQuery({
    queryKey: ['payroll', 'run', id],
    queryFn: async () => (await api.get(`/payroll/runs/${id}`)).data,
    enabled: !!id,
  })
}

export function usePayrollRunItems(id, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: ['payroll', 'items', id, page, pageSize],
    queryFn: async () =>
      paged(await api.get(`/payroll/runs/${id}/items`, { params: { page, pageSize } })),
    enabled: !!id,
    placeholderData: keepPreviousData,
  })
}

export function useCreatePayrollRun() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/payroll/runs', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payroll'] }),
  })
}

export function usePayrollAction() {
  // action: calculate | approve | finalize | reverse; body for reverse: { note }
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, action, body }) =>
      (await api.post(`/payroll/runs/${id}/${action}`, body || {})).data,
    onSuccess: (_d, vars) => {
      queryClient.invalidateQueries({ queryKey: ['payroll', 'run', vars.id] })
      queryClient.invalidateQueries({ queryKey: ['payroll', 'items', vars.id] })
      queryClient.invalidateQueries({ queryKey: ['payroll', 'periods'] })
    },
  })
}

export function useAddAdjustment(runId) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post(`/payroll/runs/${runId}/adjustments`, body)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payroll', 'run', runId] })
      queryClient.invalidateQueries({ queryKey: ['payroll', 'items', runId] })
    },
  })
}

export function usePublishPayslips(runId) {
  const queryClient = qc()
  return useMutation({
    mutationFn: async () => (await api.post(`/payslips/runs/${runId}/publish`)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payslips'] })
      queryClient.invalidateQueries({ queryKey: ['payroll', 'run', runId] })
    },
  })
}

// ===========================================================================
// Payslips
// ===========================================================================

export function usePayslips(params = {}) {
  return useQuery({
    queryKey: ['payslips', clean(params)],
    queryFn: async () => paged(await api.get('/payslips', { params: clean(params) })),
    placeholderData: keepPreviousData,
  })
}

// ===========================================================================
// Reports
// ===========================================================================

export function useHeadcountReport() {
  return useQuery({
    queryKey: ['reports', 'headcount'],
    queryFn: async () => (await api.get('/reports/headcount')).data,
  })
}

export function useAttendanceTrends(from, to) {
  return useQuery({
    queryKey: ['reports', 'attendance', from, to],
    queryFn: async () =>
      (await api.get('/reports/attendance-trends', { params: clean({ from, to }) })).data,
  })
}

export function useLeaveUtilization(year) {
  return useQuery({
    queryKey: ['reports', 'leave-utilization', year],
    queryFn: async () =>
      (await api.get('/reports/leave-utilization', { params: clean({ year }) })).data,
  })
}

export function usePayrollExpenses() {
  return useQuery({
    queryKey: ['reports', 'payroll-expenses'],
    queryFn: async () => (await api.get('/reports/payroll-expenses')).data,
  })
}

export function useTurnover(year) {
  return useQuery({
    queryKey: ['reports', 'turnover', year],
    queryFn: async () => (await api.get('/reports/turnover', { params: clean({ year }) })).data,
  })
}

// ===========================================================================
// Notifications
// ===========================================================================

export function useNotifications(params = {}) {
  return useQuery({
    queryKey: ['notifications', clean(params)],
    queryFn: async () => paged(await api.get('/notifications', { params: clean(params) })),
    placeholderData: keepPreviousData,
  })
}

export function useUnreadCount(enabled = true) {
  return useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: async () => (await api.get('/notifications/unread-count')).data, // number
    refetchInterval: 30_000,
    enabled,
    retry: 1,
  })
}

export function useMarkNotificationRead() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (id) => api.post(`/notifications/${id}/read`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })
}

export function useMarkAllNotificationsRead() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async () => api.post('/notifications/read-all'),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })
}

// ===========================================================================
// Self-service
// ===========================================================================

export function useProfileChanges(status) {
  return useQuery({
    queryKey: ['self', 'profile-changes', status || 'All'],
    queryFn: async () =>
      (await api.get('/self-service/profile-changes', { params: clean({ status }) })).data,
  })
}

export function useReviewProfileChange() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, ...body }) =>
      (await api.post(`/self-service/profile-changes/${id}/review`, body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['self', 'profile-changes'] }),
  })
}

export function useHrRequests(status) {
  return useQuery({
    queryKey: ['self', 'hr-requests', status || 'All'],
    queryFn: async () =>
      (await api.get('/self-service/hr-requests', { params: clean({ status }) })).data,
  })
}

export function useCreateHrRequest() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/self-service/hr-requests', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['self', 'hr-requests'] }),
  })
}

export function useRespondHrRequest() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, ...body }) =>
      (await api.post(`/self-service/hr-requests/${id}/respond`, body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['self', 'hr-requests'] }),
  })
}

// ===========================================================================
// Platform (super admin)
// ===========================================================================

export function usePlatformStats(enabled = true) {
  return useQuery({
    queryKey: ['platform', 'stats'],
    queryFn: async () => (await api.get('/platform/stats')).data,
    enabled,
  })
}

export function useTenants(params = {}, enabled = true) {
  return useQuery({
    queryKey: ['platform', 'tenants', clean(params)],
    queryFn: async () => paged(await api.get('/platform/tenants', { params: clean(params) })),
    enabled,
    placeholderData: keepPreviousData,
  })
}

export function useTenantAction() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async ({ id, action, body }) => {
      if (action === 'suspend') return api.post(`/platform/tenants/${id}/suspend`)
      if (action === 'activate') return api.post(`/platform/tenants/${id}/activate`)
      return (await api.post(`/platform/tenants/${id}/subscription`, body)).data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['platform'] })
      queryClient.invalidateQueries({ queryKey: ['org'] })
    },
  })
}

export function usePlans(enabled = true) {
  return useQuery({
    queryKey: ['platform', 'plans'],
    queryFn: async () => (await api.get('/platform/plans')).data,
    enabled,
  })
}

export function useSavePlan() {
  const queryClient = qc()
  return useMutation({
    mutationFn: async (body) => (await api.post('/platform/plans', body)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['platform', 'plans'] }),
  })
}

// ===========================================================================
// 2FA
// ===========================================================================

export function useSetup2fa() {
  return useMutation({
    mutationFn: async () => (await api.post('/auth/2fa/setup')).data, // { secret, otpAuthUri }
  })
}

export function useEnable2fa() {
  return useMutation({
    mutationFn: async (code) => api.post('/auth/2fa/enable', { code }),
  })
}

export function useDisable2fa() {
  return useMutation({
    mutationFn: async (password) => api.post('/auth/2fa/disable', { password }),
  })
}
