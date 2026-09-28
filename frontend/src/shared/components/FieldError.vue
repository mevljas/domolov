<script setup lang="ts">
defineProps<{ id: string; message?: string | null }>()
</script>

<template>
  <p v-if="message" :id="id" class="text-sm font-medium text-destructive" role="alert">
    {{ message }}
  </p>
</template>
