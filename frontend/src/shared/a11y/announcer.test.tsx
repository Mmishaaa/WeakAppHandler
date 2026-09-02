import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { AnnouncerProvider } from './AnnouncerProvider'
import { useAnnouncer } from './useAnnouncer'

function Announcer({ message }: { message: string }) {
  const { announce } = useAnnouncer()
  return (
    <button type="button" onClick={() => announce(message)}>
      announce
    </button>
  )
}

describe('AnnouncerProvider', () => {
  it('renders a single polite, atomic live region that starts empty', () => {
    render(
      <AnnouncerProvider>
        <div>app</div>
      </AnnouncerProvider>,
    )

    const region = screen.getByRole('status')
    expect(region).toHaveAttribute('aria-live', 'polite')
    expect(region).toHaveAttribute('aria-atomic', 'true')
    expect(region).toHaveTextContent('')
  })

  it('pushes announced messages into the live region', async () => {
    const user = userEvent.setup()

    render(
      <AnnouncerProvider>
        <Announcer message="Navigated to Alerts" />
      </AnnouncerProvider>,
    )

    await user.click(screen.getByRole('button', { name: 'announce' }))

    expect(screen.getByRole('status')).toHaveTextContent('Navigated to Alerts')
  })
})
