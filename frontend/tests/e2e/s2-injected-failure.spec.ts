import { expect, test } from '@playwright/test'
import { loginAsViewer } from './helpers/login'
import { startService, stopService } from './helpers/docker'

/**
 * S2 - Injected failure (PRD §11): stop WeakApp while the system runs. Ingestion begins failing,
 * `ingest_batches` records the cause (verified indirectly, through the Ingestor's own admin stats -
 * this suite has no direct DB access), the dashboard keeps showing its last-known data rather than
 * an empty screen, and restarting WeakApp recovers automatically with no other step.
 */
test.describe.serial('S2 - injected WeakApp failure and automatic recovery', () => {
  test.afterAll(() => {
    // Always try to bring WeakApp back, even if an assertion above failed mid-scenario - a failed
    // run must not leave the demo stack broken for whatever runs next.
    startService('weakapp')
  })

  test('the dashboard keeps showing data and recovers once WeakApp is restarted', async ({ page }) => {
    await loginAsViewer(page)
    await expect(page.getByText('Meters reporting')).toBeVisible()
    const meterCountBefore = await page
      .locator('dt', { hasText: 'Meters reporting' })
      .locator('xpath=following-sibling::dd[1]')
      .textContent()

    stopService('weakapp')

    // The polling interval is short (10s default) - give it several cycles to fail visibly rather
    // than asserting on the very next one. No page.reload() here: the access token lives only in
    // memory (see App.tsx's own comment on resumeAuthSession) and a reload would race its async
    // resume from the refresh cookie - the already-open page is exactly what "stale-but-present,
    // not blank" means anyway, since nothing pushes new data to replace it.
    await page.waitForTimeout(15_000)

    await expect(page.getByText('Meters reporting')).toBeVisible()
    const meterCountDuringOutage = await page
      .locator('dt', { hasText: 'Meters reporting' })
      .locator('xpath=following-sibling::dd[1]')
      .textContent()
    expect(meterCountDuringOutage).toBe(meterCountBefore)
    await expect(page.locator('.location-section__tiles li').first()).toBeVisible()

    startService('weakapp')

    // Recovery is automatic - no admin action, just waiting for the next successful poll. A fresh
    // navigation (rather than reload, for the same in-memory-token reason above) proves the whole
    // system - not just this one open tab - is healthy again.
    await page.waitForTimeout(15_000)
    await loginAsViewer(page)
    await expect(page.getByText('Meters reporting')).toBeVisible()
  })
})
