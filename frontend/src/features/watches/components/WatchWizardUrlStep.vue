<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { watchDebounced } from '@vueuse/core'
import { LoaderCircle } from '@lucide/vue'
import type { SearchUrlCheck } from '@/api/types'
import { checkSearchUrl } from '@/features/watches/api'
import { Label } from '@/shared/components/ui/label'
import { Input } from '@/shared/components/ui/input'
import FieldError from '@/shared/components/FieldError.vue'

const emit = defineEmits<{
  validity: [ok: boolean]
}>()

const url = defineModel<string>('url', { required: true })
const suggestedName = defineModel<string>('suggestedName', { default: '' })

const { t } = useI18n()
const inputId = useId()
const checking = ref(false)
const result = ref<SearchUrlCheck | null>(null)
const checkError = ref<string | null>(null)
let abort: AbortController | null = null

const supported = computed(() => result.value?.supported === true)
const problem = computed(() => result.value?.problem ?? checkError.value)

watchDebounced(
  url,
  async (value) => {
    abort?.abort()
    result.value = null
    checkError.value = null
    const trimmed = value.trim()
    if (!trimmed) {
      emit('validity', false)
      return
    }
    checking.value = true
    abort = new AbortController()
    try {
      const check = await checkSearchUrl(trimmed, abort.signal)
      result.value = check
      if (check.supported && check.suggestedName) {
        suggestedName.value = check.suggestedName
      }
      emit('validity', check.supported)
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      checkError.value = error instanceof Error ? error.message : t('errors.unknown')
      emit('validity', false)
    } finally {
      checking.value = false
    }
  },
  { debounce: 400, immediate: true },
)
</script>

<template>
  <div class="grid gap-4" data-testid="wizard-url-step">
    <div class="grid gap-2">
      <Label :for="inputId">{{ t('watches.searchUrl') }}</Label>
      <Input
        :id="inputId"
        v-model="url"
        type="url"
        inputmode="url"
        autocomplete="off"
        spellcheck="false"
        :placeholder="t('watches.wizard.urlPlaceholder')"
        data-testid="wizard-url-input"
        :aria-invalid="problem && !supported ? true : undefined"
        :aria-describedby="problem ? `${inputId}-error` : undefined"
      />
      <p class="text-sm text-muted-foreground">{{ t('watches.wizard.urlHint') }}</p>
    </div>

    <div
      v-if="checking"
      class="inline-flex items-center gap-2 text-sm text-muted-foreground"
      role="status"
    >
      <LoaderCircle class="size-4 animate-spin" aria-hidden="true" />
      {{ t('watches.wizard.checking') }}
    </div>

    <div
      v-else-if="supported"
      class="rounded-xl border border-success/25 bg-success-soft px-4 py-3 text-sm text-success"
      data-testid="wizard-url-supported"
    >
      <p class="font-semibold">{{ t('watches.wizard.supported') }}</p>
      <p v-if="result?.suggestedName" class="mt-1" data-testid="wizard-suggested-name">
        {{ t('watches.wizard.suggestedName') }}:
        <span class="font-display text-foreground">{{ result.suggestedName }}</span>
      </p>
    </div>

    <FieldError v-else :id="`${inputId}-error`" :message="problem" />
  </div>
</template>
