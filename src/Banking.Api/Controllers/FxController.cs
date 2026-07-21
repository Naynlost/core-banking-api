using Banking.Api.Contracts;
using Banking.Api.Extensions;
using Banking.Application.Fx;
using Banking.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/fx")]
public sealed class FxController(IDispatcher dispatcher) : ControllerBase
{
    // Transfer öncesi kuru ve hesaplanacak tutarı gösterir; deftere dokunmaz
    [HttpGet("quote")]
    public async Task<IActionResult> Quote(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] decimal amount,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new GetFxQuoteQuery(from, to, amount), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.FailureProblem(result.Error);
    }

    // Bankanın döviz stoğunu artırır. Müşteri değil hazine rolü korumalıdır: çapraz kur
    // transferi ancak bankanın hedef para biriminde pozisyonu varsa gerçekleşebilir.
    [HttpPost("positions")]
    [Authorize(Roles = FxTreasury.OperatorRole)]
    public async Task<IActionResult> FundPosition(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        FundFxPositionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new FundFxPositionCommand(
            idempotencyKey?.Trim() ?? string.Empty,
            User.GetUserId(),
            request.Amount,
            request.CurrencyCode);

        var result = await dispatcher.SendAsync(command, cancellationToken);

        return result.IsSuccess
            ? Ok(new FundFxPositionResponse(result.Value))
            : this.FailureProblem(result.Error);
    }
}
