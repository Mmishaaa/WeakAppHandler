import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AlertRuleForm } from './AlertRuleForm'
import { emptyAlertRuleFormValues } from './alertRuleValidation'

describe('AlertRuleForm', () => {
  it('shows no field errors before the first submit attempt', () => {
    render(
      <AlertRuleForm
        initialValues={emptyAlertRuleFormValues()}
        submitLabel="Create rule"
        onSubmit={vi.fn()}
        submitting={false}
      />,
    )

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('shows inline errors and does not call onSubmit when required fields are missing', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(
      <AlertRuleForm
        initialValues={emptyAlertRuleFormValues()}
        submitLabel="Create rule"
        onSubmit={onSubmit}
        submitting={false}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Create rule' }))

    expect(screen.getByText('Name is required.')).toBeInTheDocument()
    expect(screen.getByText('Metric is required.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('submits the parsed request once every field is valid', async () => {
    const user = userEvent.setup()
    const onSubmit = vi.fn()
    render(
      <AlertRuleForm
        initialValues={emptyAlertRuleFormValues()}
        submitLabel="Create rule"
        onSubmit={onSubmit}
        submitting={false}
      />,
    )

    await user.type(screen.getByLabelText('Name'), 'CO2 too high')
    await user.selectOptions(screen.getByLabelText('Metric'), 'co2')
    await user.selectOptions(screen.getByLabelText('Operator'), 'gt')
    await user.type(screen.getByLabelText(/Threshold/), '1000')
    await user.selectOptions(screen.getByLabelText('Severity'), 'critical')
    await user.click(screen.getByRole('button', { name: 'Create rule' }))

    expect(onSubmit).toHaveBeenCalledTimes(1)
    const submitted = onSubmit.mock.calls[0][0]
    expect(submitted.name).toBe('CO2 too high')
    expect(submitted.metricCode).toBe('co2')
    expect(submitted.thresholdNumeric).toBe('1000')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('switches the threshold input to a Yes/No select for a boolean metric', async () => {
    const user = userEvent.setup()
    render(
      <AlertRuleForm
        initialValues={emptyAlertRuleFormValues()}
        submitLabel="Create rule"
        onSubmit={vi.fn()}
        submitting={false}
      />,
    )

    await user.selectOptions(screen.getByLabelText('Metric'), 'motion_detected')

    const thresholdSelect = screen.getByLabelText(/Threshold/) as HTMLSelectElement
    expect(thresholdSelect.tagName).toBe('SELECT')
    expect(screen.getByRole('option', { name: 'Yes' })).toBeInTheDocument()
  })

  it('renders a server error message when provided', () => {
    render(
      <AlertRuleForm
        initialValues={emptyAlertRuleFormValues()}
        submitLabel="Create rule"
        onSubmit={vi.fn()}
        submitting={false}
        serverError="A rule with this name already exists."
      />,
    )

    expect(screen.getByText('A rule with this name already exists.')).toBeInTheDocument()
  })
})
