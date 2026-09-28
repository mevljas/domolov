import { expect, mockApi, presetPreferences, test, type Page } from './fixtures'

async function openThemeMenu(page: Page, isMobile: boolean) {
  if (isMobile) {
    await page.getByTestId('bottom-nav-more').click()
    await page.getByRole('dialog').getByTestId('theme-toggle').click()
  } else {
    await page.getByTestId('sidebar').getByTestId('theme-toggle').click()
  }
}

async function chooseLocale(page: Page, isMobile: boolean, name: string) {
  if (isMobile) {
    await page.getByTestId('bottom-nav-more').click()
    await page.getByRole('dialog').getByTestId('locale-switcher').click()
  } else {
    await page.getByTestId('sidebar').getByTestId('locale-switcher').click()
  }
  await page.getByRole('menuitemradio', { name }).click()
  await expect(page.getByRole('menu')).toBeHidden()
  if (isMobile) {
    // The More sheet is modal; close it so the page content is exposed again.
    await page.keyboard.press('Escape')
    await expect(page.getByTestId('bottom-nav-more')).toHaveAttribute('aria-expanded', 'false')
  }
}

test.beforeEach(async ({ page }) => {
  await mockApi(page, { signedIn: true })
})

test('dark mode toggle applies immediately and persists across reloads', async ({
  page,
  isMobile,
}) => {
  await page.emulateMedia({ colorScheme: 'light' })
  await presetPreferences(page, { locale: 'en' })
  await page.goto('/')
  await expect(page.locator('html')).not.toHaveClass(/dark/)

  await openThemeMenu(page, isMobile)
  await page.getByRole('menuitemradio', { name: 'Dark' }).click()
  await expect(page.locator('html')).toHaveClass(/dark/)
  await expect(page.locator('meta[name="theme-color"]')).toHaveAttribute('content', '#0F1F18')

  await page.reload()
  await expect(page.locator('html')).toHaveClass(/dark/)
  const background = await page.evaluate(() => getComputedStyle(document.body).backgroundColor)
  expect(background).toBe('rgb(15, 31, 24)')
})

test('system theme follows the OS colour scheme', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'dark' })
  await page.goto('/')
  await expect(page.locator('html')).toHaveClass(/dark/)
})

test('switching between Slovenian and English updates the UI and persists', async ({
  page,
  isMobile,
}) => {
  await page.goto('/homes')
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Nepremičnine')
  await expect(page.locator('html')).toHaveAttribute('lang', 'sl')

  await chooseLocale(page, isMobile, 'English')
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Homes')
  await expect(page.locator('html')).toHaveAttribute('lang', 'en')
  await expect(page).toHaveTitle('Homes · Domolov')

  await page.reload()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Homes')

  await chooseLocale(page, isMobile, 'Slovenščina')
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Nepremičnine')
})
