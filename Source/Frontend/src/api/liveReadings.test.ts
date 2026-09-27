import { describe, expect, it } from 'vitest'
import { feedLimit, mergeFeed } from './liveReadings'
import type { LiveReading } from './liveReadings'

const reading = (id: number): LiveReading => ({
  id,
  meterId: 'meter',
  location: 'Kitchen',
  meterType: 'air_quality',
  metricCode: 'co2',
  observedAt: '2026-09-27T12:00:00Z',
  numeric: 800,
  flag: null,
})

describe('mergeFeed', () => {
  it('puts new readings on top', () => {
    const feed = mergeFeed(mergeFeed([], [reading(1)], []), [reading(2)], [])

    expect(feed.map((event) => event.reading.id)).toEqual([2, 1])
  })

  it('ignores a reading that is already in the feed', () => {
    const once = mergeFeed([], [reading(1)], [])

    expect(mergeFeed(once, [reading(1)], [])).toHaveLength(1)
  })

  it('attaches an alert to the reading it was raised for', () => {
    const feed = mergeFeed(
      mergeFeed([], [reading(1), reading(2)], []),
      [],
      [{ reading: reading(1), kind: 'Above', threshold: 700 }],
    )

    expect(feed.find((event) => event.reading.id === 1)?.alert?.kind).toBe('Above')
    expect(feed.find((event) => event.reading.id === 2)?.alert).toBeNull()
  })

  it('shows an alert whose reading never arrived as an event of its own', () => {
    const feed = mergeFeed([], [], [{ reading: reading(7), kind: 'Below', threshold: 30 }])

    expect(feed).toHaveLength(1)
    expect(feed[0].alert?.kind).toBe('Below')
  })

  it('keeps no more than the feed limit', () => {
    const readings = Array.from({ length: feedLimit + 5 }, (_, index) => reading(index))

    expect(mergeFeed([], readings, [])).toHaveLength(feedLimit)
  })
})
