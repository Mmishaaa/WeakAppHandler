import { useId, useState } from 'react'
import { Panel } from '../components/Panel'
import { submitReadings } from '../api/readingsApi'
import type { SubmitReadingRequest } from '../api/readingsApi'

type ValueKind = 'numeric' | 'flag'

interface SubmitFormProps {
  locations: readonly string[]
  meterTypes: readonly string[]
  metricCodes: readonly string[]
  onSubmitted: () => void
}

export const SubmitForm = ({
  locations,
  meterTypes,
  metricCodes,
  onSubmitted,
}: SubmitFormProps) => {
  const listId = useId()
  const [location, setLocation] = useState('')
  const [meterType, setMeterType] = useState('')
  const [metricCode, setMetricCode] = useState('')
  const [kind, setKind] = useState<ValueKind>('numeric')
  const [numeric, setNumeric] = useState('')
  const [flag, setFlag] = useState('true')
  const [pending, setPending] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)

  const ready =
    location.trim() !== '' &&
    meterType.trim() !== '' &&
    metricCode.trim() !== '' &&
    (kind === 'flag' || (numeric.trim() !== '' && Number.isFinite(Number(numeric))))

  const submit = async () => {
    const reading: SubmitReadingRequest =
      kind === 'numeric'
        ? {
            location: location.trim(),
            meterType: meterType.trim(),
            metricCode: metricCode.trim(),
            numeric: Number(numeric),
          }
        : {
            location: location.trim(),
            meterType: meterType.trim(),
            metricCode: metricCode.trim(),
            flag: flag === 'true',
          }

    setPending(true)
    setMessage(null)
    setFailed(false)

    try {
      const response = await submitReadings([reading])

      setMessage(`Accepted as batch ${response.batchId.slice(0, 8)}.`)
      setNumeric('')
      onSubmitted()
    } catch (error) {
      setFailed(true)
      setMessage(error instanceof Error ? error.message : 'Submission failed.')
    } finally {
      setPending(false)
    }
  }

  return (
    <Panel title="Submit a reading" sub="POST /api/readings">
      <datalist id={`${listId}-locations`}>
        {locations.map((value) => (
          <option key={value} value={value} />
        ))}
      </datalist>
      <datalist id={`${listId}-types`}>
        {meterTypes.map((value) => (
          <option key={value} value={value} />
        ))}
      </datalist>
      <datalist id={`${listId}-metrics`}>
        {metricCodes.map((value) => (
          <option key={value} value={value} />
        ))}
      </datalist>

      <form
        className="form-grid"
        onSubmit={(event) => {
          event.preventDefault()
          void submit()
        }}
      >
        <label>
          Location
          <input
            className="control"
            list={`${listId}-locations`}
            value={location}
            onChange={(event) => setLocation(event.target.value)}
            required
          />
        </label>

        <label>
          Meter type
          <input
            className="control"
            list={`${listId}-types`}
            value={meterType}
            onChange={(event) => setMeterType(event.target.value)}
            required
          />
        </label>

        <label>
          Metric
          <input
            className="control"
            list={`${listId}-metrics`}
            value={metricCode}
            onChange={(event) => setMetricCode(event.target.value)}
            required
          />
        </label>

        <label>
          Value kind
          <select
            className="control"
            value={kind}
            onChange={(event) => setKind(event.target.value as ValueKind)}
          >
            <option value="numeric">Numeric</option>
            <option value="flag">Flag</option>
          </select>
        </label>

        {kind === 'numeric' ? (
          <label>
            Value
            <input
              className="control"
              type="number"
              step="any"
              value={numeric}
              onChange={(event) => setNumeric(event.target.value)}
              required
            />
          </label>
        ) : (
          <label>
            Value
            <select
              className="control"
              value={flag}
              onChange={(event) => setFlag(event.target.value)}
            >
              <option value="true">true</option>
              <option value="false">false</option>
            </select>
          </label>
        )}

        <div className="form-foot" style={{ gridColumn: '1 / -1' }}>
          <button type="submit" className="btn" disabled={pending || !ready}>
            {pending ? 'Sending…' : 'Publish'}
          </button>
          <span className={failed ? 'hint bad' : 'hint'}>
            {message ?? 'Goes to RabbitMQ, so it comes back through the live feed.'}
          </span>
        </div>
      </form>
    </Panel>
  )
}
