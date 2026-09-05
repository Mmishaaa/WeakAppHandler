import { expect, test } from '@playwright/test'
import { env } from './helpers/env'
import { loginAsViewer } from './helpers/login'

/**
 * S6 - Authorisation (PRD §11): log in as viewer - the Administration screen is absent from the
 * nav, unreachable by URL, and a direct API call for an admin-only operation is refused. Confirms
 * both the client-side gate (RequireAdmin, nav-items.ts) and that it is backed by real server-side
 * enforcement, not merely a hidden link.
 */
test('a viewer cannot see, reach, or call the Administration API', async ({ page }) => {
  await loginAsViewer(page)

  await expect(page.getByRole('link', { name: 'Administration' })).toHaveCount(0)

  await page.goto('/administration')
  await page.waitForURL('**/')
  await expect(page.getByRole('link', { name: 'Administration' })).toHaveCount(0)

  const loginResponse = await page.request.post(`${env.authUrl}/login`, {
    data: { email: env.viewerEmail, password: env.viewerPassword },
  })
  expect(loginResponse.ok()).toBeTruthy()
  const { accessToken } = (await loginResponse.json()) as { accessToken: string }

  const adminApiResponse = await page.request.get(`${env.notificationUrl}/api/v1/alert-rules`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  expect([401, 403]).toContain(adminApiResponse.status())
})
