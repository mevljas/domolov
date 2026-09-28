<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PricePoint } from '@/api/types'
import { useFormat } from '@/shared/composables/useFormat'
import { useReducedMotion } from '@/shared/composables/useReducedMotion'

const props = defineProps<{
  points: PricePoint[]
  currency?: string
}>()

const { t } = useI18n()
const format = useFormat()
const reducedMotion = useReducedMotion()

const WIDTH = 640
const HEIGHT = 200
const PAD = { top: 16, right: 16, bottom: 28, left: 56 }

const hoverIndex = ref<number | null>(null)

const sorted = computed(() =>
  [...props.points].sort(
    (a, b) => new Date(a.observedAt).getTime() - new Date(b.observedAt).getTime(),
  ),
)

const geometry = computed(() => {
  const pts = sorted.value
  if (pts.length === 0) return null

  const amounts = pts.map((p) => p.amount)
  const minY = Math.min(...amounts)
  const maxY = Math.max(...amounts)
  const spanY = maxY - minY || 1
  const times = pts.map((p) => new Date(p.observedAt).getTime())
  const minX = Math.min(...times)
  const maxX = Math.max(...times)
  const spanX = maxX - minX || 1

  const innerW = WIDTH - PAD.left - PAD.right
  const innerH = HEIGHT - PAD.top - PAD.bottom

  const coords = pts.map((p, i) => {
    const x = PAD.left + ((times[i]! - minX) / spanX) * innerW
    const y = PAD.top + (1 - (p.amount - minY) / spanY) * innerH
    return { x, y, point: p }
  })

  const polyline = coords.map((c) => `${c.x.toFixed(1)},${c.y.toFixed(1)}`).join(' ')
  return { coords, polyline, minY, maxY }
})

const hover = computed(() => {
  if (hoverIndex.value == null || !geometry.value) return null
  return geometry.value.coords[hoverIndex.value] ?? null
})

function onMove(event: MouseEvent) {
  if (!geometry.value || geometry.value.coords.length === 0) return
  const svg = event.currentTarget as SVGSVGElement
  const rect = svg.getBoundingClientRect()
  const x = ((event.clientX - rect.left) / rect.width) * WIDTH
  let best = 0
  let bestDist = Infinity
  geometry.value.coords.forEach((c, i) => {
    const d = Math.abs(c.x - x)
    if (d < bestDist) {
      bestDist = d
      best = i
    }
  })
  hoverIndex.value = best
}

function onLeave() {
  hoverIndex.value = null
}
</script>

<template>
  <figure class="space-y-3">
    <figcaption class="font-display text-xl font-semibold">
      {{ t('homes.chart.title') }}
    </figcaption>

    <p v-if="!geometry" class="text-sm text-muted-foreground">{{ t('homes.chart.empty') }}</p>

    <div v-else class="rounded-2xl border bg-card p-3 shadow-sm">
      <svg
        :viewBox="`0 0 ${WIDTH} ${HEIGHT}`"
        class="h-auto w-full"
        role="img"
        :aria-label="t('homes.chart.title')"
        @mousemove="onMove"
        @mouseleave="onLeave"
      >
        <line
          :x1="PAD.left"
          :x2="WIDTH - PAD.right"
          :y1="HEIGHT - PAD.bottom"
          :y2="HEIGHT - PAD.bottom"
          class="stroke-border"
          stroke-width="1"
        />
        <text
          :x="PAD.left - 8"
          :y="PAD.top + 4"
          text-anchor="end"
          class="fill-muted-foreground text-[10px]"
        >
          {{ format.price(geometry.maxY, currency) }}
        </text>
        <text
          :x="PAD.left - 8"
          :y="HEIGHT - PAD.bottom"
          text-anchor="end"
          class="fill-muted-foreground text-[10px]"
        >
          {{ format.price(geometry.minY, currency) }}
        </text>
        <polyline
          fill="none"
          class="stroke-terracotta"
          stroke-width="2.5"
          stroke-linecap="round"
          stroke-linejoin="round"
          :points="geometry.polyline"
          :style="reducedMotion ? undefined : { transition: 'opacity 150ms ease' }"
        />
        <circle
          v-for="(c, i) in geometry.coords"
          :key="i"
          :cx="c.x"
          :cy="c.y"
          r="3.5"
          class="fill-terracotta"
        />
        <g v-if="hover">
          <line
            :x1="hover.x"
            :x2="hover.x"
            :y1="PAD.top"
            :y2="HEIGHT - PAD.bottom"
            class="stroke-muted-foreground/40"
            stroke-dasharray="4 4"
          />
          <circle
            :cx="hover.x"
            :cy="hover.y"
            r="5"
            class="fill-background stroke-terracotta"
            stroke-width="2"
          />
          <rect
            :x="Math.min(hover.x + 8, WIDTH - 130)"
            :y="Math.max(hover.y - 36, 4)"
            width="120"
            height="32"
            rx="6"
            class="fill-foreground/90"
          />
          <text
            :x="Math.min(hover.x + 8, WIDTH - 130) + 8"
            :y="Math.max(hover.y - 36, 4) + 14"
            class="fill-background text-[10px]"
          >
            {{ format.date(hover.point.observedAt, 'short') }}
          </text>
          <text
            :x="Math.min(hover.x + 8, WIDTH - 130) + 8"
            :y="Math.max(hover.y - 36, 4) + 26"
            class="fill-background text-[10px] font-semibold"
          >
            {{ format.price(hover.point.amount, hover.point.currency) }}
          </text>
        </g>
      </svg>
    </div>

    <table class="sr-only">
      <caption>
        {{
          t('homes.chart.tableCaption')
        }}
      </caption>
      <thead>
        <tr>
          <th scope="col">{{ t('homes.chart.date') }}</th>
          <th scope="col">{{ t('homes.chart.price') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(point, i) in sorted" :key="i">
          <td>
            <time :datetime="point.observedAt">{{ format.dateTime(point.observedAt) }}</time>
          </td>
          <td>{{ format.price(point.amount, point.currency) }}</td>
        </tr>
      </tbody>
    </table>
  </figure>
</template>
