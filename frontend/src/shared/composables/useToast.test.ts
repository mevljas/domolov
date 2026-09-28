import { beforeEach, describe, expect, it, vi } from 'vitest'
import { withSetup } from '@/test/with-setup'

const toastMock = vi.hoisted(() => {
  const fn = vi.fn() as ReturnType<typeof vi.fn> & Record<string, ReturnType<typeof vi.fn>>
  for (const key of ['success', 'error', 'info', 'warning', 'promise', 'dismiss']) fn[key] = vi.fn()
  return fn
})

vi.mock('vue-sonner', () => ({ toast: toastMock }))

const { useToast } = await import('./useToast')

interface UndoOptions {
  action: { label: string; onClick: () => void }
  onAutoClose: () => void
  onDismiss: () => void
  duration: number
}

describe('useToast', () => {
  beforeEach(() => vi.clearAllMocks())

  it('exposes the vue-sonner helpers', () => {
    const { result } = withSetup(useToast)
    result.success('Saved')
    expect(toastMock.success).toHaveBeenCalledWith('Saved')
  })

  it('adds a localized Undo action that reverts and confirms', () => {
    const { result } = withSetup(useToast, { locale: 'sl' })
    const onUndo = vi.fn()
    const onCommit = vi.fn()

    result.withUndo('Bookmark removed', onUndo, { onCommit })
    const options = toastMock.mock.calls[0]![1] as UndoOptions

    expect(options.action.label).toBe('Razveljavi')
    expect(options.duration).toBe(6000)
    options.action.onClick()
    expect(onUndo).toHaveBeenCalledOnce()
    expect(toastMock.success).toHaveBeenCalledWith('Razveljavljeno.', { duration: 2000 })

    options.onAutoClose()
    options.onDismiss()
    expect(onCommit).not.toHaveBeenCalled()
  })

  it('commits when the toast closes without Undo', () => {
    const { result } = withSetup(useToast)
    const onCommit = vi.fn()
    result.withUndo('Deleted', vi.fn(), { onCommit, duration: 1000 })
    const options = toastMock.mock.calls[0]![1] as UndoOptions
    options.onAutoClose()
    expect(onCommit).toHaveBeenCalledOnce()
    expect(options.duration).toBe(1000)
  })
})
