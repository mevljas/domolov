<script setup lang="ts">
import AppShell from '@/shared/components/AppShell.vue'
import { useSignOutAction } from '@/features/auth/useSignOutAction'

const signOut = useSignOutAction()
</script>

<template>
  <AppShell @sign-out="signOut">
    <slot />
  </AppShell>
</template>
