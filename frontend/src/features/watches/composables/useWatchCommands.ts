import { type MaybeRefOrGetter, toValue } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Watch } from '@/api/types'
import { problemMessage } from '@/api/problem'
import { useDeleteWatch, useRunWatchNow, useUpdateWatch } from '@/features/watches/api'
import { useToast } from '@/shared/composables/useToast'

type WatchRef = Pick<Watch, 'id' | 'version'>

/** Pause, run-now and delete for a Watch, with the shared toast on failure. */
export function useWatchCommands(watch: MaybeRefOrGetter<WatchRef | undefined>) {
  const { t } = useI18n()
  const toast = useToast()
  const updateWatch = useUpdateWatch()
  const deleteWatch = useDeleteWatch()
  const runNow = useRunWatchNow()

  async function setPaused(paused: boolean) {
    const current = toValue(watch)
    if (!current) return
    try {
      await updateWatch.mutateAsync({ watch: current, changes: { isPaused: paused } })
    } catch (error) {
      toast.error(problemMessage(error, t))
    }
  }

  async function run() {
    const current = toValue(watch)
    if (!current) return
    try {
      await runNow.mutateAsync(current.id)
      toast.success(t('watches.runQueued'))
    } catch (error) {
      toast.error(problemMessage(error, t))
    }
  }

  async function remove(after?: () => void | Promise<void>) {
    const current = toValue(watch)
    if (!current) return
    try {
      await deleteWatch.mutateAsync(current)
      toast.success(t('watches.deleted'))
      await after?.()
    } catch (error) {
      toast.error(problemMessage(error, t))
    }
  }

  return { updateWatch, deleteWatch, runNow, setPaused, run, remove }
}
