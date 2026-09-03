import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { HistoryContent } from './HistoryContent'

const buckets = [
  { bucketStart: '2026-09-03T00:00:00Z', avg: 10, min: 5, max: 15, sum: 20, count: 2 },
  { bucketStart: '2026-09-03T01:00:00Z', avg: 12, min: 6, max: 18, sum: 24, count: 2 },
]

describe('HistoryContent', () => {
  it('renders sum and average for energy alongside the range chart', () => {
    render(<HistoryContent data={{ aggregations: buckets }} metricCode="energy" period="DAY" />)

    expect(screen.getByText('Total')).toBeInTheDocument()
    expect(screen.getByText('Average')).toBeInTheDocument()
    expect(screen.getByRole('img')).toBeInTheDocument()
  })

  it('renders only the range chart (no sum/average) for co2', () => {
    render(<HistoryContent data={{ aggregations: buckets }} metricCode="co2" period="DAY" />)

    expect(screen.queryByText('Total')).not.toBeInTheDocument()
    expect(screen.getByRole('img')).toBeInTheDocument()
  })

  it('renders a motion bar chart for motion_detected', () => {
    render(<HistoryContent data={{ aggregations: buckets }} metricCode="motion_detected" period="DAY" />)

    const chart = screen.getByRole('img')
    expect(chart.getAttribute('aria-label')).toMatch(/motion/i)
  })
})
