import type { WindowKey } from '../lib/windows'

export interface DashboardFilters {
  location: string | null
  metricCode: string | null
  windowKey: WindowKey
}

export const initialFilters: DashboardFilters = {
  location: null,
  metricCode: null,
  windowKey: '24h',
}
