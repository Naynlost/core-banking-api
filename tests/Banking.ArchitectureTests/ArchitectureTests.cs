using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace Banking.ArchitectureTests;

/// <summary>
/// Clean Architecture bağımlılık kurallarını derleme sonrası doğrular (bkz. ADR 0001).
/// Kurallar derleyici tarafından tam zorlanamadığı için buradaki testler güvenlik ağıdır:
/// yanlış yönde eklenen bir referans veya using CI'da bu testleri kırar.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Banking.Domain.ValueObjects.Money).Assembly;
    private static readonly Assembly Application = typeof(Banking.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Banking.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void Domain_DoesNotDependOnOuterLayers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Banking.Application", "Banking.Infrastructure", "Banking.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    [Fact]
    public void Domain_DoesNotReferenceAnyExternalPackage()
    {
        // Domain saf C# kalmalı: BCL dışında hiçbir assembly'ye referans veremez.
        var external = Domain.GetReferencedAssemblies()
            .Where(a => a.Name is not null
                && !a.Name.StartsWith("System", StringComparison.Ordinal)
                && a.Name != "netstandard"
                && a.Name != "mscorlib")
            .Select(a => a.Name)
            .ToList();

        external.ShouldBeEmpty($"Domain dış paketlere bağımlı olamaz: {string.Join(", ", external)}");
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Banking.Infrastructure", "Banking.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    [Fact]
    public void Application_DoesNotDependOnPersistenceOrWebFrameworks()
    {
        // Use case katmanı EF Core / ASP.NET Core / broker istemcisi tanımaz;
        // dış dünya ile yalnızca kendi arayüzleri üzerinden konuşur.
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "RabbitMQ")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("Banking.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    [Fact]
    public void Api_DoesNotBypassApplicationByUsingRepositoriesDirectly()
    {
        // Controller'lar handler'lara dispatcher üzerinden gider; repository'leri
        // doğrudan enjekte edip use case katmanını atlayamazlar.
        var result = Types.InAssembly(Api)
            .That().ResideInNamespace("Banking.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Banking.Application.Abstractions",
                "Banking.Infrastructure.Persistence.Repositories")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailingTypes(result));
    }

    private static string FailingTypes(TestResult result)
        => result.IsSuccessful
            ? string.Empty
            : "Kuralı ihlal eden tipler: " + string.Join(", ", result.FailingTypeNames ?? []);
}
