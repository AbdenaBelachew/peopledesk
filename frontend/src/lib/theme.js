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
  // Cross-fade surfaces/colors for one switch, then drop the class so it
  // never interferes with hover/layout transitions.
  const root = document.documentElement
  root.classList.add('theme-anim')
  root.classList.toggle('dark', dark)
  window.setTimeout(() => root.classList.remove('theme-anim'), 350)
  try { localStorage.setItem('pd_theme', dark ? 'dark' : 'light') } catch { /* ignore */ }
}

export function isDark() {
  return document.documentElement.classList.contains('dark')
}
