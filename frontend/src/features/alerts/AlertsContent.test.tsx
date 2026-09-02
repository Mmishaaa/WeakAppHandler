import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AlertsContent, type AlertsContentData } from './AlertsContent'
import { DEFAULT_ALERT_FILTERS } from './alertFilters'

const NOW = new Date('2026-09-03T12:00:00.000Z')

function buildData(): AlertsContentData {
  return {
    alerts: [
      {
        id: 'alert-1',
        location: 'Garage',
        meterType: 'air_quality',
        metricCode: 'co2',
        status: 'ACTIVE',
        severity: 'CRITICAL',
        triggeredAt: '2026-09-03T11:55:00.000Z',
        triggeredValueNumeric: 1500,
        triggeredValueBool: null,
        resolvedAt: null,
        resolvedValueNumeric: null,
        resolvedValueBool: null,
      },
      {
        id: 'alert-2',
        location: 'Kitchen',
        meterType: 'air_quality',
        metricCode: 'humidity',
        status: 'RESOLVED',
        severity: 'INFO',
        triggeredAt: '2026-09-03T11:00:00.000Z',
        triggeredValueNumeric: 75,
        triggeredValueBool: null,
        resolvedAt: '2026-09-03T11:05:32.000Z',
        resolvedValueNumeric: 68,
        resolvedValueBool: null,
      },
    ],
  }
}

describe('AlertsContent', () => {
  it('renders every alert in the given (server-sorted) order when filters are ALL', () => {
    render(
      <AlertsContent
        data={buildData()}
        now={NOW}
        filters={DEFAULT_ALERT_FILTERS}
        onFiltersChange={vi.fn()}
        highlightedIds={new Set()}
      />,
    )

    const titles = screen.getAllByText(/·/, { selector: 'p.alert-row__title' }).map((el) => el.textContent)
    expect(titles[0]).toContain('Garage')
    expect(titles[1]).toContain('Kitchen')
  })

  it('shows the resolved duration between triggered and resolved timestamps', () => {
    render(
      <AlertsContent
        data={buildData()}
        now={NOW}
        filters={DEFAULT_ALERT_FILTERS}
        onFiltersChange={vi.fn()}
        highlightedIds={new Set()}
      />,
    )

    expect(screen.getByText(/Resolved · lasted 5m 32s/)).toBeInTheDocument()
  })

  it('narrows the list via the status filter', () => {
    render(
      <AlertsContent
        data={buildData()}
        now={NOW}
        filters={{ status: 'RESOLVED', severity: 'ALL' }}
        onFiltersChange={vi.fn()}
        highlightedIds={new Set()}
      />,
    )

    expect(screen.getByText(/Kitchen/)).toBeInTheDocument()
    expect(screen.queryByText(/Garage/)).not.toBeInTheDocument()
  })

  it('narrows the list via the severity filter', () => {
    render(
      <AlertsContent
        data={buildData()}
        now={NOW}
        filters={{ status: 'ALL', severity: 'CRITICAL' }}
        onFiltersChange={vi.fn()}
        highlightedIds={new Set()}
      />,
    )

    expect(screen.getByText(/Garage/)).toBeInTheDocument()
    expect(screen.queryByText(/Kitchen/)).not.toBeInTheDocument()
  })

  it('shows a message instead of an empty list when no alert matches the filters', () => {
    render(
      <AlertsContent
        data={buildData()}
        now={NOW}
        filters={{ status: 'RESOLVED', severity: 'CRITICAL' }}
        onFiltersChange={vi.fn()}
        highlightedIds={new Set()}
      />,
    )

    expect(screen.getByText('No alerts match the current filters.')).toBeInTheDocument()
  })

  it('marks a highlighted alert with data-new for the raised/resolved call-out animation', () => {
    render(
      <AlertsContent
        data={buildData()}
        now={NOW}
        filters={DEFAULT_ALERT_FILTERS}
        onFiltersChange={vi.fn()}
        highlightedIds={new Set(['alert-1'])}
      />,
    )

    const garageRow = screen.getByText(/Garage/).closest('li') as HTMLElement
    expect(garageRow).toHaveAttribute('data-new')
    const kitchenRow = screen.getByText(/Kitchen/).closest('li') as HTMLElement
    expect(kitchenRow).not.toHaveAttribute('data-new')
  })

  it('calls onFiltersChange when the status select changes', async () => {
    const user = userEvent.setup()
    const onFiltersChange = vi.fn()
    render(
      <AlertsContent
        data={buildData()}
        now={NOW}
        filters={DEFAULT_ALERT_FILTERS}
        onFiltersChange={onFiltersChange}
        highlightedIds={new Set()}
      />,
    )

    await user.selectOptions(screen.getByLabelText('Status'), 'ACTIVE')
    expect(onFiltersChange).toHaveBeenCalledWith({ status: 'ACTIVE', severity: 'ALL' })
  })
})
