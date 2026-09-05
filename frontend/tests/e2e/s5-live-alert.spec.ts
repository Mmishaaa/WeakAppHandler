import { expect, test } from '@playwright/test'
import { loginAsViewer } from './helpers/login'

/**
 * S5 - Live alert (PRD §11): a threshold crossing appears in the browser through SignalR without a
 * refresh, then resolves when the value returns to normal. Reliably *provoking* a specific
 * threshold crossing from here would mean reaching into WeakApp's own data generator, which is out
 * of this suite's scope - so this automates the part that is reliably testable (the realtime
 * channel the alert would actually travel over is live, not just present in the DOM) and documents
 * the full walkthrough as the manual step: watch the Alerts page while an existing alert rule's
 * threshold is crossed by the live data (or lower a rule's threshold via Administration so the next
 * poll crosses it), with no page refresh, and confirm it disappears once the value returns to
 * normal - see README's demo scenarios section.
 */
test('the realtime connection used for live alerts is up', async ({ page }) => {
  await loginAsViewer(page)

  const badge = page.getByRole('status').filter({ hasText: /Live|Connecting|Reconnecting/ })
  await expect(badge).toBeVisible()
  await expect(badge).toHaveText('Live', { timeout: 20_000 })
})
