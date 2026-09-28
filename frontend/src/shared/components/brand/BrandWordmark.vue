<script setup lang="ts">
import { WORDMARK_DOMO, WORDMARK_LOV, WORDMARK_VIEWBOX } from './wordmark-paths'

withDefaults(
  defineProps<{
    title?: string
    decorative?: boolean
    /** `inverse` is for placement on the forest-green brand surface. */
    tone?: 'default' | 'inverse'
  }>(),
  { title: 'Domolov', decorative: false, tone: 'default' },
)
</script>

<template>
  <svg
    :viewBox="WORDMARK_VIEWBOX"
    xmlns="http://www.w3.org/2000/svg"
    :role="decorative ? undefined : 'img'"
    :aria-hidden="decorative ? 'true' : undefined"
    :aria-label="decorative ? undefined : title"
    class="h-[1em] w-auto"
  >
    <path :d="WORDMARK_DOMO" :class="tone === 'inverse' ? 'fill-paper' : 'fill-foreground'" />
    <path :d="WORDMARK_LOV" :class="tone === 'inverse' ? 'fill-[#A9CDB7]' : 'fill-primary'" />
  </svg>
</template>
