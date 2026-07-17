using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Messaging;

/// <summary>
/// The dispatcher's validation step: every registered validator for the message
/// type runs before the handler, and the first violation short-circuits the
/// dispatch as a normal failed Result. Rules carry the same machine-readable
/// error codes the domain would produce, so callers cannot tell (and need not
/// care) which layer rejected the input.
/// </summary>
internal static class DispatcherValidation
{
    public const string FallbackError = "validation.invalid_request";

    /// <summary>Returns the first validation error code, or null when the message is valid.</summary>
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
