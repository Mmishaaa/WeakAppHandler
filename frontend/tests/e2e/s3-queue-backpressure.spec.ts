import { expect, test } from '@playwright/test'
import { startService, stopService } from './helpers/docker'
import { getQueueMessageCount } from './helpers/rabbitmq'

/**
 * S3 - Queue back-pressure (PRD §11): stop the Processor. Ingestion continues and messages
 * accumulate visibly in RabbitMQ; restarting the Processor drains the backlog. `readings.ingested`
 * is the queue that actually carries meter data (the one whose loss would matter), so that is what
 * this asserts drains back down - not just "some queue somewhere had messages".
 */
test.describe.serial('S3 - Processor stopped, ingestion queues back up, then drains', () => {
  test.afterAll(() => {
    startService('processor')
  })

  test('readings.ingested accumulates while the Processor is down and drains once it restarts', async () => {
    const before = await getQueueMessageCount('readings.ingested')

    stopService('processor')

    // Several ingestion polls (10s default interval) while nothing consumes them.
    await new Promise((resolve) => setTimeout(resolve, 35_000))
    const duringOutage = await getQueueMessageCount('readings.ingested')
    expect(duringOutage).toBeGreaterThan(before)

    startService('processor')

    // Give the restarted Processor time to drain the accumulated backlog.
    await expect
      .poll(() => getQueueMessageCount('readings.ingested'), { timeout: 30_000, intervals: [2_000] })
      .toBe(0)
  })
})
