using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Messaging;

// Handler'dan önce çalışır; kurallar domain ile aynı hata kodlarını üretir, çağıran hangi katmanın reddettiğini bilmez
internal static class DispatcherValidation
{
    public const string FallbackError = "validation.invalid_request";

    // Geçersizse ilk hata kodunu, geçerliyse null döner
    public static async Task<string?> FindFailureAsync<TMessage>(
        TMessage message, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        foreach (var validator in serviceProvider.GetServices<IValidator<TMessage>>())
        {
            var result = await validator.ValidateAsync(new ValidationContext<TMessage>(message), cancellationToken);
            if (!result.IsValid)
            {
                var failure = result.Errors[0];
                return string.IsNullOrEmpty(failure.ErrorCode) ? FallbackError : failure.ErrorCode;
            }
        }

        return null;
    }
}
