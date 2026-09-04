import { describe, expect, it } from 'vitest'
import { emptyLoginFormValues, isLoginFormValid, validateLoginForm } from './loginValidation'

describe('validateLoginForm', () => {
  it('accepts a well-formed email and non-empty password', () => {
    const errors = validateLoginForm({ email: 'viewer@example.com', password: 'secret' })
    expect(isLoginFormValid(errors)).toBe(true)
  })

  it('requires an email', () => {
    const errors = validateLoginForm({ ...emptyLoginFormValues(), password: 'secret' })
    expect(errors.email).toBe('Email is required.')
  })

  it('rejects a malformed email', () => {
    const errors = validateLoginForm({ email: 'not-an-email', password: 'secret' })
    expect(errors.email).toBe('Enter a valid email address.')
  })

  it('requires a password', () => {
    const errors = validateLoginForm({ email: 'viewer@example.com', password: '' })
    expect(errors.password).toBe('Password is required.')
  })
})
