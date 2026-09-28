// Applies the saved theme and language before first paint to avoid a light/dark flash.
// Kept as an external file (not inline) so a strict CSP without 'unsafe-inline' works.
;(function () {
  try {
    var stored = JSON.parse(localStorage.getItem('domolov:preferences') || '{}')
    var theme = stored.theme || 'system'
    var dark =
      theme === 'dark' ||
      (theme === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches)
    var root = document.documentElement
    if (dark) root.classList.add('dark')
    root.style.colorScheme = dark ? 'dark' : 'light'
    if (stored.locale === 'en' || stored.locale === 'sl') root.lang = stored.locale
    var meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', dark ? '#0F1F18' : '#F7F5F0')
  } catch {
    /* storage unavailable: fall back to defaults */
  }
})()
