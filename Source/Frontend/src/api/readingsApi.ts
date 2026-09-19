export interface SubmitReadingRequest {
  location: string
  meterType: string
  metricCode: string
  numeric?: number
  flag?: boolean
}

export interface SubmitReadingsResponse {
  batchId: string
  capturedAt: string
  readingCount: number
}

interface ValidationProblem {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class SubmitError extends Error {}

const describe = (problem: ValidationProblem, status: number): string => {
  const fromErrors = Object.values(problem.errors ?? {})
    .flat()
    .join(' ')

  return fromErrors || problem.detail || problem.title || `Request failed with ${status}.`
}

export const submitReadings = async (
  readings: readonly SubmitReadingRequest[],
  signal?: AbortSignal,
): Promise<SubmitReadingsResponse> => {
  const response = await fetch('/api/readings', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ readings }),
    signal,
  })

  if (!response.ok) {
    let problem: ValidationProblem = {}

    try {
      problem = (await response.json()) as ValidationProblem
    } catch {
      problem = {}
    }

    throw new SubmitError(describe(problem, response.status))
  }

  return (await response.json()) as SubmitReadingsResponse
}
