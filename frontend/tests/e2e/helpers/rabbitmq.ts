import { env } from './env'

/** Real queue depth via RabbitMQ's management HTTP API (S3: back-pressure must be visible in RabbitMQ). */
export async function getQueueMessageCount(queueName: string): Promise<number> {
  const url =
    `${env.rabbitMqManagementUrl}/api/queues/${encodeURIComponent(env.rabbitMqVirtualHost)}/${encodeURIComponent(queueName)}`
  const response = await fetch(url, {
    headers: {
      Authorization: `Basic ${Buffer.from(`${env.rabbitMqAdminUser}:${env.rabbitMqAdminPassword}`).toString('base64')}`,
    },
  })

  if (!response.ok) {
    throw new Error(`RabbitMQ management API returned ${response.status} for queue "${queueName}"`)
  }

  const body = (await response.json()) as { messages?: number }
  return body.messages ?? 0
}
