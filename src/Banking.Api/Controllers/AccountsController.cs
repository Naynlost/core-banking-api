using Banking.Api.Contracts;
using Banking.Api.Extensions;
using Banking.Application.Accounts.CloseAccount;
using Banking.Application.Accounts.CompleteKyc;
using Banking.Application.Accounts.CreateAccount;
using Banking.Application.Accounts.GetAccount;
using Banking.Application.Accounts.GetStatement;
using Banking.Application.Accounts.ListAccounts;
using Banking.Application.CashOperations;
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

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await dispatcher.QueryAsync(new ListAccountsQuery(User.GetUserId()), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.FailureProblem(result.Error);
    }

    // KYC doğrulanmadan hesap transfer gönderemez
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

    // En yeni kayıt önce
    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetStatement(
        Guid id,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await dispatcher.QueryAsync(
            new GetAccountStatementQuery(id, User.GetUserId(), page, pageSize), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : this.FailureProblem(result.Error);
    }

    [HttpPost("{id:guid}/deposits")]
    public async Task<IActionResult> Deposit(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CashOperationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DepositMoneyCommand(
            idempotencyKey?.Trim() ?? string.Empty,
            User.GetUserId(),
            id,
            request.Amount,
            request.CurrencyCode);

        var result = await dispatcher.SendAsync(command, cancellationToken);

        return result.IsSuccess
            ? Ok(new CashOperationResponse(result.Value))
            : this.FailureProblem(result.Error);
    }

    [HttpPost("{id:guid}/withdrawals")]
    public async Task<IActionResult> Withdraw(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CashOperationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new WithdrawMoneyCommand(
            idempotencyKey?.Trim() ?? string.Empty,
            User.GetUserId(),
            id,
            request.Amount,
            request.CurrencyCode);

        var result = await dispatcher.SendAsync(command, cancellationToken);

        return result.IsSuccess
            ? Ok(new CashOperationResponse(result.Value))
            : this.FailureProblem(result.Error);
    }

    // Bakiye sıfır değilse kapatma reddedilir
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var result = await dispatcher.SendAsync(new CloseAccountCommand(id, User.GetUserId()), cancellationToken);

        return result.IsSuccess ? NoContent() : this.FailureProblem(result.Error);
    }
}
