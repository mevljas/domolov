<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    title?: string
    decorative?: boolean
    /** `auto` follows the theme; `onDark` is the light tile for forest/dark surfaces. */
    variant?: 'auto' | 'onLight' | 'onDark'
  }>(),
  { title: 'Domolov', decorative: false, variant: 'auto' },
)

const palette = computed(() => {
  switch (props.variant) {
    case 'onLight':
      return {
        tile: 'fill-[#1F4D3A]',
        roof: 'stroke-[#F7F5F0]',
        accent: 'stroke-[#E08A66]',
        dot: 'fill-[#E08A66]',
      }
    case 'onDark':
      return {
        tile: 'fill-[#7FB89A]',
        roof: 'stroke-[#0F1F18]',
        accent: 'stroke-[#8A3E22]',
        dot: 'fill-[#8A3E22]',
      }
    default:
      return {
        tile: 'fill-[#1F4D3A] dark:fill-[#7FB89A]',
        roof: 'stroke-[#F7F5F0] dark:stroke-[#0F1F18]',
        accent: 'stroke-[#E08A66] dark:stroke-[#8A3E22]',
        dot: 'fill-[#E08A66] dark:fill-[#8A3E22]',
      }
  }
})
</script>

<template>
  <svg
    viewBox="0 0 64 64"
    xmlns="http://www.w3.org/2000/svg"
    :role="decorative ? undefined : 'img'"
    :aria-hidden="decorative ? 'true' : undefined"
    :aria-label="decorative ? undefined : title"
  >
    <rect width="64" height="64" rx="15" :class="palette.tile" />
    <g fill="none" stroke-linecap="round" stroke-linejoin="round">
      <path
        d="M18.5 29.5V47.5a4 4 0 0 0 4 4H41.5a4 4 0 0 0 4-4V29.5"
        :class="palette.roof"
        stroke-width="4.5"
      />
      <path d="M12 31.5 32 12.5l20 19" :class="palette.roof" stroke-width="5.5" />
      <circle cx="32" cy="37.5" r="6.75" :class="palette.accent" stroke-width="3.5" />
      <path
        d="M32 27.25v2M32 45.75v2M22.75 37.5h1.75M39.5 37.5h1.75"
        :class="palette.accent"
        stroke-width="3"
      />
    </g>
    <circle cx="32" cy="37.5" r="2.25" :class="palette.dot" />
  </svg>
</template>
