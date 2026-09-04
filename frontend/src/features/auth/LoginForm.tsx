import { useState, type FormEvent } from 'react'
import {
  emptyLoginFormValues,
  isLoginFormValid,
  validateLoginForm,
  type LoginFormErrors,
  type LoginFormValues,
} from './loginValidation'
import './login-form.css'

export interface LoginFormProps {
  onSubmit: (values: LoginFormValues) => void
  submitting: boolean
  serverError?: string
}

function FieldError({ id, message }: { id: string; message?: string }) {
  if (!message) {
    return null
  }
  return (
    <p id={id} role="alert" className="login-form__error">
      {message}
    </p>
  )
}

function errorId(field: keyof LoginFormErrors): string {
  return `login-form-error-${field}`
}

function describedBy(errors: LoginFormErrors, field: keyof LoginFormErrors): string | undefined {
  return errors[field] ? errorId(field) : undefined
}

/**
 * Login form (TASK-041) - inline validation mirrors AlertRuleForm.tsx's pattern: errors are
 * computed on every render but only shown once the user has attempted a submit, so the form
 * doesn't open with both fields already flagged invalid.
 */
export function LoginForm({ onSubmit, submitting, serverError }: LoginFormProps) {
  const [values, setValues] = useState<LoginFormValues>(emptyLoginFormValues())
  const [submitted, setSubmitted] = useState(false)

  const errors = validateLoginForm(values)
  const shownErrors = submitted ? errors : {}

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    if (!isLoginFormValid(errors)) {
      return
    }
    onSubmit(values)
  }

  return (
    <form className="login-form" onSubmit={handleSubmit} noValidate>
      <div className="login-form__field">
        <label htmlFor="login-email">Email</label>
        <input
          id="login-email"
          type="email"
          autoComplete="username"
          value={values.email}
          onChange={(event) => setValues((current) => ({ ...current, email: event.target.value }))}
          aria-invalid={shownErrors.email ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'email')}
        />
        <FieldError id={errorId('email')} message={shownErrors.email} />
      </div>

      <div className="login-form__field">
        <label htmlFor="login-password">Password</label>
        <input
          id="login-password"
          type="password"
          autoComplete="current-password"
          value={values.password}
          onChange={(event) => setValues((current) => ({ ...current, password: event.target.value }))}
          aria-invalid={shownErrors.password ? true : undefined}
          aria-describedby={describedBy(shownErrors, 'password')}
        />
        <FieldError id={errorId('password')} message={shownErrors.password} />
      </div>

      {serverError && (
        <p role="alert" className="login-form__error">
          {serverError}
        </p>
      )}

      <div className="login-form__actions">
        <button type="submit" disabled={submitting}>
          {submitting ? 'Signing in…' : 'Log in'}
        </button>
      </div>
    </form>
  )
}
