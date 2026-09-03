import { getAccessToken } from '../auth/accessTokenStore'

/**
 * Thrown for any non-2xx REST response. `fieldErrors` carries the ASP.NET Core
 * `ValidationProblemDetails.errors` map (PascalCase property names, e.g. "ThresholdNumeric") when
 * the server responded with one, so a form can attach a server-side rejection to the same field
 * its own inline validation would have flagged.
 */
export class RestError extends Error {
  readonly status: number
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>

  constructor(status: number, message: string, fieldErrors: Readonly<Record<string, readonly string[]>> = {}) {
    super(message)
    this.name = 'RestError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

async function toRestError(response: Response): Promise<RestError> {
  let fieldErrors: Record<string, readonly string[]> = {}
  let message = `Request failed with status ${response.status}`

  try {
    const body: unknown = await response.json()
    if (body && typeof body === 'object') {
      const problem = body as { title?: string; errors?: Record<string, readonly string[]> }
      message = problem.title ?? message
      fieldErrors = problem.errors ?? {}
    }
  } catch {
    // Response body wasn't JSON (or was empty) - fall back to the generic message above.
  }

  return new RestError(response.status, message, fieldErrors)
}

/**
 * Attaches the bearer token the same way apolloClient.ts's authLink does - read fresh on every
 * call so a token set after this module loads (the normal case once TASK-041 adds login) is
 * picked up immediately - and throws {@link RestError} on any non-2xx response rather than
 * returning it, so callers can `await` happy-path JSON without checking `response.ok` themselves.
 */
export async function restFetch<T>(url: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  headers.set('Accept', 'application/json')
  if (init?.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  const token = getAccessToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(url, { ...init, headers })

  if (!response.ok) {
    throw await toRestError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
