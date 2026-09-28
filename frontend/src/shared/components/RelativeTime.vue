<script setup lang="ts">
import { computed } from 'vue'
import { useFormat, useTicker } from '@/shared/composables/useFormat'

const props = defineProps<{ value: string | null | undefined; fallback?: string }>()

const format = useFormat()
const now = useTicker()

const label = computed(() => (props.value ? format.relative(props.value, now.value) : (props.fallback ?? '—')))
const full = computed(() => (props.value ? format.dateTime(props.value, 'long') : undefined))
</script>

<template>
  <time v-if="value" :datetime="value" :title="full">{{ label }}</time>
  <span v-else>{{ label }}</span>
</template>
