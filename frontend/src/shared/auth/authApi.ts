import { runtimeConfig } from '../config/runtimeConfig'
import type { AuthRole } from './accessTokenStore'

/** Thrown for any non-2xx response from `/login` or `/refresh`. */
export class AuthApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'AuthApiError'
    this.status = status
  }
}

export interface AuthTokenResult {
  accessToken: string
  role: AuthRole
  email: string
  expiresInSeconds: number
}

/** Wire shape of AuthController's `LoginResponse` (camelCase - the default System.Text.Json naming
 * policy ASP.NET Core applies), shared by both `/login` and `/refresh`. */
interface LoginResponseBody {
  accessToken: string
  tokenType: string
  expiresInSeconds: number
  role: string
  email: string
}

function toResult(body: LoginResponseBody): AuthTokenResult {
  return {
    accessToken: body.accessToken,
    role: body.role as AuthRole,
    email: body.email,
    expiresInSeconds: body.expiresInSeconds,
  }
}

/**
 * Calls the Auth Service directly (not through the Gateway's REST proxy) with
 * `credentials: 'include'`, since `/refresh` authenticates via the httpOnly `refresh_token` cookie
 * alone (see AuthController.cs) - `shared/rest/restClient.ts`'s `restFetch` doesn't send
 * credentials and unconditionally attaches whatever bearer token is already in the store, neither
 * of which is right here (there is no token yet at login, and refresh has none to send either).
 */
async function postForSession(path: string, body?: unknown): Promise<AuthTokenResult> {
  const response = await fetch(`${runtimeConfig.authApiUrl}${path}`, {
    method: 'POST',
    credentials: 'include',
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!response.ok) {
    throw new AuthApiError(response.status, `Request to ${path} failed with status ${response.status}`)
  }

  return toResult((await response.json()) as LoginResponseBody)
}

export function login(email: string, password: string): Promise<AuthTokenResult> {
  return postForSession('/login', { email, password })
}

export function refreshAuthSession(): Promise<AuthTokenResult> {
  return postForSession('/refresh')
}
