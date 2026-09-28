import { useI18n } from 'vue-i18n'
import { toast } from 'vue-sonner'

export interface UndoToastOptions {
  description?: string
  duration?: number
  /** Called when the toast closes without Undo being pressed. */
  onCommit?: () => void
}

/** Themed toasts (vue-sonner) with a localized Undo action helper. */
export function useToast() {
  const { t } = useI18n()

  function withUndo(message: string, onUndo: () => void, options: UndoToastOptions = {}) {
    let undone = false
    return toast(message, {
      description: options.description,
      duration: options.duration ?? 6000,
      action: {
        label: t('common.undo'),
        onClick: () => {
          undone = true
          onUndo()
          toast.success(t('toast.undone'), { duration: 2000 })
        },
      },
      onAutoClose: () => {
        if (!undone) options.onCommit?.()
      },
      onDismiss: () => {
        if (!undone) options.onCommit?.()
      },
    })
  }

  return {
    message: toast,
    success: toast.success,
    error: toast.error,
    info: toast.info,
    warning: toast.warning,
    promise: toast.promise,
    dismiss: toast.dismiss,
    withUndo,
  }
}
