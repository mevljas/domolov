<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { LoaderCircle } from '@lucide/vue'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/shared/components/ui/alert-dialog'
import { Button } from '@/shared/components/ui/button'
import { Input } from '@/shared/components/ui/input'
import { Label } from '@/shared/components/ui/label'

const props = withDefaults(
  defineProps<{
    title?: string
    description?: string
    confirmLabel?: string
    cancelLabel?: string
    destructive?: boolean
    /** When set, the user must type this exact phrase before confirming. */
    confirmText?: string
    loading?: boolean
    /** Set to false for async work; close by updating v-model:open yourself. */
    closeOnConfirm?: boolean
  }>(),
  {
    title: undefined,
    description: undefined,
    confirmLabel: undefined,
    cancelLabel: undefined,
    destructive: false,
    confirmText: undefined,
    loading: false,
    closeOnConfirm: true,
  },
)

const emit = defineEmits<{ confirm: [] }>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const inputId = useId()
const typed = ref('')

const matches = computed(() => !props.confirmText || typed.value.trim() === props.confirmText)
const showMismatch = computed(
  () => Boolean(props.confirmText) && typed.value.length > 0 && !matches.value,
)

watch(open, (value) => {
  if (!value) typed.value = ''
})

function confirm() {
  if (!matches.value || props.loading) return
  emit('confirm')
  if (props.closeOnConfirm) open.value = false
}
</script>

<template>
  <AlertDialog v-model:open="open">
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle class="font-display text-xl">
          {{ title ?? t('confirm.title') }}
        </AlertDialogTitle>
        <AlertDialogDescription v-if="description">{{ description }}</AlertDialogDescription>
      </AlertDialogHeader>

      <form v-if="confirmText" class="grid gap-2" novalidate @submit.prevent="confirm">
        <Label :for="inputId" class="font-normal text-muted-foreground">
          <i18n-t keypath="confirm.typeToConfirm" tag="span" scope="global">
            <template #phrase>
              <strong class="font-mono font-semibold text-foreground">{{ confirmText }}</strong>
            </template>
          </i18n-t>
        </Label>
        <Input
          :id="inputId"
          v-model="typed"
          autocomplete="off"
          autocapitalize="off"
          spellcheck="false"
          :aria-invalid="showMismatch || undefined"
          :aria-describedby="showMismatch ? `${inputId}-error` : undefined"
          data-testid="confirm-input"
        />
        <p v-if="showMismatch" :id="`${inputId}-error`" class="text-sm text-destructive">
          {{ t('confirm.mismatch') }}
        </p>
      </form>

      <AlertDialogFooter>
        <AlertDialogCancel :disabled="loading">{{
          cancelLabel ?? t('common.cancel')
        }}</AlertDialogCancel>
        <Button
          :variant="destructive ? 'destructive' : 'default'"
          :disabled="!matches || loading"
          :aria-busy="loading || undefined"
          data-testid="confirm-action"
          @click="confirm"
        >
          <LoaderCircle v-if="loading" class="animate-spin" aria-hidden="true" />
          {{ confirmLabel ?? t('common.confirm') }}
        </Button>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
