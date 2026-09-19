import type { MetricState } from '../gql/graphql'

const classNames: Record<MetricState, string> = {
  OK: 'pill ok',
  ABOVE: 'pill crit',
  BELOW: 'pill warn',
  UNKNOWN: 'pill idle',
}

const labels: Record<MetricState, string> = {
  OK: 'in band',
  ABOVE: 'above',
  BELOW: 'below',
  UNKNOWN: 'no band',
}

interface StatePillProps {
  state: MetricState
  threshold: number | null
}

export const StatePill = ({ state, threshold }: StatePillProps) => {
  const suffix =
    (state === 'ABOVE' || state === 'BELOW') && threshold !== null ? ` ${threshold}` : ''

  return <span className={classNames[state]}>{`${labels[state]}${suffix}`}</span>
}
