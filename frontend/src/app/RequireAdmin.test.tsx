import { render, screen } from '@testing-library/react'
import { Route, MemoryRouter, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { setAuthSession, type AuthSession } from '../shared/auth/accessTokenStore'
import { RequireAdmin } from './RequireAdmin'

function session(overrides: Partial<AuthSession> = {}): AuthSession {
  return {
    accessToken: 'token',
    role: 'Admin',
    email: 'admin@example.com',
    expiresAt: Date.now() + 900_000,
    ...overrides,
  }
}

function renderGuarded() {
  return render(
    <MemoryRouter initialEntries={['/administration']}>
      <Routes>
        <Route path="/" element={<h1>Overview</h1>} />
        <Route
          path="/administration"
          element={
            <RequireAdmin>
              <h1>Administration</h1>
            </RequireAdmin>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireAdmin', () => {
  afterEach(() => {
    setAuthSession(null)
  })

  it('renders the protected content for an admin session', () => {
    setAuthSession(session({ role: 'Admin' }))
    renderGuarded()

    expect(screen.getByRole('heading', { name: 'Administration' })).toBeInTheDocument()
  })

  it('redirects a viewer session away from the route', () => {
    setAuthSession(session({ role: 'Viewer' }))
    renderGuarded()

    expect(screen.queryByRole('heading', { name: 'Administration' })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Overview' })).toBeInTheDocument()
  })

  it('redirects an anonymous visitor away from the route', () => {
    renderGuarded()

    expect(screen.queryByRole('heading', { name: 'Administration' })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Overview' })).toBeInTheDocument()
  })
})
