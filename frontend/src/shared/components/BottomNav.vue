<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { Ellipsis, LogOut } from '@lucide/vue'
import { motion } from 'motion-v'
import LocaleSwitcher from '@/shared/components/LocaleSwitcher.vue'
import ThemeToggle from '@/shared/components/ThemeToggle.vue'
import { Button } from '@/shared/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/shared/components/ui/sheet'
import { formatBadge, useNavBadges } from '@/shared/composables/useNavBadges'
import { cn } from '@/shared/lib/cn'
import { MOBILE_MORE_ITEMS, MOBILE_PRIMARY_ITEMS, sectionForPath } from '@/shared/navigation'

const emit = defineEmits<{ signOut: [] }>()

const { t } = useI18n()
const route = useRoute()
const { badges } = useNavBadges()
const moreOpen = ref(false)

const activeSection = computed(() => sectionForPath(route.path))
const moreActive = computed(() =>
  MOBILE_MORE_ITEMS.some((item) => item.key === activeSection.value),
)

watch(
  () => route.fullPath,
  () => (moreOpen.value = false),
)

const itemClass =
  'relative flex min-h-16 flex-col items-center justify-center gap-1 px-1 text-[0.7rem] font-semibold tracking-wide transition-colors'
</script>

<template>
  <nav
    :aria-label="t('a11y.mobileNavigation')"
    class="pb-safe fixed inset-x-0 bottom-0 z-40 border-t bg-surface/90 shadow-[0_-8px_24px_-16px_rgb(0_0_0/0.25)] backdrop-blur-lg md:hidden"
    data-testid="bottom-nav"
  >
    <ul class="mx-auto grid max-w-lg grid-cols-5">
      <li v-for="item in MOBILE_PRIMARY_ITEMS" :key="item.key">
        <RouterLink v-slot="{ href, navigate, isExactActive }" :to="item.to" custom>
          <a
            :href="href"
            :aria-current="isExactActive ? 'page' : activeSection === item.key ? 'true' : undefined"
            :data-testid="`bottom-nav-${item.key}`"
            :class="
              cn(itemClass, activeSection === item.key ? 'text-primary' : 'text-muted-foreground')
            "
            @click="navigate"
          >
            <span class="relative grid h-8 w-14 place-items-center">
              <motion.span
                v-if="activeSection === item.key"
                layout-id="bottom-nav-pill"
                class="absolute inset-0 rounded-full bg-sage-soft"
                :transition="{ type: 'spring', stiffness: 520, damping: 34 }"
                aria-hidden="true"
              />
              <component :is="item.icon" class="relative size-5" aria-hidden="true" />
              <span
                v-if="(badges[item.key] ?? 0) > 0"
                class="numeric absolute -top-1 right-1 min-w-4 rounded-full bg-notify px-1 text-center text-[0.625rem] leading-4 font-bold text-notify-foreground"
              >
                {{ formatBadge(badges[item.key] ?? 0) }}
                <span class="sr-only">{{ t('a11y.newItems', { count: badges[item.key] }) }}</span>
              </span>
            </span>
            <span class="max-w-full truncate">{{ t(item.labelKey) }}</span>
          </a>
        </RouterLink>
      </li>
      <li>
        <Sheet v-model:open="moreOpen">
          <SheetTrigger as-child>
            <button
              type="button"
              :class="
                cn(itemClass, 'w-full', moreActive ? 'text-primary' : 'text-muted-foreground')
              "
              data-testid="bottom-nav-more"
            >
              <span class="relative grid h-8 w-14 place-items-center">
                <span
                  v-if="moreActive"
                  class="absolute inset-0 rounded-full bg-sage-soft"
                  aria-hidden="true"
                />
                <Ellipsis class="relative size-5" aria-hidden="true" />
              </span>
              <span>{{ t('common.more') }}</span>
            </button>
          </SheetTrigger>
          <SheetContent
            side="bottom"
            :close-label="t('common.close')"
            class="pb-safe rounded-t-2xl"
          >
            <SheetHeader class="pb-0">
              <SheetTitle class="font-display text-xl">{{ t('common.more') }}</SheetTitle>
              <SheetDescription class="sr-only">{{ t('a11y.moreNavigation') }}</SheetDescription>
            </SheetHeader>
            <nav :aria-label="t('a11y.moreNavigation')" class="px-4">
              <ul class="grid gap-1">
                <li v-for="item in MOBILE_MORE_ITEMS" :key="item.key">
                  <RouterLink
                    :to="item.to"
                    :data-testid="`more-nav-${item.key}`"
                    :aria-current="activeSection === item.key ? 'page' : undefined"
                    :class="
                      cn(
                        'flex min-h-12 items-center gap-3 rounded-lg px-3 text-base font-medium transition-colors',
                        activeSection === item.key
                          ? 'bg-sage-soft text-primary'
                          : 'text-foreground hover:bg-muted',
                      )
                    "
                  >
                    <component :is="item.icon" class="size-5" aria-hidden="true" />
                    {{ t(item.labelKey) }}
                  </RouterLink>
                </li>
              </ul>
            </nav>
            <div class="mx-4 mb-4 grid grid-cols-2 gap-2 border-t pt-4">
              <ThemeToggle show-label side="top" class="min-h-11 border" />
              <LocaleSwitcher show-label side="top" class="min-h-11 border" />
              <Button
                variant="ghost"
                class="col-span-2 min-h-11 justify-start gap-3 border px-3"
                @click="emit('signOut')"
              >
                <LogOut class="size-[1.125rem]" aria-hidden="true" />
                {{ t('auth.signOut') }}
              </Button>
            </div>
          </SheetContent>
        </Sheet>
      </li>
    </ul>
  </nav>
</template>
