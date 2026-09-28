/// <reference lib="webworker" />
import { clientsClaim } from 'workbox-core'
import {
  cleanupOutdatedCaches,
  createHandlerBoundToURL,
  precacheAndRoute,
} from 'workbox-precaching'
import { NavigationRoute, registerRoute } from 'workbox-routing'

declare const self: ServiceWorkerGlobalScope & {
  __WB_MANIFEST: Array<{ url: string; revision: string | null } | string>
}

self.skipWaiting()
clientsClaim()

precacheAndRoute(self.__WB_MANIFEST)
cleanupOutdatedCaches()

// SPA shell for client-side routes; API, health and SSE traffic always go to the network.
registerRoute(
  new NavigationRoute(createHandlerBoundToURL('/index.html'), {
    denylist: [/^\/api\//, /^\/health/],
  }),
)

export interface PushPayload {
  title: string
  body?: string
  url?: string
  image?: string
  tag?: string
}

type NotificationOptionsWithImage = NotificationOptions & { image?: string }

function parsePayload(data: PushMessageData | null): PushPayload {
  if (!data) return { title: 'Domolov' }
  try {
    const json = data.json() as Partial<PushPayload>
    return { ...json, title: json.title || 'Domolov' }
  } catch {
    return { title: 'Domolov', body: data.text() }
  }
}

self.addEventListener('push', (event) => {
  const payload = parsePayload(event.data)
  const options: NotificationOptionsWithImage = {
    body: payload.body,
    icon: '/pwa-192x192.png',
    badge: '/pwa-64x64.png',
    image: payload.image,
    tag: payload.tag,
    data: { url: payload.url ?? '/' },
  }
  event.waitUntil(self.registration.showNotification(payload.title, options))
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const rawUrl = (event.notification.data as { url?: string } | null)?.url ?? '/'
  const target = new URL(rawUrl, self.location.origin)

  event.waitUntil(
    (async () => {
      const windows = await self.clients.matchAll({
        type: 'window',
        includeUncontrolled: true,
      })
      const exact = windows.find((client) => client.url === target.href)
      if (exact) {
        await exact.focus()
        return
      }
      const sameOrigin = windows.find(
        (client) => new URL(client.url).origin === self.location.origin,
      )
      if (sameOrigin && target.origin === self.location.origin) {
        const focused = await sameOrigin.focus()
        await focused.navigate(target.href)
        return
      }
      await self.clients.openWindow(target.href)
    })(),
  )
})
