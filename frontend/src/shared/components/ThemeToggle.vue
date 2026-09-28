<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Monitor, Moon, Sun } from '@lucide/vue'
import { AnimatePresence, motion } from 'motion-v'
import { Button } from '@/shared/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/components/ui/dropdown-menu'
import { useTheme } from '@/shared/composables/useTheme'
import { cn } from '@/shared/lib/cn'
import { THEME_PREFERENCES, type ThemePreference } from '@/stores/preferences'

const props = withDefaults(
  defineProps<{
    showLabel?: boolean
    side?: 'top' | 'right' | 'bottom' | 'left'
    align?: 'start' | 'center' | 'end'
    class?: string
  }>(),
  { showLabel: false, side: 'top', align: 'start', class: undefined },
)

const { t } = useI18n()
const { preference, setTheme } = useTheme()

const ICONS = { system: Monitor, light: Sun, dark: Moon } as const

const options = computed(() =>
  THEME_PREFERENCES.map((value) => ({ value, label: t(`theme.${value}`), icon: ICONS[value] })),
)
const currentName = computed(() => t(`theme.${preference.value}`))
const selected = computed({
  get: () => preference.value,
  set: (value: string) => setTheme(value as ThemePreference),
})
</script>

<template>
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <Button
        variant="ghost"
        :size="showLabel ? 'default' : 'icon'"
        :aria-label="t('theme.current', { theme: currentName })"
        data-testid="theme-toggle"
        :class="cn(showLabel && 'w-full justify-start gap-3 px-3', props.class)"
      >
        <span class="relative grid size-5 place-items-center" aria-hidden="true">
          <AnimatePresence mode="wait" :initial="false">
            <motion.span
              :key="preference"
              class="grid place-items-center"
              :initial="{ rotate: -90, scale: 0.4, opacity: 0 }"
              :animate="{ rotate: 0, scale: 1, opacity: 1 }"
              :exit="{ rotate: 90, scale: 0.4, opacity: 0 }"
              :transition="{ type: 'spring', stiffness: 460, damping: 24 }"
            >
              <component :is="ICONS[preference]" class="size-[1.125rem]" />
            </motion.span>
          </AnimatePresence>
        </span>
        <span v-if="showLabel" class="truncate">{{ currentName }}</span>
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent :side="side" :align="align" class="w-48">
      <DropdownMenuLabel>{{ t('theme.label') }}</DropdownMenuLabel>
      <DropdownMenuSeparator />
      <DropdownMenuRadioGroup v-model="selected">
        <DropdownMenuRadioItem
          v-for="option in options"
          :key="option.value"
          :value="option.value"
          :data-testid="`theme-option-${option.value}`"
        >
          <component :is="option.icon" class="size-4" aria-hidden="true" />
          {{ option.label }}
        </DropdownMenuRadioItem>
      </DropdownMenuRadioGroup>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
