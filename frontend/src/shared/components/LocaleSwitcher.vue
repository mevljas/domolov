<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { Languages } from '@lucide/vue'
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
import { useLocale } from '@/shared/composables/useLocale'
import { cn } from '@/shared/lib/cn'
import type { AppLocale } from '@/shared/lib/locale'

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
const { locale, locales, setLocale } = useLocale()

const selected = computed({
  get: () => locale.value,
  set: (value: string) => setLocale(value as AppLocale),
})
</script>

<template>
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <Button
        variant="ghost"
        :size="showLabel ? 'default' : 'icon'"
        :aria-label="`${t('locale.switch')}: ${t(`locale.${locale}`)}`"
        data-testid="locale-switcher"
        :class="cn(showLabel && 'w-full justify-start gap-3 px-3', props.class)"
      >
        <Languages class="size-[1.125rem]" aria-hidden="true" />
        <span v-if="showLabel" class="truncate">{{ t(`locale.${locale}`) }}</span>
      </Button>
    </DropdownMenuTrigger>
    <DropdownMenuContent :side="side" :align="align" class="w-48">
      <DropdownMenuLabel>{{ t('locale.label') }}</DropdownMenuLabel>
      <DropdownMenuSeparator />
      <DropdownMenuRadioGroup v-model="selected">
        <DropdownMenuRadioItem
          v-for="code in locales"
          :key="code"
          :value="code"
          :lang="code"
          :data-testid="`locale-option-${code}`"
        >
          {{ t(`locale.${code}`) }}
        </DropdownMenuRadioItem>
      </DropdownMenuRadioGroup>
    </DropdownMenuContent>
  </DropdownMenu>
</template>
