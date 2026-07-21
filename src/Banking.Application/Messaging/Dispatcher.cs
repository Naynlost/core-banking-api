using System.Collections.Concurrent;
using System.Diagnostics;
using Banking.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application.Messaging;

// Mesaj tipi başına generic wrapper bir kez oluşturulup cache'lenir, sonraki dispatch'ler reflection'sız çalışır
internal sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public async Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        var wrapper = (CommandWrapper)Wrappers.GetOrAdd(
            command.GetType(),
            static type => Activator.CreateInstance(typeof(CommandWrapper<>).MakeGenericType(type))!);

        using var activity = StartActivity(command.GetType().Name);
        var result = await wrapper.HandleAsync(command, serviceProvider, cancellationToken);
        TagOutcome(activity, result);
        return result;
    }

    public async Task<Result<TResult>> SendAsync<TResult>(
        ICommand<TResult> command, CancellationToken cancellationToken)
    {
        var wrapper = (ResultCommandWrapper<TResult>)Wrappers.GetOrAdd(
            command.GetType(),
            static (type, resultType) =>
                Activator.CreateInstance(typeof(ResultCommandWrapper<,>).MakeGenericType(type, resultType))!,
            typeof(TResult));

        using var activity = StartActivity(command.GetType().Name);
        var result = await wrapper.HandleAsync(command, serviceProvider, cancellationToken);
        TagOutcome(activity, result);
        return result;
    }

    public async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
    {
        var wrapper = (QueryWrapper<TResult>)Wrappers.GetOrAdd(
            query.GetType(),
            static (type, resultType) =>
                Activator.CreateInstance(typeof(QueryWrapper<,>).MakeGenericType(type, resultType))!,
            typeof(TResult));

        using var activity = StartActivity(query.GetType().Name);
        var result = await wrapper.HandleAsync(query, serviceProvider, cancellationToken);
        TagOutcome(activity, result);
        return result;
    }

    private static Activity? StartActivity(string messageName) =>
        BankingDiagnostics.ActivitySource.StartActivity($"handle {messageName}");

    // İş kuralı hataları span error değil, tag olarak görünür
    private static void TagOutcome(Activity? activity, Result result) =>
        activity?.SetTag("banking.outcome", result.IsSuccess ? "success" : result.Error);

    private abstract class CommandWrapper
    {
        public abstract Task<Result> HandleAsync(
            ICommand command, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    }

    private sealed class CommandWrapper<TCommand> : CommandWrapper
        where TCommand : ICommand
    {
        public override async Task<Result> HandleAsync(
            ICommand command, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var violation = await DispatcherValidation.FindFailureAsync(
                (TCommand)command, serviceProvider, cancellationToken);
            if (violation is not null)
            {
                return Result.Failure(violation);
            }

            return await serviceProvider.GetRequiredService<ICommandHandler<TCommand>>()
                .HandleAsync((TCommand)command, cancellationToken);
        }
    }

    private abstract class ResultCommandWrapper<TResult>
    {
        public abstract Task<Result<TResult>> HandleAsync(
            ICommand<TResult> command, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    }

    private sealed class ResultCommandWrapper<TCommand, TResult> : ResultCommandWrapper<TResult>
        where TCommand : ICommand<TResult>
    {
        public override async Task<Result<TResult>> HandleAsync(
            ICommand<TResult> command, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var violation = await DispatcherValidation.FindFailureAsync(
                (TCommand)command, serviceProvider, cancellationToken);
            if (violation is not null)
            {
                return Result.Failure<TResult>(violation);
            }

            return await serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>()
                .HandleAsync((TCommand)command, cancellationToken);
        }
    }

    private abstract class QueryWrapper<TResult>
    {
        public abstract Task<Result<TResult>> HandleAsync(
            IQuery<TResult> query, IServiceProvider serviceProvider, CancellationToken cancellationToken);
    }

    private sealed class QueryWrapper<TQuery, TResult> : QueryWrapper<TResult>
        where TQuery : IQuery<TResult>
    {
        public override async Task<Result<TResult>> HandleAsync(
            IQuery<TResult> query, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var violation = await DispatcherValidation.FindFailureAsync(
                (TQuery)query, serviceProvider, cancellationToken);
            if (violation is not null)
            {
                return Result.Failure<TResult>(violation);
            }

            return await serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>()
                .HandleAsync((TQuery)query, cancellationToken);
        }
    }
}
