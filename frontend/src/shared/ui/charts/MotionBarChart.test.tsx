import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MotionBarChart } from './MotionBarChart'
import type { MotionChartBucket } from './MotionBarChart'

function bucket(hour: number, count: number): MotionChartBucket {
  return { bucketStart: `2026-09-03T${String(hour).padStart(2, '0')}:00:00Z`, count }
}

describe('MotionBarChart', () => {
  it('shows an empty message when there are no buckets', () => {
    render(<MotionBarChart buckets={[]} />)
    expect(screen.getByText(/no data for this period/i)).toBeInTheDocument()
  })

  it('renders exactly one bar per bucket', () => {
    const buckets = [bucket(0, 3), bucket(1, 0), bucket(2, 7)]
    const { container } = render(<MotionBarChart buckets={buckets} />)

    expect(container.querySelectorAll('.motion-bar-chart__bar')).toHaveLength(3)
  })

  it('gives the bucket with the highest count the tallest bar', () => {
    const buckets = [bucket(0, 1), bucket(1, 10), bucket(2, 2)]
    const { container } = render(<MotionBarChart buckets={buckets} />)

    const bars = Array.from(container.querySelectorAll('.motion-bar-chart__bar'))
    const heights = bars.map((bar) => {
      // Every coordinate pair in the path's "d" attribute is an (x, y) token in order, so the
      // odd-indexed numbers are all y-coordinates; their spread is the bar's rendered height.
      const d = bar.getAttribute('d') ?? ''
      const numbers = Array.from(d.matchAll(/-?[\d.]+/g)).map((match) => Number(match[0]))
      const ys = numbers.filter((_, i) => i % 2 === 1)
      return Math.max(...ys) - Math.min(...ys)
    })

    expect(heights[1]).toBeGreaterThan(heights[0])
    expect(heights[1]).toBeGreaterThan(heights[2])
  })

  it('renders a flat (zero-height) bar for a zero-count bucket without throwing', () => {
    const buckets = [bucket(0, 0), bucket(1, 5)]
    const { container } = render(<MotionBarChart buckets={buckets} />)

    expect(container.querySelectorAll('.motion-bar-chart__bar')).toHaveLength(2)
  })

  it('never renders more x-axis tick labels than maxTicks, even with many buckets', () => {
    const buckets = Array.from({ length: 24 }, (_, hour) => bucket(hour, hour))
    const { container } = render(<MotionBarChart buckets={buckets} maxTicks={6} />)

    const axisLabels = container.querySelectorAll('.motion-bar-chart__axis-label')
    const xAxisLabelCount = axisLabels.length - 4
    expect(xAxisLabelCount).toBeLessThanOrEqual(6)
  })

  it('labels the chart with an accessible summary naming the total event count', () => {
    const buckets = [bucket(0, 3), bucket(1, 4)]
    render(<MotionBarChart buckets={buckets} />)

    const chart = screen.getByRole('img')
    expect(chart.getAttribute('aria-label')).toMatch(/motion/i)
    expect(chart.getAttribute('aria-label')).toMatch(/7/)
  })
})
