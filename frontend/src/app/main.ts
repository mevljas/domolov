import '@fontsource-variable/fraunces/opsz.css'
import '@fontsource/source-sans-3/400.css'
import '@fontsource/source-sans-3/600.css'
import '@fontsource/source-sans-3/700.css'
import 'vue-sonner/style.css'
import '@/styles/main.css'

import { createApp, watch } from 'vue'
import { createPinia } from 'pinia'
import { VueQueryPlugin } from '@tanstack/vue-query'
import { setUnauthorizedHandler } from '@/api/client'
import { sessionQueryKey } from '@/features/auth/useSession'
import { installRouteViewTransitions } from '@/shared/composables/useViewTransition'
import { loginLocation } from '@/shared/lib/redirect'
import App from './App.vue'
import { i18n } from './i18n'
import { queryClient } from './query-client'
import { installRouteEffects } from './route-effects'
import { createAppRouter } from './router'
import { registerServiceWorker } from './pwa'

const app = createApp(App)
const router = createAppRouter(queryClient)

app.use(createPinia())
app.use(i18n)
app.use(VueQueryPlugin, { queryClient })
app.use(router)

const translate = (key: string, params?: Record<string, unknown>) =>
  i18n.global.t(key, params ?? {})

setUnauthorizedHandler((path) => {
  queryClient.setQueryData(sessionQueryKey, null)
  void router.replace(loginLocation(path))
})

installRouteViewTransitions(router)
const { refreshTitle } = installRouteEffects(router, translate)
watch(i18n.global.locale, refreshTitle)

void router.isReady().then(() => {
  app.mount('#app')
  registerServiceWorker()
})
