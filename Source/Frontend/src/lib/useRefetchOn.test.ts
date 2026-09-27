import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useRefetchOn } from './useRefetchOn'

const setVisibility = (state: DocumentVisibilityState) => {
  Object.defineProperty(document, 'visibilityState', { configurable: true, value: state })
  document.dispatchEvent(new Event('visibilitychange'))
}

describe('useRefetchOn', () => {
  afterEach(() => {
    setVisibility('visible')
  })

  it('does not refetch on mount', () => {
    const refetch = vi.fn()

    renderHook(() => useRefetchOn(1, refetch))

    expect(refetch).not.toHaveBeenCalled()
  })

  it('refetches once per new version', () => {
    const refetch = vi.fn()
    const { rerender } = renderHook(({ version }) => useRefetchOn(version, refetch), {
      initialProps: { version: 1 },
    })

    rerender({ version: 2 })
    rerender({ version: 2 })

    expect(refetch).toHaveBeenCalledTimes(1)
  })

  it('does not refetch when only the callback changes', () => {
    const first = vi.fn()
    const second = vi.fn()
    const { rerender } = renderHook(({ refetch }) => useRefetchOn(1, refetch), {
      initialProps: { refetch: first },
    })

    rerender({ refetch: second })

    expect(first).not.toHaveBeenCalled()
    expect(second).not.toHaveBeenCalled()
  })

  it('waits while the page is hidden and catches up once it is visible', () => {
    const refetch = vi.fn()
    const { rerender } = renderHook(({ version }) => useRefetchOn(version, refetch), {
      initialProps: { version: 1 },
    })

    act(() => setVisibility('hidden'))
    rerender({ version: 2 })
    rerender({ version: 3 })

    expect(refetch).not.toHaveBeenCalled()

    act(() => setVisibility('visible'))

    expect(refetch).toHaveBeenCalledTimes(1)
  })
})
