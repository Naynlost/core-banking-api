using Banking.Api.Contracts;
using Banking.Api.Extensions;
using Banking.Application.Fraud;
using Banking.Application.Fraud.ListFraudAlerts;
using Banking.Application.Fraud.ResolveFraudAlert;
using Banking.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

/// <summary>
/// Back-office review queue for flagged transactions. Unlike the customer
/// endpoints this is gated by the fraud-reviewer role: a regular customer
/// token gets 403 regardless of account ownership.
/// </summary>
[ApiController]
[Authorize(Roles = FraudReview.ReviewerRole)]
[Route("api/fraud-alerts")]
public sealed class FraudAlertsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        CancellationToken cancellationToken,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await dispatcher.QueryAsync(
            new ListFraudAlertsQuery(status, page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.FailureProblem(result.Error);
    }

    /// <summary>Closes an open alert as confirmed fraud or a dismissed false positive.</summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id, ResolveFraudAlertRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new ResolveFraudAlertCommand(id, request.Resolution, request.Note), cancellationToken);

        return result.IsSuccess ? NoContent() : this.FailureProblem(result.Error);
    }
}
