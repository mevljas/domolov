/**
 * Accept only same-origin, in-app paths as post-login redirect targets.
 * Rejects absolute URLs, protocol-relative "//host" and backslash tricks to avoid open redirects.
 */
export function safeRedirect(value: unknown, fallback = '/'): string {
  const raw = Array.isArray(value) ? value[0] : value
  if (typeof raw !== 'string' || raw === '') return fallback
  if (!raw.startsWith('/') || raw.startsWith('//') || raw.includes('\\')) return fallback
  if ([...raw].some((ch) => ch.charCodeAt(0) < 0x20)) return fallback
  if (raw === '/login' || raw.startsWith('/login?') || raw.startsWith('/login#')) return fallback
  return raw
}

/** Build the /login location that returns to `current` after signing in. */
export function loginLocation(current: string): { path: string; query?: { redirect: string } } {
  const redirect = safeRedirect(current, '')
  return redirect && redirect !== '/' ? { path: '/login', query: { redirect } } : { path: '/login' }
}
