using Banking.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dispatcher and every command/query handler in this assembly,
    /// so adding a new use case never requires touching DI configuration.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var handlerDefinitions = new[]
        {
            typeof(ICommandHandler<>),
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>),
        };

        var handlerRegistrations =
            from type in typeof(DependencyInjection).Assembly.GetTypes()
            where type is { IsAbstract: false, IsInterface: false }
            from implemented in type.GetInterfaces()
            where implemented.IsGenericType
                && handlerDefinitions.Contains(implemented.GetGenericTypeDefinition())
            select (Service: implemented, Implementation: type);

        foreach (var (service, implementation) in handlerRegistrations)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}
