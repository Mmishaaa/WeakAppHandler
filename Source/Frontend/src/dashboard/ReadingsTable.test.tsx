import { MockedProvider } from '@apollo/client/testing/react'
import type { MockLink } from '@apollo/client/testing'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { ReadingsDocument } from '../gql/graphql'
import type { ReadingFilterModelInput, ReadingsQuery } from '../gql/graphql'
import { ReadingsTable } from './ReadingsTable'

const filter: ReadingFilterModelInput = {
  location: 'Kitchen',
  metricCode: 'co2',
  from: '2026-09-26T13:00:00.000Z',
  to: '2026-09-27T13:00:00.000Z',
}

type Connection = NonNullable<ReadingsQuery['readings']>

const page = (
  ids: number[],
  pageInfo: Partial<Connection['pageInfo']>,
): Connection => ({
  totalCount: 4,
  pageInfo: {
    hasNextPage: false,
    hasPreviousPage: false,
    startCursor: null,
    endCursor: null,
    ...pageInfo,
  },
  nodes: ids.map((id) => ({
    id,
    meterId: 'meter',
    location: 'Kitchen',
    meterType: 'air_quality',
    metricCode: 'co2',
    observedAt: '2026-09-27T12:00:00Z',
    numeric: id * 100,
    flag: null,
  })),
})

const mock = (
  variables: Record<string, unknown>,
  readings: Connection,
): MockLink.MockedResponse => ({
  request: { query: ReadingsDocument, variables },
  result: { data: { readings } },
})

describe('ReadingsTable', () => {
  it('follows the end cursor to the next page and the start cursor back', async () => {
    const mocks = [
      mock(
        { filter, first: 25, after: null },
        page([1, 2], { hasNextPage: true, startCursor: 'c1', endCursor: 'c2' }),
      ),
      mock(
        { filter, first: 25, after: 'c2' },
        page([3, 4], { hasPreviousPage: true, startCursor: 'c3', endCursor: 'c4' }),
      ),
      mock(
        { filter, last: 25, before: 'c3' },
        page([1, 2], { hasNextPage: true, startCursor: 'c1', endCursor: 'c2' }),
      ),
    ]

    render(
      <MockedProvider mocks={mocks}>
        <ReadingsTable filter={filter} scope="Kitchen|co2|24h" dataVersion={0} />
      </MockedProvider>,
    )

    expect(await screen.findByText('100')).toBeInTheDocument()
    expect(screen.getByText('4 match the filter')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()

    await userEvent.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('300')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()

    await userEvent.click(screen.getByRole('button', { name: 'Previous' }))

    expect(await screen.findByText('100')).toBeInTheDocument()
  })

  it('says so when nothing matches the filter', async () => {
    render(
      <MockedProvider mocks={[mock({ filter, first: 25, after: null }, page([], {}))]}>
        <ReadingsTable filter={filter} scope="Kitchen|co2|24h" dataVersion={0} />
      </MockedProvider>,
    )

    expect(await screen.findByText('No readings')).toBeInTheDocument()
  })
})
