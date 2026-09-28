<script setup lang="ts">
import { useAnnouncer } from '@/shared/composables/useAnnouncer'

const { polite, assertive } = useAnnouncer()
</script>

<template>
  <div class="sr-only">
    <div aria-live="polite" aria-atomic="true" data-testid="live-polite">{{ polite }}</div>
    <div aria-live="assertive" aria-atomic="true" data-testid="live-assertive">
      {{ assertive }}
    </div>
  </div>
</template>
