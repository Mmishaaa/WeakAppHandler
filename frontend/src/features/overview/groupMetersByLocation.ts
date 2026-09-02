export interface LocationGroup<T> {
  location: string
  meters: T[]
}

/** Groups meters by location, preserving each location's first-seen order from the input list. */
export function groupMetersByLocation<T extends { location: string }>(meters: readonly T[]): LocationGroup<T>[] {
  const order: string[] = []
  const groups = new Map<string, T[]>()

  for (const meter of meters) {
    let group = groups.get(meter.location)
    if (!group) {
      group = []
      groups.set(meter.location, group)
      order.push(meter.location)
    }
    group.push(meter)
  }

  return order.map((location) => ({ location, meters: groups.get(location) as T[] }))
}
