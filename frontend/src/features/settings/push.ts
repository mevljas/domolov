const PUSH_SUBSCRIPTION_ID_KEY = 'domolov.pushSubscriptionId'

export function getStoredPushSubscriptionId(): string | null {
  try {
    return localStorage.getItem(PUSH_SUBSCRIPTION_ID_KEY)
  } catch {
    return null
  }
}

export function setStoredPushSubscriptionId(id: string | null) {
  try {
    if (id) localStorage.setItem(PUSH_SUBSCRIPTION_ID_KEY, id)
    else localStorage.removeItem(PUSH_SUBSCRIPTION_ID_KEY)
  } catch {
    // private mode / blocked storage
  }
}

/** Convert a URL-safe base64 VAPID key into the ArrayBuffer Web Push expects. */
export function urlBase64ToUint8Array(base64String: string): Uint8Array<ArrayBuffer> {
  const padding = '='.repeat((4 - (base64String.length % 4)) % 4)
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/')
  const raw = atob(base64)
  const buffer = new ArrayBuffer(raw.length)
  const output = new Uint8Array(buffer)
  for (let i = 0; i < raw.length; i++) output[i] = raw.charCodeAt(i)
  return output
}

export { PUSH_SUBSCRIPTION_ID_KEY }
