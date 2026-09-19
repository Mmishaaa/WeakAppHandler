import { useState } from 'react'
import { useQuery } from '@apollo/client/react'
import { ReadingsDocument } from '../gql/graphql'
import type { ReadingFilterModelInput } from '../gql/graphql'
import { Panel } from '../components/Panel'
import { EmptyState, ErrorState, LoadingState } from '../components/PanelState'
import { formatStamp, formatValue } from '../lib/format'
import { useRefetchOn } from '../lib/useRefetchOn'

const pageSize = 25

interface Cursor {
  after: string | null
  before: string | null
}

const firstPage: Cursor = { after: null, before: null }

interface ReadingsTableProps {
  filter: ReadingFilterModelInput
  scope: string
  dataVersion: number
}

export const ReadingsTable = ({ filter, scope, dataVersion }: ReadingsTableProps) => {
  const [pageKey, setPageKey] = useState(scope)
  const [cursor, setCursor] = useState<Cursor>(firstPage)

  if (pageKey !== scope) {
    setPageKey(scope)
    setCursor(firstPage)
  }

  const { data, loading, error, refetch } = useQuery(ReadingsDocument, {
    variables:
      cursor.before === null
        ? { filter, first: pageSize, after: cursor.after }
        : { filter, last: pageSize, before: cursor.before },
  })

  useRefetchOn(dataVersion, () => void refetch())

  const connection = data?.readings ?? null
  const rows = connection?.nodes ?? []
  const pageInfo = connection?.pageInfo ?? null

  return (
    <Panel
      title="Recent readings"
      sub={connection === null ? undefined : `${connection.totalCount} match the filter`}
    >
      {loading && data === undefined ? (
        <LoadingState rows={6} />
      ) : error !== undefined ? (
        <ErrorState message={error.message} onRetry={() => void refetch()} />
      ) : rows.length === 0 ? (
        <EmptyState
          title="No readings"
          hint="Nothing matches the current location, metric and time window."
        />
      ) : (
        <>
          <div className="tbl-scroll">
            <table>
              <thead>
                <tr>
                  <th>Observed</th>
                  <th>Location</th>
                  <th>Meter type</th>
                  <th>Metric</th>
                  <th>Value</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.id}>
                    <td>{formatStamp(row.observedAt)}</td>
                    <td>{row.location}</td>
                    <td>{row.meterType}</td>
                    <td>{row.metricCode}</td>
                    <td>{formatValue(row.numeric, row.flag)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="pager">
            <button
              type="button"
              className="btn ghost"
              disabled={pageInfo?.hasPreviousPage !== true}
              onClick={() =>
                setCursor({ after: null, before: pageInfo?.startCursor ?? null })
              }
            >
              Previous
            </button>
            <button
              type="button"
              className="btn ghost"
              disabled={pageInfo?.hasNextPage !== true}
              onClick={() => setCursor({ after: pageInfo?.endCursor ?? null, before: null })}
            >
              Next
            </button>
            <span className="spacer">
              {pageSize} per page · newest first · keyset cursors
            </span>
          </div>
        </>
      )}
    </Panel>
  )
}
