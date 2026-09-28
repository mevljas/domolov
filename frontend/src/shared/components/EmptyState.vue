<script setup lang="ts">
import type { Component } from 'vue'
import RooflinePattern from './brand/RooflinePattern.vue'

withDefaults(
  defineProps<{
    title: string
    description?: string
    icon?: Component
    headingLevel?: 2 | 3
  }>(),
  { headingLevel: 2 },
)

defineSlots<{ actions?: () => unknown; default?: () => unknown }>()
</script>

<template>
  <section
    class="relative isolate overflow-hidden rounded-2xl border bg-card px-6 pt-14 pb-20 text-center shadow-sm sm:px-10"
  >
    <RooflinePattern
      class="absolute inset-x-0 bottom-0 -z-10 h-24 w-full text-sage opacity-30 dark:opacity-25"
    />
    <div
      v-if="icon"
      class="mx-auto mb-6 grid size-16 place-items-center rounded-2xl bg-sage-soft text-primary shadow-xs ring-8 ring-sage-soft/40"
    >
      <component :is="icon" class="size-7" aria-hidden="true" />
    </div>
    <component :is="`h${headingLevel}`" class="text-2xl font-semibold tracking-tight">
      {{ title }}
    </component>
    <p v-if="description" class="mx-auto mt-3 max-w-md text-pretty text-muted-foreground">
      {{ description }}
    </p>
    <slot />
    <div v-if="$slots.actions" class="mt-7 flex flex-wrap justify-center gap-3">
      <slot name="actions" />
    </div>
  </section>
</template>
