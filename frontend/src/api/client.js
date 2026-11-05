import axios from 'axios'

// ---------------------------------------------------------------------------
// Access token lives in memory only (never localStorage). The refresh token is
// an httpOnly cookie scoped to /api/v1/auth, sent automatically by the browser.
// ---------------------------------------------------------------------------

let accessToken = null

export function setAccessToken(token) {
  accessToken = token
}

export function getAccessToken() {
  return accessToken
}

export function clearAccessToken() {
  accessToken = null
}

// Platform admins target a tenant with the X-Tenant-Id header (selection kept in localStorage).
const TENANT_KEY = 'pd_tenant_id'
export function getSelectedTenantId() {
  try { return localStorage.getItem(TENANT_KEY) || '' } catch { return '' }
}
export function setSelectedTenantId(id) {
  try {
    if (id) localStorage.setItem(TENANT_KEY, id)
    else localStorage.removeItem(TENANT_KEY)
  } catch { /* ignore */ }
}

// Public (pre-auth) axios instance: no Authorization header, no refresh loop on 401.
export const publicApi = axios.create({
  baseURL: '/api/v1',
  withCredentials: true,
})

// Authenticated API instance.
export const api = axios.create({
  baseURL: '/api/v1',
  withCredentials: true,
})

api.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers = config.headers || {}
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  const tenantId = getSelectedTenantId()
  if (tenantId && String(config.url || '').includes('/platform')) {
    config.headers = config.headers || {}
    config.headers['X-Tenant-Id'] = tenantId
  }
  return config
})

// Single in-flight refresh so parallel 401s share one refresh call.
let refreshPromise = null

function doRefresh() {
  if (!refreshPromise) {
    refreshPromise = publicApi
      .post('/auth/refresh')
      .then((res) => {
        setAccessToken(res.data.accessToken)
        return res.data.accessToken
      })
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

const PUBLIC_PATHS = ['/auth/login', '/auth/register', '/auth/verify-2fa', '/auth/refresh', '/auth/forgot-password', '/auth/reset-password', '/auth/verify-email', '/auth/resend-verification']

function isPublicUrl(url = '') {
  return PUBLIC_PATHS.some((p) => url.includes(p))
}

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const { response, config } = error
    // On 401 (expired token): refresh once, then retry the original request.
    if (
      response?.status === 401 &&
      config &&
      !config._retried &&
      !isPublicUrl(config.url)
    ) {
      config._retried = true
      try {
        await doRefresh()
        return api(config)
      } catch {
        clearAccessToken()
        const path = window.location.pathname
        const isPublicPage = /^\/(login|register|forgot-password|reset-password|verify-email)/.test(path)
        if (!isPublicPage) {
          window.location.assign('/login')
        }
      }
    }
    return Promise.reject(error)
  },
)

// ---------------------------------------------------------------------------
// Error extraction: contract error envelope is { error: { code, message, errors? } }
// ---------------------------------------------------------------------------

export async function apiErrorMessage(error) {
  const data = error?.response?.data
  let payload = data
  if (data instanceof Blob) {
    try {
      payload = JSON.parse(await data.text())
    } catch {
      return error?.message || 'Request failed'
    }
  }
  const e = payload?.error
  if (e) {
    if (e.errors && typeof e.errors === 'object' && Object.keys(e.errors).length) {
      const first = Object.values(e.errors)[0]
      if (Array.isArray(first) && first.length) return String(first[0])
      if (typeof first === 'string') return first
    }
    return e.message || 'Request failed'
  }
  return error?.message || 'Request failed'
}

// ---------------------------------------------------------------------------
// Blob helpers: file exports, payslip/document downloads, PDF preview URLs.
// ---------------------------------------------------------------------------

function filenameFromDisposition(header, fallback) {
  if (!header) return fallback
  const m = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header)
  if (m) {
    try { return decodeURIComponent(m[1]) } catch { return m[1] }
  }
  return fallback
}

/** Fetch a binary endpoint with auth; returns the axios response (res.data is a Blob). */
export async function fetchBlob(path, params) {
  return api.get(path, { params, responseType: 'blob' })
}

/** Fetch + trigger a file download named from Content-Disposition. */
export async function downloadFile(path, params, fallbackName = 'download') {
  const res = await fetchBlob(path, params)
  const name = filenameFromDisposition(res.headers?.['content-disposition'], fallbackName)
  const url = URL.createObjectURL(res.data)
  const a = document.createElement('a')
  a.href = url
  a.download = name
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
  return name
}

/** Fetch a PDF/image as an object URL for inline preview (iframe/img). */
export async function openBlobUrl(path) {
  const res = await fetchBlob(path)
  const type = res.headers?.['content-type'] || 'application/octet-stream'
  return URL.createObjectURL(new Blob([res.data], { type }))
}
