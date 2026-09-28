<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { useMediaQuery } from '@vueuse/core'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/components/ui/dialog'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
} from '@/shared/components/ui/sheet'
import { cn } from '@/shared/lib/cn'

withDefaults(
  defineProps<{
    title: string
    description?: string
    /** Tailwind max-width for the desktop dialog. */
    size?: 'md' | 'lg' | 'xl'
    contentClass?: string
  }>(),
  { description: undefined, size: 'md', contentClass: undefined },
)

const open = defineModel<boolean>('open', { default: false })

defineSlots<{ default?: () => unknown; footer?: () => unknown }>()

const { t } = useI18n()
const isDesktop = useMediaQuery('(min-width: 768px)')

const SIZES = { md: 'sm:max-w-lg', lg: 'sm:max-w-2xl', xl: 'sm:max-w-4xl' }
</script>

<template>
  <Dialog v-if="isDesktop" v-model:open="open">
    <DialogContent
      :close-label="t('common.close')"
      :class="cn('max-h-[90dvh] overflow-y-auto', SIZES[size], contentClass)"
    >
      <DialogHeader>
        <DialogTitle class="font-display text-2xl">{{ title }}</DialogTitle>
        <DialogDescription v-if="description">{{ description }}</DialogDescription>
      </DialogHeader>
      <slot />
      <DialogFooter v-if="$slots.footer">
        <slot name="footer" />
      </DialogFooter>
    </DialogContent>
  </Dialog>
  <Sheet v-else v-model:open="open">
    <SheetContent
      side="bottom"
      :close-label="t('common.close')"
      :class="cn('pb-safe max-h-[92dvh] gap-0 overflow-y-auto rounded-t-2xl', contentClass)"
    >
      <SheetHeader>
        <SheetTitle class="font-display text-2xl">{{ title }}</SheetTitle>
        <SheetDescription v-if="description">{{ description }}</SheetDescription>
      </SheetHeader>
      <div class="px-4 pb-2">
        <slot />
      </div>
      <SheetFooter v-if="$slots.footer" class="border-t">
        <slot name="footer" />
      </SheetFooter>
    </SheetContent>
  </Sheet>
</template>
