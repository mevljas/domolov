import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useToast } from '@/shared/composables/useToast'
import { useSignOut } from './useSession'

/** Sign out, return to /login and confirm with a toast — even if the DELETE fails. */
export function useSignOutAction() {
  const signOut = useSignOut()
  const router = useRouter()
  const toast = useToast()
  const { t } = useI18n()

  return async function signOutAndLeave() {
    try {
      await signOut.mutateAsync()
    } catch {
      // The local session cache is cleared regardless; the cookie expires server-side.
    }
    await router.replace({ name: 'login' })
    toast.success(t('auth.signedOut'))
  }
}
