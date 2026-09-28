<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{ rows?: number; strokeWidth?: number }>(), {
  rows: 1,
  strokeWidth: 1.5,
})

const WIDTHS = [96, 72, 120, 84, 104, 68, 112, 90]
const VIEW_WIDTH = 1200
const ROW_HEIGHT = 150

/** Deterministic skyline of gabled roofs, drawn as one stroked path per row. */
const rowPaths = computed(() =>
  Array.from({ length: props.rows }, (_, row) => {
    const base = ROW_HEIGHT * (row + 1)
    const segments: string[] = []
    let x = -20 - row * 37
    let i = row * 3
    while (x < VIEW_WIDTH + 20) {
      const w = WIDTHS[i % WIDTHS.length]!
      const h = 34 + ((i * 37) % 46)
      const roof = w * 0.42
      segments.push(
        `M${x} ${base}V${base - h}L${x + w / 2} ${base - h - roof}L${x + w} ${base - h}V${base}`,
      )
      x += w + 10
      i++
    }
    return segments.join('')
  }),
)
</script>

<template>
  <svg
    :viewBox="`0 0 ${VIEW_WIDTH} ${ROW_HEIGHT * rows}`"
    preserveAspectRatio="xMidYMax slice"
    aria-hidden="true"
    focusable="false"
    class="pointer-events-none"
  >
    <path
      v-for="(d, index) in rowPaths"
      :key="index"
      :d="d"
      fill="none"
      stroke="currentColor"
      :stroke-width="strokeWidth"
      stroke-linejoin="round"
      vector-effect="non-scaling-stroke"
    />
  </svg>
</template>
