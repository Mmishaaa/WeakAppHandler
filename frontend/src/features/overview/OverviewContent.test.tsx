import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { OverviewContent, type OverviewContentData } from './OverviewContent'

const NOW = new Date('2026-09-03T12:00:00.000Z')

function buildData(overrides: Partial<OverviewContentData> = {}): OverviewContentData {
  return {
    meters: [
      {
        id: 'meter-1',
        location: 'Kitchen',
        meterType: 'energy',
        lastSeenAt: '2026-09-03T11:59:00.000Z',
        currentValues: [
          { metricCode: 'energy', valueNumeric: 4.2, valueBool: null, observedAt: '2026-09-03T11:59:00.000Z' },
        ],
      },
      {
        id: 'meter-2',
        location: 'Garage',
        meterType: 'motion',
        lastSeenAt: '2026-09-03T11:50:00.000Z',
        currentValues: [
          { metricCode: 'motion_detected', valueNumeric: null, valueBool: true, observedAt: '2026-09-03T11:50:00.000Z' },
        ],
      },
    ],
    alerts: { nodes: [] },
    ...overrides,
  }
}

describe('OverviewContent', () => {
  it('shows the header stats derived from meters and active alerts', () => {
    render(<OverviewContent data={buildData()} now={NOW} />)

    expect(screen.getByText('Meters reporting').nextElementSibling).toHaveTextContent('2')
    expect(screen.getByText('Active alerts').nextElementSibling).toHaveTextContent('0')
    expect(screen.getByText('Last successful poll').nextElementSibling).toHaveTextContent(/minute/i)
  })

  it('groups meter tiles under their location heading', () => {
    render(<OverviewContent data={buildData()} now={NOW} />)

    const kitchen = screen.getByRole('heading', { name: 'Kitchen' })
    const kitchenSection = kitchen.closest('section') as HTMLElement
    expect(within(kitchenSection).getByRole('heading', { name: 'energy', level: 3 })).toBeInTheDocument()

    const garage = screen.getByRole('heading', { name: 'Garage' })
    const garageSection = garage.closest('section') as HTMLElement
    expect(within(garageSection).getByRole('heading', { name: 'motion', level: 3 })).toBeInTheDocument()
  })

  it('renders the formatted metric value and unit on each tile', () => {
    render(<OverviewContent data={buildData()} now={NOW} />)

    expect(screen.getByText('4.2 kWh')).toBeInTheDocument()
    expect(screen.getByText('Yes')).toBeInTheDocument()
  })

  it('colors a meter tile with the worst active alert severity for that meter', () => {
    render(
      <OverviewContent
        data={buildData({ alerts: { nodes: [{ meterId: 'meter-2', severity: 'CRITICAL' }] } })}
        now={NOW}
      />,
    )

    const garageTile = screen.getByRole('heading', { name: 'motion', level: 3 }).closest('li') as HTMLElement
    expect(garageTile).toHaveAttribute('data-severity', 'critical')
    expect(within(garageTile).getByText('Critical')).toBeInTheDocument()

    const kitchenTile = screen.getByRole('heading', { name: 'energy', level: 3 }).closest('li') as HTMLElement
    expect(kitchenTile).toHaveAttribute('data-severity', 'normal')
  })

  it('reflects the active alert count from the header, matching what the Alerts screen would show', () => {
    render(
      <OverviewContent
        data={buildData({
          alerts: {
            nodes: [
              { meterId: 'meter-1', severity: 'WARNING' },
              { meterId: 'meter-2', severity: 'CRITICAL' },
            ],
          },
        })}
        now={NOW}
      />,
    )

    expect(screen.getByText('Active alerts').nextElementSibling).toHaveTextContent('2')
  })
})
