import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { RangeLineChart } from './RangeLineChart'
import type { RangeChartBucket } from './RangeLineChart'

function bucket(hour: number, avg: number | null, min: number | null, max: number | null): RangeChartBucket {
  return { bucketStart: `2026-09-03T${String(hour).padStart(2, '0')}:00:00Z`, avg, min, max }
}

describe('RangeLineChart', () => {
  it('shows an empty message when there are no buckets', () => {
    render(<RangeLineChart metricCode="co2" buckets={[]} />)
    expect(screen.getByText(/no data for this period/i)).toBeInTheDocument()
  })

  it('shows an empty message when every bucket has no reading (all-null avg)', () => {
    render(<RangeLineChart metricCode="co2" buckets={[bucket(0, null, null, null), bucket(1, null, null, null)]} />)
    expect(screen.getByText(/no data for this period/i)).toBeInTheDocument()
  })

  it('draws exactly one min/max band and one average line for a fully contiguous series', () => {
    const buckets = [bucket(0, 500, 400, 600), bucket(1, 520, 410, 610), bucket(2, 540, 420, 630)]
    const { container } = render(<RangeLineChart metricCode="co2" buckets={buckets} />)

    expect(container.querySelectorAll('.range-line-chart__band')).toHaveLength(1)
    expect(container.querySelectorAll('.range-line-chart__line')).toHaveLength(1)
  })

  it('splits the band/line into separate segments across a gap where a bucket has no reading', () => {
    const buckets = [bucket(0, 500, 400, 600), bucket(1, null, null, null), bucket(2, 540, 420, 630)]
    const { container } = render(<RangeLineChart metricCode="co2" buckets={buckets} />)

    expect(container.querySelectorAll('.range-line-chart__band')).toHaveLength(2)
    expect(container.querySelectorAll('.range-line-chart__line')).toHaveLength(2)
  })

  it('uses the min/max band even when min/max are missing but avg is present (falls back to avg)', () => {
    const buckets = [bucket(0, 500, null, null)]
    const { container } = render(<RangeLineChart metricCode="co2" buckets={buckets} />)

    expect(container.querySelector('.range-line-chart__band')).not.toBeNull()
  })

  it('never renders more x-axis tick labels than maxTicks, even with many buckets', () => {
    const buckets = Array.from({ length: 24 }, (_, hour) => bucket(hour, 500 + hour, 400 + hour, 600 + hour))
    const { container } = render(<RangeLineChart metricCode="co2" buckets={buckets} maxTicks={6} />)

    const axisLabels = container.querySelectorAll('.range-line-chart__axis-label')
    // 4 y-axis ticks (computeNiceTicks target) + at most 6 x-axis ticks.
    const xAxisLabelCount = axisLabels.length - 4
    expect(xAxisLabelCount).toBeLessThanOrEqual(6)
  })

  it('labels the chart with an accessible summary naming the metric and its value range', () => {
    const buckets = [bucket(0, 500, 400, 600), bucket(1, 800, 700, 900)]
    render(<RangeLineChart metricCode="co2" buckets={buckets} />)

    const chart = screen.getByRole('img')
    expect(chart.getAttribute('aria-label')).toMatch(/CO2/i)
    expect(chart.getAttribute('aria-label')).toMatch(/500/)
    expect(chart.getAttribute('aria-label')).toMatch(/800/)
  })
})
