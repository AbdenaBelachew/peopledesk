// Dark-mode helpers: persisted in localStorage, respects prefers-color-scheme initially.

export function initTheme() {
  let stored = null
  try { stored = localStorage.getItem('pd_theme') } catch { /* ignore */ }
  const dark = stored
    ? stored === 'dark'
    : window.matchMedia('(prefers-color-scheme: dark)').matches
  document.documentElement.classList.toggle('dark', dark)
  return dark
}

export function setTheme(dark) {
  document.documentElement.classList.toggle('dark', dark)
  try { localStorage.setItem('pd_theme', dark ? 'dark' : 'light') } catch { /* ignore */ }
}

export function isDark() {
  return document.documentElement.classList.contains('dark')
}
