using Banking.Application.Messaging;

namespace Banking.Application.Transactions.ReverseTransaction;

// Requester, orijinalin alacaklandırdığı hesaba sahip olmalı; idempotency key gerekmez
// çünkü reversal linkindeki unique index ikinci ters kaydı zaten imkansız kılar
public sealed record ReverseTransactionCommand(Guid TransactionId, string Requester) : ICommand<Guid>;
