import { computed, ref, watch, type MaybeRefOrGetter, toValue } from 'vue'
import { useI18n } from 'vue-i18n'
import { problemMessage } from '@/api/problem'
import { useScans } from '@/features/scans/api'
import { useScanEvents } from '@/features/scans/composables/useScanEvents'
import { useWatch } from '@/features/watches/api'
import { useWatchCommands } from '@/features/watches/composables/useWatchCommands'
import { urlHost } from '@/features/watches/lib/urlHost'
import { createCronModel, describeCron, parseCron, toCron, validateCron } from '@/shared/lib/cron'
import { isAppLocale } from '@/shared/lib/locale'
import { useToast } from '@/shared/composables/useToast'

/** Schedule editing and the loaded Watch for the detail route. */
export function useWatchDetail(id: MaybeRefOrGetter<string>) {
  const { t, locale } = useI18n()
  const toast = useToast()
  useScanEvents()

  const watchQuery = useWatch(() => toValue(id))
  const watchData = computed(() => watchQuery.data.value)
  const commands = useWatchCommands(watchData)
  const scansQuery = useScans(() => ({ watchId: toValue(id), page: 1, pageSize: 20 }))

  const scheduleModel = ref(createCronModel())
  const savingSchedule = ref(false)

  const appLocale = computed(() => (isAppLocale(locale.value) ? locale.value : 'sl'))
  const host = computed(() => (watchData.value ? urlHost(watchData.value.searchUrl) : ''))
  const scheduleSummary = computed(() =>
    watchData.value ? describeCron(watchData.value.cron, appLocale.value) : '',
  )
  const isCooling = computed(() => {
    const until = watchData.value?.cloudflareBlockedUntil
    return until ? new Date(until).getTime() > Date.now() : false
  })
  const scheduleValid = computed(() =>
    scheduleModel.value.mode === 'custom'
      ? validateCron(scheduleModel.value.customCron?.trim() || '').valid
      : true,
  )

  watch(
    () => watchData.value?.cron,
    (cron) => {
      if (cron) scheduleModel.value = parseCron(cron)
    },
    { immediate: true },
  )

  async function saveSchedule() {
    if (!watchData.value || !scheduleValid.value) return
    savingSchedule.value = true
    try {
      await commands.updateWatch.mutateAsync({
        watch: watchData.value,
        changes: { cron: toCron(scheduleModel.value) },
      })
      toast.success(t('watches.scheduleSaved'))
    } catch (error) {
      toast.error(problemMessage(error, t))
    } finally {
      savingSchedule.value = false
    }
  }

  return {
    watchQuery,
    watchData,
    scansQuery,
    scheduleModel,
    savingSchedule,
    host,
    scheduleSummary,
    isCooling,
    scheduleValid,
    saveSchedule,
    ...commands,
  }
}
