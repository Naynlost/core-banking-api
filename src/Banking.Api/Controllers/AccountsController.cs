using Banking.Api.Contracts;
using Banking.Api.Extensions;
using Banking.Application.Accounts.CompleteKyc;
using Banking.Application.Accounts.CreateAccount;
using Banking.Application.Accounts.GetAccount;
using Banking.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateAccountCommand(User.GetUserId(), request.CurrencyCode);
        var result = await dispatcher.SendAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.FailureProblem(result.Error);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value },
            new CreateAccountResponse(result.Value));
    }

    /// <summary>
    /// Marks the caller's account as KYC-verified. Stands in for a real
    /// verification flow; without it the account cannot send transfers.
    /// </summary>
    [HttpPost("{id:guid}/kyc")]
    public async Task<IActionResult> CompleteKyc(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new CompleteKycCommand(id, User.GetUserId()), cancellationToken);

        return result.IsSuccess ? NoContent() : this.FailureProblem(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new GetAccountQuery(id, User.GetUserId()), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.FailureProblem(result.Error);
    }
}
