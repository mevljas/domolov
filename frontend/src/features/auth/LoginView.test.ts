import { describe, expect, it } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw'
import { flushPromises, renderWithPlugins } from '@/test/render'
import LoginView from './LoginView.vue'

const session = {
  name: 'admin',
  sessionId: '5b0f6a2e-3b7c-4b8a-9d8e-1f2a3b4c5d6e',
  createdAt: '2026-09-27T10:00:00Z',
  expiresAt: '2026-10-27T10:00:00Z',
}

const problem = (status: number, title: string, headers: Record<string, string> = {}) =>
  HttpResponse.json(
    { title, status },
    { status, headers: { 'content-type': 'application/problem+json', ...headers } },
  )

async function waitFor(assertion: () => void, timeout = 1500) {
  const start = Date.now()
  for (;;) {
    try {
      assertion()
      return
    } catch (error) {
      if (Date.now() - start > timeout) throw error
      await flushPromises()
    }
  }
}

async function signInAs(password: string, route = '/login') {
  const rendered = await renderWithPlugins(LoginView, { route })
  await rendered.wrapper.get('[data-testid="password-input"]').setValue(password)
  await rendered.wrapper.get('form').trigger('submit')
  return rendered
}

describe('LoginView', () => {
  it('renders an accessible sign-in form', async () => {
    const { wrapper } = await renderWithPlugins(LoginView, { route: '/login' })
    const input = wrapper.get('[data-testid="password-input"]')

    expect(wrapper.get('h1').text()).toBe('Welcome back')
    expect(input.attributes('type')).toBe('password')
    expect(input.attributes('autocomplete')).toBe('current-password')
    expect(wrapper.get(`label[for="${input.attributes('id')}"]`).text()).toBe('Password')
    expect(wrapper.find('main#main').exists()).toBe(true)
  })

  it('toggles password visibility', async () => {
    const { wrapper } = await renderWithPlugins(LoginView, { route: '/login' })
    const toggle = wrapper.get('[data-testid="toggle-password"]')

    expect(toggle.attributes('aria-pressed')).toBe('false')
    await toggle.trigger('click')
    expect(wrapper.get('[data-testid="password-input"]').attributes('type')).toBe('text')
    expect(toggle.attributes('aria-pressed')).toBe('true')
    expect(toggle.attributes('aria-label')).toBe('Hide password')
  })

  it('asks for a password before calling the API', async () => {
    const { wrapper } = await signInAs('')
    expect(wrapper.get('[data-testid="login-error"]').text()).toBe('Enter your password.')
  })

  it('signs in and redirects to the requested page', async () => {
    let posted: unknown
    let signedIn = false
    server.use(
      http.post('*/api/session', async ({ request }) => {
        posted = await request.json()
        signedIn = true
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/session', () =>
        signedIn ? HttpResponse.json(session) : problem(401, 'Unauthorized'),
      ),
    )

    const { router, queryClient } = await signInAs('hunter2', '/login?redirect=/homes')

    await waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/homes'))
    expect(posted).toEqual({ password: 'hunter2' })
    expect(queryClient.getQueryData(['session'])).toEqual(session)
  })

  it('redirects to the dashboard when the redirect target is unsafe', async () => {
    server.use(
      http.post('*/api/session', () => new HttpResponse(null, { status: 204 })),
      http.get('*/api/session', () => HttpResponse.json(session)),
    )

    const { router } = await signInAs('hunter2', '/login?redirect=//evil.example')

    await waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/'))
  })

  it('shows an error for a wrong password and stays on the page', async () => {
    server.use(http.post('*/api/session', () => problem(401, 'Unauthorized')))

    const { wrapper, router } = await signInAs('wrong')

    await waitFor(() => expect(wrapper.find('[data-testid="login-error"]').exists()).toBe(true))
    expect(wrapper.get('[data-testid="login-error"]').text()).toBe('Invalid password.')
    expect(wrapper.get('[data-testid="login-error"]').attributes('role')).toBe('alert')
    expect(wrapper.get('[data-testid="password-input"]').attributes('aria-invalid')).toBe('true')
    expect(router.currentRoute.value.path).toBe('/login')
  })

  it('shows a rate-limit message with the retry hint and locks the button', async () => {
    server.use(
      http.post('*/api/session', () => problem(429, 'Too Many Requests', { 'retry-after': '30' })),
    )

    const { wrapper } = await signInAs('again')

    await waitFor(() =>
      expect(wrapper.get('[data-testid="login-error"]').text()).toBe(
        'Too many sign-in attempts. Try again in 30 seconds.',
      ),
    )
    expect(wrapper.get('[data-testid="login-submit"]').attributes('disabled')).toBeDefined()
  })

  it('shows a generic rate-limit message without Retry-After', async () => {
    server.use(http.post('*/api/session', () => problem(429, 'Too Many Requests')))

    const { wrapper } = await signInAs('again')

    await waitFor(() =>
      expect(wrapper.get('[data-testid="login-error"]').text()).toBe(
        'Too many sign-in attempts. Please wait a moment and try again.',
      ),
    )
  })

  it('explains server failures', async () => {
    server.use(http.post('*/api/session', () => problem(503, 'Service Unavailable')))

    const { wrapper } = await signInAs('pw')

    await waitFor(() =>
      expect(wrapper.get('[data-testid="login-error"]').text()).toBe(
        'Something went wrong on the server. Try again shortly.',
      ),
    )
  })

  it('renders in Slovenian by default locale', async () => {
    const { wrapper } = await renderWithPlugins(LoginView, { route: '/login', locale: 'sl' })
    expect(wrapper.get('h1').text()).toBe('Lepo, da si spet tu')
    expect(wrapper.get('[data-testid="login-submit"]').text()).toBe('Prijava')
  })
})
