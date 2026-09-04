export interface LoginFormValues {
  email: string
  password: string
}

export interface LoginFormErrors {
  email?: string
  password?: string
}

export function emptyLoginFormValues(): LoginFormValues {
  return { email: '', password: '' }
}

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/** Client-side sanity check only - the Auth Service is the source of truth for whether a
 * credential pair is actually valid, so this never claims more than "well-formed". */
export function validateLoginForm(values: LoginFormValues): LoginFormErrors {
  const errors: LoginFormErrors = {}

  if (values.email.trim().length === 0) {
    errors.email = 'Email is required.'
  } else if (!EMAIL_PATTERN.test(values.email.trim())) {
    errors.email = 'Enter a valid email address.'
  }

  if (values.password.length === 0) {
    errors.password = 'Password is required.'
  }

  return errors
}

export function isLoginFormValid(errors: LoginFormErrors): boolean {
  return Object.keys(errors).length === 0
}
