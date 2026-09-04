import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { LoginForm } from '../../features/auth/LoginForm'
import type { LoginFormValues } from '../../features/auth/loginValidation'
import { AuthApiError } from '../../shared/auth/authApi'
import { login } from '../../shared/auth/authSessionManager'

export function LoginPage() {
  const navigate = useNavigate()
  const [submitting, setSubmitting] = useState(false)
  const [serverError, setServerError] = useState<string>()

  async function handleSubmit(values: LoginFormValues) {
    setSubmitting(true)
    setServerError(undefined)
    try {
      await login(values.email, values.password)
      navigate('/', { replace: true })
    } catch (error) {
      setServerError(
        error instanceof AuthApiError && error.status === 401
          ? 'Invalid email or password.'
          : 'Login failed. Please try again.',
      )
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <h1>Log in</h1>
      <LoginForm onSubmit={(values) => void handleSubmit(values)} submitting={submitting} serverError={serverError} />
    </>
  )
}
