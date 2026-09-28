<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Settings } from '@/api/types'
import { Badge } from '@/shared/components/ui/badge'

defineProps<{
  settings: Settings
}>()

const { t } = useI18n()

function yesNo(value: boolean) {
  return value ? t('common.yes') : t('common.no')
}

function channel(configured: boolean) {
  return configured ? t('common.configured') : t('common.notConfigured')
}
</script>

<template>
  <section class="grid gap-4" aria-labelledby="settings-server">
    <h2 id="settings-server" class="font-display text-xl font-semibold tracking-tight">
      {{ t('settings.server.title') }}
    </h2>
    <p class="text-sm text-muted-foreground">{{ t('settings.server.description') }}</p>

    <dl class="grid gap-x-8 gap-y-3 rounded-2xl border bg-card p-4 shadow-xs sm:grid-cols-2">
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.server.version') }}</dt>
        <dd class="font-medium tabular-nums">{{ settings.version }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.timeZone') }}</dt>
        <dd class="font-medium">{{ settings.timeZone }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.role') }}</dt>
        <dd>
          <Badge variant="outline">{{ settings.role }}</Badge>
        </dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.maxConcurrentScans') }}</dt>
        <dd class="font-medium tabular-nums">{{ settings.maxConcurrentScans }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.scanCooldownMs') }}</dt>
        <dd class="font-medium tabular-nums">{{ settings.scanCooldownMs }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.cloudflareChallengeWaitMs') }}</dt>
        <dd class="font-medium tabular-nums">{{ settings.cloudflareChallengeWaitMs }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.browserHeadless') }}</dt>
        <dd class="font-medium">{{ yesNo(settings.browserHeadless) }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.server.fakeProvider') }}</dt>
        <dd class="font-medium">{{ yesNo(settings.fakeProvider) }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.server.channels') }}</dt>
        <dd class="space-y-1 text-sm">
          <p>Telegram · {{ channel(settings.telegramConfigured) }}</p>
          <p>SMTP · {{ channel(settings.smtpConfigured) }}</p>
          <p>Web Push · {{ channel(settings.vapidConfigured) }}</p>
        </dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.server.publicUrl') }}</dt>
        <dd class="truncate font-medium">{{ settings.publicUrl || '—' }}</dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.server.retention') }}</dt>
        <dd class="space-y-0.5 text-sm">
          <p>
            {{ t('settings.server.retentionDelisted', { days: settings.retention.delistedDays }) }}
          </p>
          <p>
            {{
              t('settings.server.retentionOrphan', { days: settings.retention.orphanListingDays })
            }}
          </p>
          <p>
            {{ t('settings.server.retentionScans', { days: settings.retention.scanRunDays }) }}
          </p>
          <p>{{ t('settings.server.retentionDailyAt', { time: settings.retention.dailyAt }) }}</p>
        </dd>
      </div>
      <div>
        <dt class="text-xs text-muted-foreground">{{ t('settings.server.match') }}</dt>
        <dd class="space-y-0.5 text-sm">
          <p>
            {{
              t('settings.server.matchAutoLink', {
                score: Math.round(settings.match.autoLinkScore * 100),
              })
            }}
          </p>
          <p>
            {{
              t('settings.server.matchPossible', {
                score: Math.round(settings.match.possibleScore * 100),
              })
            }}
          </p>
        </dd>
      </div>
    </dl>
  </section>
</template>
