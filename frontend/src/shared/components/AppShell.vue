<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Search } from '@lucide/vue'
import BottomNav from '@/shared/components/BottomNav.vue'
import BrandMark from '@/shared/components/brand/BrandMark.vue'
import BrandWordmark from '@/shared/components/brand/BrandWordmark.vue'
import CommandPalette from '@/shared/components/CommandPalette.vue'
import OfflineBanner from '@/shared/components/OfflineBanner.vue'
import SideNav from '@/shared/components/SideNav.vue'
import { TooltipProvider } from '@/shared/components/ui/tooltip'
import { isApplePlatform, useCommandPalette } from '@/shared/composables/useCommandPalette'
import { useSidebar } from '@/shared/composables/useSidebar'
import { cn } from '@/shared/lib/cn'
import type { NavItem } from '@/shared/navigation'

const emit = defineEmits<{ signOut: [] }>()

defineSlots<{
  default?: () => unknown
  'header-actions'?: () => unknown
  'nav-badge'?: (props: { item: NavItem; count: number; collapsed: boolean }) => unknown
}>()

const { t } = useI18n()
const { collapsed, toggle } = useSidebar()
const palette = useCommandPalette()

const shortcutLabel = computed(() => (isApplePlatform() ? '⌘ K' : 'Ctrl K'))

function focusMain(event: MouseEvent) {
  const main = document.getElementById('main')
  if (!main) return
  event.preventDefault()
  main.focus()
  main.scrollIntoView()
}
</script>

<template>
  <TooltipProvider :delay-duration="250">
    <div class="min-h-dvh">
      <a href="#main" class="skip-link" @click="focusMain">{{ t('a11y.skipToContent') }}</a>

      <aside
        :class="
          cn(
            'fixed inset-y-0 left-0 z-40 hidden border-r border-sidebar-border bg-sidebar transition-[width] duration-300 ease-(--ease-out-soft) md:block',
            collapsed ? 'w-[4.75rem]' : 'w-64',
          )
        "
        data-testid="sidebar"
        :data-collapsed="collapsed"
      >
        <SideNav :collapsed="collapsed" @toggle="toggle" @sign-out="emit('signOut')">
          <template v-if="$slots['nav-badge']" #badge="badgeProps">
            <slot name="nav-badge" v-bind="badgeProps" />
          </template>
        </SideNav>
      </aside>

      <div
        :class="
          cn(
            'flex min-h-dvh flex-col transition-[padding] duration-300 ease-(--ease-out-soft)',
            collapsed ? 'md:pl-[4.75rem]' : 'md:pl-64',
          )
        "
      >
        <header class="pt-safe sticky top-0 z-30 border-b bg-background/85 backdrop-blur-md">
          <div class="mx-auto flex h-14 w-full max-w-6xl items-center gap-3 px-4 md:h-16 md:px-8">
            <RouterLink to="/" class="flex items-center gap-2 rounded-md md:hidden">
              <BrandMark class="size-8" decorative />
              <BrandWordmark class="mt-0.5 text-[1.35rem]" decorative />
              <span class="sr-only">{{ t('common.appName') }} — {{ t('nav.dashboard') }}</span>
            </RouterLink>

            <button
              type="button"
              class="group ml-auto flex h-10 items-center gap-2.5 rounded-full border bg-surface px-3 text-sm text-muted-foreground shadow-xs transition-[color,box-shadow,border-color] hover:border-input/60 hover:text-foreground hover:shadow-sm md:ml-0 md:w-80 md:rounded-lg"
              aria-haspopup="dialog"
              :aria-expanded="palette.open.value"
              data-testid="command-trigger"
              @click="palette.show"
            >
              <Search class="size-4 shrink-0" aria-hidden="true" />
              <span class="sr-only md:not-sr-only md:flex-1 md:text-left">
                {{ t('command.trigger') }}
              </span>
              <kbd
                class="hidden items-center rounded border bg-muted px-1.5 py-0.5 font-sans text-[0.7rem] font-semibold text-muted-foreground md:inline-flex"
                aria-hidden="true"
              >
                {{ shortcutLabel }}
              </kbd>
            </button>

            <div v-if="$slots['header-actions']" class="ml-auto hidden items-center gap-1 md:flex">
              <slot name="header-actions" />
            </div>
          </div>
        </header>

        <OfflineBanner />

        <main
          id="main"
          tabindex="-1"
          class="main-content mx-auto w-full max-w-6xl flex-1 px-4 pt-6 pb-[calc(6.5rem+env(safe-area-inset-bottom))] outline-none md:px-8 md:pt-10 md:pb-16"
        >
          <slot />
        </main>
      </div>

      <BottomNav @sign-out="emit('signOut')" />
      <CommandPalette @sign-out="emit('signOut')" />
    </div>
  </TooltipProvider>
</template>

<style scoped>
.main-content {
  view-transition-name: main-content;
}
</style>
