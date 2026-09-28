<script setup lang="ts">
import { ArrowLeft } from '@lucide/vue'
import type { RouteLocationRaw } from 'vue-router'

defineProps<{
  title: string
  description?: string
  eyebrow?: string
  backTo?: RouteLocationRaw
  backLabel?: string
}>()

defineSlots<{
  actions?: () => unknown
  default?: () => unknown
}>()
</script>

<template>
  <header class="mb-8 md:mb-10">
    <RouterLink
      v-if="backTo"
      :to="backTo"
      class="group mb-4 inline-flex items-center gap-1.5 rounded-md text-sm font-medium text-muted-foreground transition-colors hover:text-foreground"
    >
      <ArrowLeft
        class="size-4 transition-transform group-hover:-translate-x-0.5"
        aria-hidden="true"
      />
      {{ backLabel }}
    </RouterLink>
    <div class="flex flex-col gap-5 md:flex-row md:items-end md:justify-between">
      <div class="min-w-0">
        <p
          v-if="eyebrow"
          class="mb-2 text-xs font-semibold tracking-[0.16em] text-primary uppercase"
        >
          {{ eyebrow }}
        </p>
        <h1 class="text-3xl leading-tight font-semibold tracking-tight md:text-[2.5rem]">
          {{ title }}
        </h1>
        <p v-if="description" class="mt-2 max-w-2xl text-base text-muted-foreground md:text-lg">
          {{ description }}
        </p>
      </div>
      <div v-if="$slots.actions" class="flex shrink-0 flex-wrap items-center gap-2">
        <slot name="actions" />
      </div>
    </div>
    <slot />
  </header>
</template>
