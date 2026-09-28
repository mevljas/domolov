import { expect, mockApi, PASSWORD, presetPreferences, test } from './fixtures'

test.describe('sign in', () => {
  test('login page renders in Slovenian by default', async ({ page }) => {
    await mockApi(page)
    await page.goto('/login')

    await expect(page).toHaveTitle('Prijava · Domolov')
    await expect(page.locator('html')).toHaveAttribute('lang', 'sl')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Lepo, da si spet tu')
    const password = page.getByLabel('Geslo', { exact: true })
    await expect(password).toHaveAttribute('autocomplete', 'current-password')
    await expect(page.getByRole('button', { name: 'Prijava' })).toBeEnabled()
  })

  test('protected routes redirect to login and back after signing in', async ({ page }) => {
    const api = await mockApi(page)
    await presetPreferences(page, { locale: 'en' })
    await page.goto('/homes')

    await expect(page).toHaveURL(/\/login\?redirect=(%2F|\/)homes$/)
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD)
    await page.getByRole('button', { name: 'Sign in' }).click()

    await expect(page).toHaveURL(/\/homes$/)
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Homes')
    expect(api.requests.every((r) => r.requestedWith === 'domolov')).toBe(true)
  })

  test('shows errors for a wrong password and rate limiting', async ({ page }) => {
    await mockApi(page)
    await presetPreferences(page, { locale: 'en' })
    await page.goto('/login')
    const password = page.getByLabel('Password', { exact: true })

    await password.fill('nope')
    await password.press('Enter')
    await expect(page.getByRole('alert')).toHaveText('Invalid password.')

    await password.fill('too-many')
    await password.press('Enter')
    await expect(page.getByRole('alert')).toContainText('Try again in 4')
    await expect(page.getByRole('button', { name: 'Sign in' })).toBeDisabled()
  })

  test('password visibility can be toggled', async ({ page }) => {
    await mockApi(page)
    await presetPreferences(page, { locale: 'en' })
    await page.goto('/login')
    const password = page.getByLabel('Password', { exact: true })
    await password.fill('secret')

    await page.getByRole('button', { name: 'Show password' }).click()
    await expect(password).toHaveAttribute('type', 'text')
    await page.getByRole('button', { name: 'Hide password' }).click()
    await expect(password).toHaveAttribute('type', 'password')
  })

  test('signing out returns to the login page', async ({ page, isMobile }) => {
    await mockApi(page, { signedIn: true })
    await presetPreferences(page, { locale: 'en' })
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dashboard')

    if (isMobile) {
      await page.getByTestId('bottom-nav-more').click()
      await page.getByRole('dialog').getByRole('button', { name: 'Sign out' }).click()
    } else {
      await page.getByTestId('sign-out').click()
    }

    await expect(page).toHaveURL(/\/login$/)
    await expect(page.getByText('You have been signed out.')).toBeVisible()
  })
})
