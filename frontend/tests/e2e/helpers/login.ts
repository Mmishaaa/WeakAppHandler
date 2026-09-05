import type { Page } from '@playwright/test'
import { env } from './env'

/** Logs in through the real UI form (not an API shortcut) - the login journey is itself S6's setup. */
export async function loginAs(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Log in' }).click()
  await page.waitForURL('**/')
}

export async function loginAsViewer(page: Page): Promise<void> {
  await loginAs(page, env.viewerEmail, env.viewerPassword)
}

export async function loginAsAdmin(page: Page): Promise<void> {
  await loginAs(page, env.adminEmail, env.adminPassword)
}
