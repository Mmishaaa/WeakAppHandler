import type { Page, Route } from '@playwright/test'

type GraphQlData = Record<string, unknown>

export interface Backend {
  submitted: unknown[]
}

const metricSnapshot = [
  {
    __typename: 'MetricSnapshotModel',
    metricCode: 'co2',
    unit: 'ppm',
    location: 'Office',
    meterType: 'air_quality',
    observedAt: '2026-09-27T12:00:00Z',
    numeric: 1250,
    flag: null,
    locationCount: 2,
    state: 'ABOVE',
    threshold: 1000,
  },
  {
    __typename: 'MetricSnapshotModel',
    metricCode: 'humidity',
    unit: '%',
    location: 'Kitchen',
    meterType: 'air_quality',
    observedAt: '2026-09-27T12:00:00Z',
    numeric: 22,
    flag: null,
    locationCount: 2,
    state: 'BELOW',
    threshold: 30,
  },
]

const responses: Record<string, GraphQlData> = {
  FilterOptions: {
    filterOptions: {
      __typename: 'FilterOptionsModel',
      locations: ['Kitchen', 'Office'],
      meterTypes: ['air_quality'],
      metricCodes: ['co2', 'humidity'],
    },
  },
  MetricSnapshot: { metricSnapshot },
  ReadingSeries: { readingSeries: [] },
  LocationStats: { locationStats: [] },
  MeterTypeStats: { meterTypeStats: [] },
  Readings: {
    readings: {
      __typename: 'ReadingsConnection',
      totalCount: 1,
      pageInfo: {
        __typename: 'PageInfo',
        hasNextPage: false,
        hasPreviousPage: false,
        startCursor: 'c1',
        endCursor: 'c1',
      },
      nodes: [
        {
          __typename: 'ReadingModel',
          id: 1,
          meterId: 'meter',
          location: 'Office',
          meterType: 'air_quality',
          metricCode: 'co2',
          observedAt: '2026-09-27T12:00:00Z',
          numeric: 1250,
          flag: null,
        },
      ],
    },
  },
}

const answerGraphQl = async (route: Route) => {
  const body = route.request().postDataJSON() as { operationName?: string }
  const data = responses[body.operationName ?? '']

  await route.fulfill(
    data === undefined
      ? { status: 400, json: { errors: [{ message: `Unexpected ${body.operationName}` }] } }
      : { json: { data } },
  )
}

export const mockBackend = async (page: Page): Promise<Backend> => {
  const backend: Backend = { submitted: [] }

  await page.route('**/graphql', answerGraphQl)
  await page.route('**/hubs/readings/negotiate**', (route) => route.fulfill({ status: 503 }))
  await page.route('**/api/readings', async (route) => {
    backend.submitted.push(route.request().postDataJSON())

    await route.fulfill({
      status: 202,
      json: {
        batchId: '0190f3a2-7b1c-7d10-9e55-1b2c3d4e5f60',
        capturedAt: '2026-09-27T12:00:00Z',
        readingCount: 1,
      },
    })
  })

  return backend
}
