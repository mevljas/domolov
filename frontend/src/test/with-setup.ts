import { createApp, defineComponent, h } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { createAppI18n } from '@/app/i18n'
import type { AppLocale } from '@/shared/lib/locale'

/** Run a composable inside a real component setup with Pinia and i18n installed. */
export function withSetup<T>(composable: () => T, { locale = 'en' as AppLocale } = {}) {
  let result!: T
  const pinia = createPinia()
  setActivePinia(pinia)
  const i18n = createAppI18n(locale)
  const app = createApp(
    defineComponent({
      setup() {
        result = composable()
        return () => h('div')
      },
    }),
  )
  app.use(pinia).use(i18n)
  const root = document.createElement('div')
  app.mount(root)
  return { result, app, i18n, pinia, unmount: () => app.unmount() }
}
