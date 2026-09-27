import { expect, test } from '@playwright/test'
import { mockBackend } from './backend.ts'

test('shows the metric tiles and highlights both kinds of breach', async ({ page }) => {
  await mockBackend(page)
  await page.goto('/')

  const co2 = page.locator('article.tile', { hasText: 'co2' })
  const humidity = page.locator('article.tile', { hasText: 'humidity' })

  await expect(co2).toHaveClass(/alarm/)
  await expect(co2).toContainText('above 1000')
  await expect(co2).toContainText('Office +1')
  await expect(humidity).toHaveClass(/warn/)
  await expect(humidity).toContainText('below 30')
})

test('lists the latest readings with the total count', async ({ page }) => {
  await mockBackend(page)
  await page.goto('/')

  await expect(page.getByText('1 match the filter')).toBeVisible()
  await expect(page.getByRole('cell', { name: 'Office' })).toBeVisible()
})

test('reports the live feed as offline when the hub is unreachable', async ({ page }) => {
  await mockBackend(page)
  await page.goto('/')

  await expect(page.locator('.live')).toHaveText('offline')
  await expect(page.getByText('Not connected')).toBeVisible()
})

test('publishes a reading through the REST API', async ({ page }) => {
  const backend = await mockBackend(page)
  await page.goto('/')

  const form = page.locator('form.form-grid')

  await form.getByLabel('Location').fill('Kitchen')
  await form.getByLabel('Meter type').fill('air_quality')
  await form.getByLabel('Metric').fill('co2')
  await form.getByLabel('Value', { exact: true }).fill('812')
  await form.getByRole('button', { name: 'Publish' }).click()

  await expect(page.getByText('Accepted as batch 0190f3a2.')).toBeVisible()
  expect(backend.submitted).toEqual([
    { readings: [{ location: 'Kitchen', meterType: 'air_quality', metricCode: 'co2', numeric: 812 }] },
  ])
})
