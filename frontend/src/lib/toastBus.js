// Tiny bridge so non-React modules (react-query caches, api client) can push toasts.
let handler = null

export const toastBus = {
  set(fn) { handler = fn },
  emit(message, type = 'error') {
    if (handler) handler(message, type)
  },
}
