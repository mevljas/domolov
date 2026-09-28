<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import LocaleSwitcher from '@/shared/components/LocaleSwitcher.vue'
import ThemeToggle from '@/shared/components/ThemeToggle.vue'
import { Label } from '@/shared/components/ui/label'

const { t } = useI18n()
</script>

<template>
  <section class="grid gap-4" aria-labelledby="settings-appearance">
    <h2 id="settings-appearance" class="font-display text-xl font-semibold tracking-tight">
      {{ t('settings.appearance.title') }}
    </h2>
    <p class="text-sm text-muted-foreground">{{ t('settings.appearance.description') }}</p>
    <div class="grid gap-4 rounded-2xl border bg-card p-4 shadow-xs sm:grid-cols-2">
      <div class="flex items-center justify-between gap-3">
        <Label>{{ t('theme.label') }}</Label>
        <ThemeToggle show-label class="w-auto" />
      </div>
      <div class="flex items-center justify-between gap-3">
        <Label>{{ t('locale.label') }}</Label>
        <LocaleSwitcher show-label class="w-auto" />
      </div>
    </div>
  </section>
</template>
