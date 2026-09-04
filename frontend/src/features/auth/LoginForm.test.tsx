import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { LoginForm } from './LoginForm'

describe('LoginForm', () => {
  it('shows no field errors before the first submit attempt', () => {
    render(<LoginForm onSubmit={vi.fn()} submitting={false} />)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows inline errors and does not call onSubmit when fields are missing', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<LoginForm onSubmit={onSubmit} submitting={false} />)

    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(screen.getByText('Email is required.')).toBeInTheDocument()
    expect(screen.getByText('Password is required.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('submits the entered credentials once both fields are valid', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(<LoginForm onSubmit={onSubmit} submitting={false} />)

    await user.type(screen.getByLabelText('Email'), 'viewer@example.com')
    await user.type(screen.getByLabelText('Password'), 'secret')
    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(onSubmit).toHaveBeenCalledTimes(1)
    expect(onSubmit).toHaveBeenCalledWith({ email: 'viewer@example.com', password: 'secret' })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('renders a server error message when provided', () => {
    render(<LoginForm onSubmit={vi.fn()} submitting={false} serverError="Invalid email or password." />)

    expect(screen.getByText('Invalid email or password.')).toBeInTheDocument()
  })

  it('disables the submit button and shows a busy label while submitting', () => {
    render(<LoginForm onSubmit={vi.fn()} submitting={true} />)

    const button = screen.getByRole('button', { name: 'Signing in…' })
    expect(button).toBeDisabled()
  })
})
