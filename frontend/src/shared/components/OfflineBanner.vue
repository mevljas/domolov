<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { WifiOff } from '@lucide/vue'
import { useOnline } from '@/shared/composables/useOnline'

const { t } = useI18n()
const { isOffline } = useOnline()
</script>

<template>
  <div role="status" aria-live="polite">
    <Transition name="rise">
      <div
        v-if="isOffline"
        class="flex items-center justify-center gap-2 border-b border-warning/30 bg-warning-soft px-4 py-2 text-sm font-medium text-warning-foreground"
      >
        <WifiOff class="size-4 shrink-0" aria-hidden="true" />
        <span>{{ t('offline.banner') }}</span>
      </div>
    </Transition>
  </div>
</template>
