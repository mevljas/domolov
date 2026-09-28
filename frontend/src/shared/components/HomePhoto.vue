<script setup lang="ts">
import { ref, watch } from 'vue'
import { House } from '@lucide/vue'
import RooflinePattern from '@/shared/components/brand/RooflinePattern.vue'
import { cn } from '@/shared/lib/cn'

const props = withDefaults(
  defineProps<{
    src: string | null | undefined
    alt: string
    width?: number
    height?: number
    /** Eager-load above-the-fold images (detail hero). */
    eager?: boolean
    class?: string
    imgClass?: string
  }>(),
  { width: 800, height: 600, eager: false, class: undefined, imgClass: undefined },
)

const failed = ref(false)
const loaded = ref(false)

watch(
  () => props.src,
  () => {
    failed.value = false
    loaded.value = false
  },
)
</script>

<template>
  <div
    :class="cn('relative overflow-hidden bg-muted', props.class)"
    :style="{ aspectRatio: `${width} / ${height}` }"
  >
    <div
      v-if="!src || failed || !loaded"
      class="absolute inset-0 grid place-items-center bg-gradient-to-br from-sage-soft to-muted text-sage"
      aria-hidden="true"
    >
      <RooflinePattern :rows="2" class="absolute inset-x-0 bottom-0 h-3/5 w-full opacity-60" />
      <House v-if="!src || failed" class="relative size-8 text-primary/40" />
    </div>
    <img
      v-if="src && !failed"
      :src="src"
      :alt="alt"
      :width="width"
      :height="height"
      :loading="eager ? 'eager' : 'lazy'"
      :fetchpriority="eager ? 'high' : 'auto'"
      decoding="async"
      referrerpolicy="no-referrer"
      :class="
        cn(
          'relative size-full object-cover transition-opacity duration-300',
          loaded ? 'opacity-100' : 'opacity-0',
          imgClass,
        )
      "
      @load="loaded = true"
      @error="failed = true"
    />
  </div>
</template>
