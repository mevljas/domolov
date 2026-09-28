<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { MotionConfig } from 'motion-v'
import LiveAnnouncer from '@/shared/components/LiveAnnouncer.vue'
import { Toaster } from '@/shared/components/ui/sonner'
import { TooltipProvider } from '@/shared/components/ui/tooltip'
import { useLocaleEffect } from '@/shared/composables/useLocale'
import { useThemeEffect } from '@/shared/composables/useTheme'
import AppLayout from './AppLayout.vue'

const route = useRoute()
const { resolved } = useThemeEffect()
useLocaleEffect()

const isBare = computed(() => route.meta.layout === 'bare')
</script>

<template>
  <MotionConfig reduced-motion="user">
    <TooltipProvider :delay-duration="250">
      <RouterView v-slot="{ Component }">
        <component :is="Component" v-if="isBare" />
        <AppLayout v-else>
          <component :is="Component" />
        </AppLayout>
      </RouterView>
      <Toaster
        :theme="resolved"
        position="bottom-right"
        close-button
        :mobile-offset="{ bottom: 'calc(5.5rem + env(safe-area-inset-bottom))' }"
      />
      <LiveAnnouncer />
    </TooltipProvider>
  </MotionConfig>
</template>
