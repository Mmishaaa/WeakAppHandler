import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, MemoryRouter, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { AnnouncerProvider } from '../../shared/a11y/AnnouncerProvider'
import { setAuthSession, type AuthSession } from '../../shared/auth/accessTokenStore'
import { AppShell } from './AppShell'

function session(overrides: Partial<AuthSession> = {}): AuthSession {
  return {
    accessToken: 'token',
    role: 'Admin',
    email: 'admin@example.com',
    expiresAt: Date.now() + 900_000,
    ...overrides,
  }
}

function renderShell() {
  return render(
    <AnnouncerProvider>
      <MemoryRouter initialEntries={['/']}>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<h1>Overview</h1>} />
            <Route path="history" element={<h1>History</h1>} />
            <Route path="alerts" element={<h1>Alerts</h1>} />
            <Route path="administration" element={<h1>Administration</h1>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AnnouncerProvider>,
  )
}

describe('AppShell', () => {
  afterEach(() => {
    setAuthSession(null)
  })

  it('exposes a skip link that targets the focusable main landmark', () => {
    renderShell()

    const skipLink = screen.getByRole('link', { name: /skip to main content/i })
    expect(skipLink).toHaveAttribute('href', '#main-content')

    const main = screen.getByRole('main')
    expect(main).toHaveAttribute('id', 'main-content')
    expect(main).toHaveAttribute('tabindex', '-1')
  })

  it('renders all four primary navigation destinations for an admin session, reachable via the keyboard nav landmark', () => {
    setAuthSession(session({ role: 'Admin' }))
    renderShell()

    const nav = screen.getByRole('navigation', { name: /main/i })
    for (const label of ['Overview', 'History', 'Alerts', 'Administration']) {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument()
      expect(nav).toContainElement(screen.getByRole('link', { name: label }))
    }
  })

  it('hides Administration from the nav for a viewer session', () => {
    setAuthSession(session({ role: 'Viewer' }))
    renderShell()

    for (const label of ['Overview', 'History', 'Alerts']) {
      expect(screen.getByRole('link', { name: label })).toBeInTheDocument()
    }
    expect(screen.queryByRole('link', { name: 'Administration' })).not.toBeInTheDocument()
  })

  it('hides Administration from the nav for an anonymous visitor', () => {
    renderShell()

    expect(screen.queryByRole('link', { name: 'Administration' })).not.toBeInTheDocument()
  })

  it('reaches the skip link and every nav item in order via Tab alone', async () => {
    setAuthSession(session({ role: 'Admin' }))
    const user = userEvent.setup()
    renderShell()

    await user.tab()
    expect(screen.getByRole('link', { name: /skip to main content/i })).toHaveFocus()

    for (const label of ['Overview', 'History', 'Alerts', 'Administration']) {
      await user.tab()
      expect(screen.getByRole('link', { name: label })).toHaveFocus()
    }
  })

  it('shows a "Log in" link when no session is active, and signed-in identity plus a log-out control once one is', () => {
    renderShell()
    expect(screen.getByRole('link', { name: 'Log in' })).toBeInTheDocument()

    // AppShell re-renders on the store's own change notification (see useAuthSession) - the
    // listener calls a React state setter outside of any render/event handler, so the update needs
    // an explicit act() to flush before the assertion below runs.
    act(() => setAuthSession(session({ email: 'admin@example.com', role: 'Admin' })))
    expect(screen.getByText('admin@example.com · Admin')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument()
  })

  it('marks the active route with aria-current and navigates when a focused link is activated', async () => {
    setAuthSession(session({ role: 'Admin' }))
    const user = userEvent.setup()
    renderShell()

    const overviewLink = screen.getByRole('link', { name: 'Overview' })
    expect(overviewLink).toHaveAttribute('aria-current', 'page')

    const historyLink = screen.getByRole('link', { name: 'History' })
    historyLink.focus()
    await user.keyboard('{Enter}')

    expect(await screen.findByRole('heading', { name: 'History' })).toBeInTheDocument()
    expect(historyLink).toHaveAttribute('aria-current', 'page')
  })
})
