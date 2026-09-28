<script setup lang="ts">
import type { DiffToken } from '@/shared/lib/diff'

withDefaults(
  defineProps<{
    tokens: DiffToken[]
    as?: 'p' | 'span'
  }>(),
  { as: 'p' },
)
</script>

<template>
  <component
    :is="as"
    :class="
      as === 'p' ? 'text-sm leading-relaxed whitespace-pre-wrap text-muted-foreground' : undefined
    "
  >
    <template v-for="(token, index) in tokens" :key="index">
      <mark
        v-if="token.kind !== 'same'"
        class="rounded-sm bg-terracotta-soft px-0.5 text-terracotta-foreground"
        >{{ token.text }}</mark
      >
      <template v-else>{{ token.text }}</template>
    </template>
  </component>
</template>
