import { expect, mockApi, presetPreferences, test } from './fixtures'

const DESKTOP_DESTINATIONS = [
  { key: 'homes', path: '/homes', heading: 'Homes' },
  { key: 'bookmarks', path: '/bookmarks', heading: 'Bookmarks' },
  { key: 'matches', path: '/matches', heading: 'Matches' },
  { key: 'watches', path: '/watches', heading: 'Watches' },
  { key: 'scans', path: '/scans', heading: 'Scans' },
  { key: 'settings', path: '/settings', heading: 'Settings' },
  { key: 'dashboard', path: '/', heading: 'Dashboard' },
]

test.beforeEach(async ({ page }) => {
  await mockApi(page, { signedIn: true })
  await presetPreferences(page, { locale: 'en' })
})

test('desktop sidebar navigates between the main sections', async ({ page, isMobile }) => {
  test.skip(isMobile, 'desktop layout only')
  await page.goto('/')
  const nav = page.getByRole('navigation', { name: 'Main' })

  for (const destination of DESKTOP_DESTINATIONS) {
    await nav.getByTestId(`nav-${destination.key}`).click()
    await expect(page).toHaveURL(new RegExp(`${destination.path}$`))
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(destination.heading)
    await expect(nav.getByTestId(`nav-${destination.key}`)).toHaveAttribute('aria-current', 'page')
  }
  await expect(page).toHaveTitle('Dashboard · Domolov')
})

test('detail routes highlight their parent section and link back', async ({ page, isMobile }) => {
  test.skip(isMobile, 'desktop layout only')
  await page.goto('/watches/42')

  await expect(page.getByRole('heading', { level: 2 })).toHaveText('Watch not found.')
  await expect(page.getByTestId('nav-watches')).toHaveAttribute('aria-current', 'true')
  await page.getByTestId('nav-watches').click()
  await expect(page).toHaveURL(/\/watches$/)
})

test('mobile bottom nav and More sheet navigate', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'mobile layout only')
  await page.goto('/')
  const bottom = page.getByRole('navigation', { name: 'Primary' })
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeHidden()

  await bottom.getByTestId('bottom-nav-homes').click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Homes')
  await bottom.getByTestId('bottom-nav-matches').click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Matches')

  const box = await bottom.getByTestId('bottom-nav-watches').boundingBox()
  expect(box?.height ?? 0).toBeGreaterThanOrEqual(44)

  await bottom.getByTestId('bottom-nav-more').click()
  const sheet = page.getByRole('dialog', { name: 'More' })
  await sheet.getByTestId('more-nav-settings').click()
  await expect(page).toHaveURL(/\/settings$/)
  await expect(sheet).toBeHidden()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Settings')
})

test('command palette opens with the keyboard and navigates', async ({ page, isMobile }) => {
  test.skip(isMobile, 'keyboard shortcut is a desktop affordance')
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Dashboard')

  await page.keyboard.press('ControlOrMeta+k')
  const palette = page.getByRole('dialog', { name: 'Command palette' })
  await expect(palette).toBeVisible()
  await page.keyboard.type('Scans')
  await page.keyboard.press('Enter')

  await expect(page).toHaveURL(/\/scans$/)
  await expect(palette).toBeHidden()
})

test('unknown routes show the not-found page inside the shell', async ({ page }) => {
  await page.goto('/nowhere')
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Page not found')
  await page.getByRole('link', { name: 'Back to dashboard' }).click()
  await expect(page).toHaveURL(/\/$/)
})
