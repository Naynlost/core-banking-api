using Banking.Api.Contracts;
using Banking.Api.Extensions;
using Banking.Application.Messaging;
using Banking.Application.Transfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transfers")]
public sealed class TransfersController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Transfer(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        TransferRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return this.FailureProblem("transfer.idempotency_key_required");
        }

        var command = new TransferMoneyCommand(
            idempotencyKey.Trim(),
            User.GetUserId(),
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.CurrencyCode);

        var result = await dispatcher.SendAsync(command, cancellationToken);

        return result.IsSuccess
            ? Ok(new TransferResponse(result.Value))
            : this.FailureProblem(result.Error);
    }
}
