using Banking.Application.Messaging;
using Banking.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Banking.Application.Tests.Messaging;

public class DispatcherTests
{
    private sealed record PingCommand(string Name) : ICommand;

    private sealed record AddCommand(int Left, int Right) : ICommand<int>;

    private sealed record EchoQuery(string Text) : IQuery<string>;

    private sealed class PingCommandHandler : ICommandHandler<PingCommand>
    {
        public PingCommand? Received { get; private set; }

        public Task<Result> HandleAsync(PingCommand command, CancellationToken cancellationToken)
        {
            Received = command;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class AddCommandHandler : ICommandHandler<AddCommand, int>
    {
        public Task<Result<int>> HandleAsync(AddCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(command.Left + command.Right));
    }

    private sealed class EchoQueryHandler : IQueryHandler<EchoQuery, string>
    {
        public Task<Result<string>> HandleAsync(EchoQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(query.Text));
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendAsync_RoutesCommandToItsHandler()
    {
        var handler = new PingCommandHandler();
        await using var provider = BuildProvider(s => s.AddSingleton<ICommandHandler<PingCommand>>(handler));
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new PingCommand("merhaba"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        handler.Received.ShouldBe(new PingCommand("merhaba"));
    }

    [Fact]
    public async Task SendAsync_ReturnsHandlerResultForResultCommands()
    {
        await using var provider = BuildProvider(s =>
            s.AddSingleton<ICommandHandler<AddCommand, int>, AddCommandHandler>());
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new AddCommand(40, 2), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public async Task QueryAsync_RoutesQueryToItsHandler()
    {
        await using var provider = BuildProvider(s =>
            s.AddSingleton<IQueryHandler<EchoQuery, string>, EchoQueryHandler>());
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.QueryAsync(new EchoQuery("yankı"), CancellationToken.None);

        result.Value.ShouldBe("yankı");
    }

    [Fact]
    public async Task SendAsync_WithoutRegisteredHandler_Throws()
    {
        await using var provider = BuildProvider(_ => { });
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        await Should.ThrowAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new PingCommand("kayıp"), CancellationToken.None));
    }

    [Fact]
    public void AddApplication_RegistersAllHandlersInAssembly()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        services.ShouldContain(s =>
            s.ServiceType == typeof(ICommandHandler<Application.Accounts.CreateAccount.CreateAccountCommand, Guid>));
        services.ShouldContain(s =>
            s.ServiceType == typeof(IQueryHandler<
                Application.Accounts.GetAccount.GetAccountQuery,
                Application.Accounts.GetAccount.AccountResponse>));
    }
}
