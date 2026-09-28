<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { LogOut, PanelLeftClose, PanelLeftOpen } from '@lucide/vue'
import BrandMark from '@/shared/components/brand/BrandMark.vue'
import BrandWordmark from '@/shared/components/brand/BrandWordmark.vue'
import LocaleSwitcher from '@/shared/components/LocaleSwitcher.vue'
import ThemeToggle from '@/shared/components/ThemeToggle.vue'
import { Button } from '@/shared/components/ui/button'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/shared/components/ui/tooltip'
import { formatBadge, useNavBadges } from '@/shared/composables/useNavBadges'
import { cn } from '@/shared/lib/cn'
import { NAV_ITEMS, type NavItem, sectionForPath } from '@/shared/navigation'

const props = withDefaults(defineProps<{ collapsed?: boolean }>(), { collapsed: false })

const emit = defineEmits<{ toggle: []; signOut: [] }>()

defineSlots<{
  badge?: (props: { item: NavItem; count: number; collapsed: boolean }) => unknown
}>()

const { t } = useI18n()
const route = useRoute()
const { badges } = useNavBadges()

const activeSection = computed(() => sectionForPath(route.path))

function ariaCurrent(item: NavItem, isExactActive: boolean) {
  if (isExactActive) return 'page'
  return activeSection.value === item.key ? 'true' : undefined
}
</script>

<template>
  <div class="flex h-full flex-col px-3 pt-4 pb-3">
    <RouterLink
      to="/"
      :class="
        cn(
          'flex h-12 items-center gap-2.5 rounded-lg px-1.5 transition-opacity hover:opacity-90',
          props.collapsed && 'justify-center px-0',
        )
      "
    >
      <BrandMark class="size-9 shrink-0 drop-shadow-sm" decorative />
      <BrandWordmark v-if="!props.collapsed" class="mt-0.5 text-[1.55rem]" decorative />
      <span class="sr-only">{{ t('common.appName') }} — {{ t('nav.dashboard') }}</span>
    </RouterLink>

    <nav :aria-label="t('a11y.mainNavigation')" class="mt-6 flex-1 overflow-y-auto">
      <ul class="space-y-1">
        <li v-for="item in NAV_ITEMS" :key="item.key">
          <Tooltip :disabled="!props.collapsed">
            <TooltipTrigger as-child>
              <RouterLink v-slot="{ href, navigate, isExactActive }" :to="item.to" custom>
                <a
                  :href="href"
                  :aria-current="ariaCurrent(item, isExactActive)"
                  :data-testid="`nav-${item.key}`"
                  :class="
                    cn(
                      'group relative flex h-11 items-center gap-3 rounded-lg px-3 text-[0.95rem] font-medium transition-colors duration-150',
                      activeSection === item.key
                        ? 'bg-sidebar-accent font-semibold text-sidebar-accent-foreground shadow-xs'
                        : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-foreground',
                      props.collapsed && 'justify-center px-0',
                    )
                  "
                  @click="navigate"
                >
                  <span
                    v-if="activeSection === item.key"
                    class="absolute top-2 bottom-2 left-0 w-[3px] rounded-full bg-primary"
                    aria-hidden="true"
                  />
                  <span class="relative">
                    <component
                      :is="item.icon"
                      class="size-5 transition-transform duration-200 group-hover:scale-105"
                      aria-hidden="true"
                    />
                    <span
                      v-if="props.collapsed && (badges[item.key] ?? 0) > 0"
                      class="absolute -top-1 -right-1 size-2.5 rounded-full bg-notify ring-2 ring-sidebar"
                      aria-hidden="true"
                    />
                  </span>
                  <span :class="props.collapsed ? 'sr-only' : 'flex-1 truncate'">
                    {{ t(item.labelKey) }}
                  </span>
                  <slot
                    name="badge"
                    :item="item"
                    :count="badges[item.key] ?? 0"
                    :collapsed="props.collapsed"
                  >
                    <span
                      v-if="(badges[item.key] ?? 0) > 0"
                      :class="
                        props.collapsed
                          ? 'sr-only'
                          : 'numeric ml-auto min-w-6 rounded-full bg-notify px-1.5 py-0.5 text-center text-xs font-semibold text-notify-foreground'
                      "
                    >
                      {{ formatBadge(badges[item.key] ?? 0) }}
                      <span class="sr-only">{{
                        t('a11y.newItems', { count: badges[item.key] })
                      }}</span>
                    </span>
                  </slot>
                </a>
              </RouterLink>
            </TooltipTrigger>
            <TooltipContent side="right">{{ t(item.labelKey) }}</TooltipContent>
          </Tooltip>
        </li>
      </ul>
    </nav>

    <div
      :class="cn('mt-4 space-y-1 border-t pt-3', props.collapsed && 'flex flex-col items-center')"
    >
      <ThemeToggle :show-label="!props.collapsed" side="right" align="end" />
      <LocaleSwitcher :show-label="!props.collapsed" side="right" align="end" />
      <Button
        variant="ghost"
        :size="props.collapsed ? 'icon' : 'default'"
        :class="cn(!props.collapsed && 'w-full justify-start gap-3 px-3')"
        :aria-label="props.collapsed ? t('auth.signOut') : undefined"
        data-testid="sign-out"
        @click="emit('signOut')"
      >
        <LogOut class="size-[1.125rem]" aria-hidden="true" />
        <span v-if="!props.collapsed">{{ t('auth.signOut') }}</span>
      </Button>
      <Button
        variant="ghost"
        :size="props.collapsed ? 'icon' : 'default'"
        :class="cn('text-muted-foreground', !props.collapsed && 'w-full justify-start gap-3 px-3')"
        :aria-label="props.collapsed ? t('a11y.expandSidebar') : undefined"
        :aria-expanded="!props.collapsed"
        data-testid="sidebar-toggle"
        @click="emit('toggle')"
      >
        <PanelLeftOpen v-if="props.collapsed" class="size-[1.125rem]" aria-hidden="true" />
        <PanelLeftClose v-else class="size-[1.125rem]" aria-hidden="true" />
        <span v-if="!props.collapsed">{{ t('a11y.collapseSidebar') }}</span>
      </Button>
    </div>
  </div>
</template>
