<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { Skeleton } from '@/shared/components/ui/skeleton'

const { t } = useI18n()
</script>

<template>
  <div role="status" :aria-label="t('common.loading')" class="grid gap-8 lg:grid-cols-[1fr_20rem]">
    <div class="space-y-6" aria-hidden="true">
      <Skeleton class="aspect-[16/9] w-full rounded-2xl" />
      <div class="space-y-3">
        <Skeleton class="h-8 w-1/2" />
        <Skeleton class="h-4 w-full" />
        <Skeleton class="h-4 w-11/12" />
        <Skeleton class="h-4 w-3/4" />
      </div>
    </div>
    <div class="space-y-4 rounded-2xl border bg-card p-5 shadow-xs" aria-hidden="true">
      <Skeleton class="h-10 w-2/3" />
      <Skeleton class="h-4 w-1/2" />
      <Skeleton class="h-32 w-full" />
      <Skeleton class="h-10 w-full rounded-lg" />
    </div>
  </div>
</template>
