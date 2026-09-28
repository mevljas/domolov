import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useIntervalFn } from '@vueuse/core'
import { ApiError, problemMessage } from '@/api/problem'
import { safeRedirect } from '@/shared/lib/redirect'
import { useSignIn } from './useSession'

/** Sign-in form state: submit, error mapping (401 / 429 with countdown) and post-login redirect. */
export function useLoginForm() {
  const { t } = useI18n()
  const route = useRoute()
  const router = useRouter()
  const signIn = useSignIn()

  const password = ref('')
  const showPassword = ref(false)
  const error = ref<'invalid' | 'required' | 'rateLimited' | 'other' | null>(null)
  const otherMessage = ref('')
  const retryIn = ref<number | null>(null)

  const countdown = useIntervalFn(
    () => {
      if (retryIn.value === null) return
      retryIn.value = Math.max(0, retryIn.value - 1)
      if (retryIn.value === 0) {
        retryIn.value = null
        countdown.pause()
        if (error.value === 'rateLimited') error.value = null
      }
    },
    1000,
    { immediate: false },
  )

  const isSubmitting = computed(() => signIn.isPending.value)
  const isLocked = computed(() => retryIn.value !== null && retryIn.value > 0)

  const errorMessage = computed(() => {
    switch (error.value) {
      case 'invalid':
        return t('auth.invalidPassword')
      case 'required':
        return t('auth.passwordRequired')
      case 'rateLimited':
        return retryIn.value
          ? t('auth.rateLimited', { seconds: retryIn.value })
          : t('auth.rateLimitedGeneric')
      case 'other':
        return otherMessage.value
      default:
        return ''
    }
  })

  async function submit() {
    if (isSubmitting.value || isLocked.value) return
    if (!password.value) {
      error.value = 'required'
      return
    }
    error.value = null
    try {
      await signIn.mutateAsync(password.value)
      await router.replace(safeRedirect(route.query.redirect))
    } catch (cause) {
      password.value = ''
      if (cause instanceof ApiError && cause.status === 401) {
        error.value = 'invalid'
      } else if (cause instanceof ApiError && cause.status === 429) {
        error.value = 'rateLimited'
        if (cause.retryAfterSeconds) {
          retryIn.value = cause.retryAfterSeconds
          countdown.resume()
        }
      } else {
        error.value = 'other'
        otherMessage.value = problemMessage(cause, t)
      }
    }
  }

  return {
    password,
    showPassword,
    error,
    errorMessage,
    isSubmitting,
    isLocked,
    retryIn,
    submit,
  }
}
