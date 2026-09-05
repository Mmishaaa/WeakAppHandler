import { expect, test } from '@playwright/test'
import { loginAsViewer } from './helpers/login'

/**
 * S1 - Cold start (PRD §11): `docker compose up` on a clean machine, and the dashboard populates on
 * its own. This spec assumes the stack is already up (per the suite's own README instructions) and
 * asserts the actual outcome that matters: real ingested data renders with no manual intervention
 * beyond logging in.
 */
test('the dashboard populates with real meter data with no manual intervention', async ({ page }) => {
  await loginAsViewer(page)

  await expect(page.getByText('Meters reporting')).toBeVisible()

  const meterCount = page.locator('dt', { hasText: 'Meters reporting' }).locator('xpath=following-sibling::dd[1]')
  await expect(meterCount).not.toHaveText('0')

  // At least one location section with at least one meter tile actually rendered - not just a
  // non-zero count in the header.
  await expect(page.locator('.location-section').first()).toBeVisible()
  await expect(page.locator('.location-section__tiles li').first()).toBeVisible()
})
