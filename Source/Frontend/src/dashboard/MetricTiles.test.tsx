import { MockedProvider } from '@apollo/client/testing/react'
import type { MockLink } from '@apollo/client/testing'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { MetricSnapshotDocument } from '../gql/graphql'
import type { MetricSnapshotQuery } from '../gql/graphql'
import { MetricTiles } from './MetricTiles'

type Metric = MetricSnapshotQuery['metricSnapshot'][number]

const metric = (overrides: Partial<Metric>): Metric => ({
  metricCode: 'co2',
  unit: 'ppm',
  location: 'Kitchen',
  meterType: 'air_quality',
  observedAt: '2026-09-27T12:00:00Z',
  numeric: 800,
  flag: null,
  locationCount: 1,
  state: 'OK',
  threshold: 1000,
  ...overrides,
})

const snapshot = (metrics: Metric[]): MockLink.MockedResponse => ({
  request: { query: MetricSnapshotDocument },
  result: { data: { metricSnapshot: metrics } },
})

const renderTiles = (mocks: MockLink.MockedResponse[]) =>
  render(
    <MockedProvider mocks={mocks}>
      <MetricTiles dataVersion={0} />
    </MockedProvider>,
  )

describe('MetricTiles', () => {
  it('shows one tile per metric with its value, unit and location', async () => {
    renderTiles([
      snapshot([
        metric({ metricCode: 'co2', numeric: 812, location: 'Kitchen', locationCount: 3 }),
        metric({ metricCode: 'humidity', unit: '%', numeric: 45, location: 'Office' }),
      ]),
    ])

    expect(await screen.findByText('co2')).toBeInTheDocument()
    expect(screen.getByText('humidity')).toBeInTheDocument()
    expect(screen.getByText('Kitchen +2')).toBeInTheDocument()
    expect(screen.getByText('ppm')).toBeInTheDocument()
  })

  it('highlights a tile above its band and one below it', async () => {
    renderTiles([
      snapshot([
        metric({ metricCode: 'co2', numeric: 1200, state: 'ABOVE', threshold: 1000 }),
        metric({ metricCode: 'humidity', unit: '%', numeric: 20, state: 'BELOW', threshold: 30 }),
        metric({ metricCode: 'pm25', unit: 'µg/m³', numeric: 12, state: 'OK', threshold: 35 }),
      ]),
    ])

    const above = (await screen.findByText('co2')).closest('article')
    const below = screen.getByText('humidity').closest('article')
    const inside = screen.getByText('pm25').closest('article')

    expect(above).toHaveClass('tile', 'alarm')
    expect(below).toHaveClass('tile', 'warn')
    expect(inside).toHaveClass('tile')
    expect(inside).not.toHaveClass('alarm')
    expect(inside).not.toHaveClass('warn')
    expect(screen.getByText('below 30')).toBeInTheDocument()
  })

  it('says so when there is nothing to show', async () => {
    renderTiles([snapshot([])])

    expect(await screen.findByText('No readings yet')).toBeInTheDocument()
  })

  it('shows the error and retries on request', async () => {
    renderTiles([
      { request: { query: MetricSnapshotDocument }, error: new Error('gateway is down') },
      snapshot([metric({ metricCode: 'co2' })]),
    ])

    expect(await screen.findByRole('alert')).toHaveTextContent('gateway is down')

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('co2')).toBeInTheDocument()
  })
})
