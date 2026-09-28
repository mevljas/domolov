<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { problemMessage } from '@/api/problem'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import { Button } from '@/shared/components/ui/button'
import { useToast } from '@/shared/composables/useToast'
import { useDeleteAllListings } from './api'

const { t } = useI18n()
const toast = useToast()
const deleteAll = useDeleteAllListings()
const open = ref(false)

async function onConfirm() {
  try {
    await deleteAll.mutateAsync()
    open.value = false
    toast.success(t('settings.danger.deleted'))
  } catch (error) {
    toast.error(problemMessage(error, t))
  }
}
</script>

<template>
  <section class="grid gap-4" aria-labelledby="settings-danger">
    <h2
      id="settings-danger"
      class="font-display text-xl font-semibold tracking-tight text-destructive"
    >
      {{ t('settings.danger.title') }}
    </h2>
    <p class="text-sm text-muted-foreground">{{ t('settings.danger.description') }}</p>

    <div class="rounded-2xl border border-destructive/30 bg-destructive-soft/40 p-4">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p class="font-medium">{{ t('listings.deleteAll') }}</p>
          <p class="mt-1 text-sm text-muted-foreground">{{ t('listings.deleteAllConfirm') }}</p>
        </div>
        <Button variant="destructive" data-testid="delete-all-listings" @click="open = true">
          {{ t('listings.deleteAll') }}
        </Button>
      </div>
    </div>

    <ConfirmDialog
      v-model:open="open"
      :title="t('listings.deleteAll')"
      :description="t('listings.deleteAllConfirm')"
      :confirm-label="t('listings.deleteAll')"
      :confirm-text="t('settings.danger.confirmPhrase')"
      destructive
      :loading="deleteAll.isPending.value"
      :close-on-confirm="false"
      @confirm="onConfirm"
    />
  </section>
</template>
