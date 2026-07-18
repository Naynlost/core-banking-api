using Banking.Api.Contracts;
using Banking.Api.Extensions;
using Banking.Application.Messaging;
using Banking.Application.StandingOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/standing-orders")]
public sealed class StandingOrdersController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>
    /// Sets up a recurring transfer. Each occurrence is executed as a regular
    /// transfer in the background, so KYC, balance and daily limit rules apply
    /// at execution time.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateStandingOrderRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateStandingOrderCommand(
            User.GetUserId(),
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.CurrencyCode,
            request.Frequency,
            request.FirstRunAt);

        var result = await dispatcher.SendAsync(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(List), new CreateStandingOrderResponse(result.Value))
            : this.FailureProblem(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new ListStandingOrdersQuery(User.GetUserId()), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.FailureProblem(result.Error);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new CancelStandingOrderCommand(id, User.GetUserId()), cancellationToken);

        return result.IsSuccess ? NoContent() : this.FailureProblem(result.Error);
    }
}
