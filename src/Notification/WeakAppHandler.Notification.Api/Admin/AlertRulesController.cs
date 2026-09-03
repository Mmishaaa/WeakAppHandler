using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WeakAppHandler.Notification.Api.Domain;
using WeakAppHandler.Notification.Api.Persistence;
using WeakAppHandler.Notification.Api.Persistence.Converters;
using WeakAppHandler.ServiceDefaults.Auth;

namespace WeakAppHandler.Notification.Api.Admin;

/// <summary>
/// TASK-030's REST CRUD over <c>alert_rules</c>. Guarded by <see cref="ServicePolicies.Admin"/> - a
/// human admin's role claim, not a machine scope - since this is an operator managing rules through
/// a browser session, unlike the Ingestor's/Processor's machine-only admin surfaces.
/// </summary>
[ApiController]
[Route("api/v1/alert-rules")]
[Authorize(Policy = ServicePolicies.Admin)]
public sealed class AlertRulesController(
    AlertingDbContext db,
    IValidator<AlertRuleRequest> validator,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AlertRuleResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AlertRuleResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var rules = await db.AlertRules.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);

        return Ok(rules.Select(AlertRuleResponse.FromEntity).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AlertRuleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertRuleResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var rule = await db.AlertRules.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        return rule is null ? NotFound() : Ok(AlertRuleResponse.FromEntity(rule));
    }

    [HttpPost]
    [ProducesResponseType<AlertRuleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AlertRuleResponse>> Create(AlertRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ToValidationProblem(validation);
        }

        var now = timeProvider.GetUtcNow();
        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Location = request.Location,
            MeterType = request.MeterType,
            MetricCode = request.MetricCode,
            Operator = AlertOperatorConverter.FromCode(request.Operator),
            ThresholdNumeric = request.ThresholdNumeric,
            ThresholdBool = request.ThresholdBool,
            Severity = AlertSeverityConverter.FromCode(request.Severity),
            HysteresisPercent = request.HysteresisPercent ?? AlertRule.DefaultHysteresisPercent,
            CooldownSeconds = request.CooldownSeconds ?? AlertRule.DefaultCooldownSeconds,
            IsEnabled = request.IsEnabled ?? true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.AlertRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = rule.Id }, AlertRuleResponse.FromEntity(rule));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AlertRuleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertRuleResponse>> Update(Guid id, AlertRuleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ToValidationProblem(validation);
        }

        var rule = await db.AlertRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        rule.Name = request.Name;
        rule.Location = request.Location;
        rule.MeterType = request.MeterType;
        rule.MetricCode = request.MetricCode;
        rule.Operator = AlertOperatorConverter.FromCode(request.Operator);
        rule.ThresholdNumeric = request.ThresholdNumeric;
        rule.ThresholdBool = request.ThresholdBool;
        rule.Severity = AlertSeverityConverter.FromCode(request.Severity);
        rule.HysteresisPercent = request.HysteresisPercent ?? AlertRule.DefaultHysteresisPercent;
        rule.CooldownSeconds = request.CooldownSeconds ?? AlertRule.DefaultCooldownSeconds;
        rule.IsEnabled = request.IsEnabled ?? true;
        rule.UpdatedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);

        return Ok(AlertRuleResponse.FromEntity(rule));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var rule = await db.AlertRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rule is null)
        {
            return NotFound();
        }

        db.AlertRules.Remove(rule);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private ActionResult ToValidationProblem(ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
