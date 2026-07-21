using Banking.Api.Contracts;
using Banking.Api.Extensions;
using Banking.Application.Messaging;
using Banking.Application.Transactions.ReverseTransaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(IDispatcher dispatcher) : ControllerBase
{
    // Ledger append-only olduğundan orijinale dokunulmaz, ters çevrilmiş satırlarla yeni işlem eklenir
    [HttpPost("{id:guid}/reversal")]
    public async Task<IActionResult> Reverse(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(
            new ReverseTransactionCommand(id, User.GetUserId()), cancellationToken);

        return result.IsSuccess
            ? Ok(new ReversalResponse(result.Value))
            : this.FailureProblem(result.Error);
    }
}
