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
    /// <summary>
    /// Posts the reversal of a transaction. The ledger is append-only, so the
    /// original is never touched; a counter-transaction with flipped entries is
    /// added. Only the owner of an account the transaction credited can do this.
    /// </summary>
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
