using Banking.Application.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Application;

public static class DependencyInjection
{
    // Assembly'deki tüm handler/validator'ları tarar; yeni bir use case DI konfigürasyonuna dokunmaz
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();

        var handlerDefinitions = new[]
        {
            typeof(ICommandHandler<>),
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>),
            typeof(IValidator<>),
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
