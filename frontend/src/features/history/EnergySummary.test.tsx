import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { EnergySummary } from './EnergySummary'

describe('EnergySummary', () => {
  it('shows the total across all buckets and the count-weighted average', () => {
    const buckets = [
      { sum: 10, count: 2 },
      { sum: 20, count: 2 },
    ]

    render(<EnergySummary metricCode="energy" buckets={buckets} />)

    expect(screen.getByText('30 kWh')).toBeInTheDocument()
    // total 30 across 4 readings => average 7.5
    expect(screen.getByText('7.5 kWh')).toBeInTheDocument()
  })

  it('shows a placeholder average when there are no readings at all', () => {
    render(<EnergySummary metricCode="energy" buckets={[{ sum: null, count: 0 }]} />)

    expect(screen.getByText('0 kWh')).toBeInTheDocument()
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('treats a missing sum as zero rather than throwing', () => {
    const buckets = [{ sum: undefined, count: 1 }, { sum: 10, count: 1 }]

    render(<EnergySummary metricCode="energy" buckets={buckets} />)

    expect(screen.getByText('10 kWh')).toBeInTheDocument()
  })
})
