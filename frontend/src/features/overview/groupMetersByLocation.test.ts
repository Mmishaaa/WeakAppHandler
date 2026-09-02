import { describe, expect, it } from 'vitest'
import { groupMetersByLocation } from './groupMetersByLocation'

interface FakeMeter {
  location: string
  meterType: string
}

describe('groupMetersByLocation', () => {
  it('groups meters sharing a location together', () => {
    const meters: FakeMeter[] = [
      { location: 'Kitchen', meterType: 'energy' },
      { location: 'Garage', meterType: 'motion' },
      { location: 'Kitchen', meterType: 'air_quality' },
    ]

    const groups = groupMetersByLocation(meters)

    expect(groups).toEqual([
      { location: 'Kitchen', meters: [meters[0], meters[2]] },
      { location: 'Garage', meters: [meters[1]] },
    ])
  })

  it('returns an empty list for no meters', () => {
    expect(groupMetersByLocation([])).toEqual([])
  })

  it('preserves the order locations first appear in', () => {
    const meters: FakeMeter[] = [
      { location: 'B', meterType: 'x' },
      { location: 'A', meterType: 'y' },
    ]

    expect(groupMetersByLocation(meters).map((g) => g.location)).toEqual(['B', 'A'])
  })
})
