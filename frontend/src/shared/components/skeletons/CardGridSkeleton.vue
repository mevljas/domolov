<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Skeleton } from '@/shared/components/ui/skeleton'

withDefaults(defineProps<{ count?: number }>(), { count: 6 })

const { t } = useI18n()
</script>

<template>
  <div
    role="status"
    :aria-label="t('common.loading')"
    class="grid gap-5 sm:grid-cols-2 xl:grid-cols-3"
  >
    <div
      v-for="index in count"
      :key="index"
      class="overflow-hidden rounded-xl border bg-card shadow-xs"
      aria-hidden="true"
    >
      <Skeleton class="aspect-[4/3] w-full rounded-none" />
      <div class="space-y-3 p-4">
        <Skeleton class="h-6 w-2/5" />
        <Skeleton class="h-4 w-4/5" />
        <div class="flex gap-2 pt-1">
          <Skeleton class="h-5 w-16 rounded-full" />
          <Skeleton class="h-5 w-20 rounded-full" />
        </div>
      </div>
    </div>
  </div>
</template>
