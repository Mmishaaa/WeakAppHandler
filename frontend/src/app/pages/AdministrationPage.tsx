import { AlertRulesAdminContent } from '../../features/administration/alert-rules/AlertRulesAdminContent'
import { useAlertRulesAdmin } from '../../features/administration/alert-rules/useAlertRulesAdmin'
import { IngestionPanel } from '../../features/administration/ingestion/IngestionPanel'
import { useIngestionAdmin } from '../../features/administration/ingestion/useIngestionAdmin'
import { useNow } from '../../shared/time/useNow'
import { AsyncBoundary } from '../../shared/ui/async-boundary/AsyncBoundary'
import { Skeleton } from '../../shared/ui/skeleton/Skeleton'

function IngestionSection() {
  const {
    status,
    loading,
    error,
    refetch,
    trigger,
    triggering,
    lastTriggerResult,
    triggerError,
    changeInterval,
    updatingInterval,
    intervalError,
  } = useIngestionAdmin()
  const now = useNow()

  return (
    <AsyncBoundary
      loading={loading}
      error={error}
      data={status}
      onRetry={() => void refetch()}
      skeleton={<Skeleton lines={4} label="Loading ingestion status" />}
    >
      {(statusData) => (
        <IngestionPanel
          status={statusData}
          now={now}
          onTrigger={() => void trigger()}
          triggering={triggering}
          lastTriggerResult={lastTriggerResult}
          triggerError={triggerError}
          onIntervalChange={(seconds) => void changeInterval(seconds)}
          updatingInterval={updatingInterval}
          intervalError={intervalError}
        />
      )}
    </AsyncBoundary>
  )
}

function AlertRulesSection() {
  const { data, loading, error, refetch, create, update, remove, saving, saveError } = useAlertRulesAdmin()

  return (
    <AsyncBoundary
      loading={loading}
      error={error}
      data={data}
      onRetry={() => void refetch()}
      skeleton={<Skeleton lines={6} label="Loading alert rules" />}
    >
      {(resolved) => (
        <AlertRulesAdminContent
          rules={resolved.alertRules}
          onCreate={create}
          onUpdate={update}
          onDelete={remove}
          saving={saving}
          saveError={saveError}
        />
      )}
    </AsyncBoundary>
  )
}

export function AdministrationPage() {
  return (
    <>
      <h1>Administration</h1>
      <section aria-labelledby="ingestion-panel-heading">
        <h2 id="ingestion-panel-heading">Ingestion</h2>
        <IngestionSection />
      </section>
      <section aria-labelledby="alert-rules-heading">
        <h2 id="alert-rules-heading">Alert Rules</h2>
        <AlertRulesSection />
      </section>
    </>
  )
}
