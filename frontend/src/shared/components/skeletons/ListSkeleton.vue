<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Skeleton } from '@/shared/components/ui/skeleton'

withDefaults(defineProps<{ rows?: number }>(), { rows: 5 })

const { t } = useI18n()
</script>

<template>
  <div
    role="status"
    :aria-label="t('common.loading')"
    class="divide-y overflow-hidden rounded-xl border bg-card shadow-xs"
  >
    <div v-for="index in rows" :key="index" class="flex items-center gap-4 p-4" aria-hidden="true">
      <Skeleton class="size-11 shrink-0 rounded-lg" />
      <div class="min-w-0 flex-1 space-y-2">
        <Skeleton class="h-4 w-1/3" />
        <Skeleton class="h-3.5 w-2/3" />
      </div>
      <Skeleton class="hidden h-6 w-20 rounded-full sm:block" />
    </div>
  </div>
</template>
