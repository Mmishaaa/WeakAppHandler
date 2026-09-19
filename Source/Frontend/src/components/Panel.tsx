import type { ReactNode } from 'react'

interface PanelProps {
  title: string
  sub?: ReactNode
  children: ReactNode
}

export const Panel = ({ title, sub, children }: PanelProps) => (
  <section className="panel">
    <div className="panel-head">
      <h3>{title}</h3>
      {sub === undefined ? null : <span className="sub">{sub}</span>}
    </div>
    {children}
  </section>
)
