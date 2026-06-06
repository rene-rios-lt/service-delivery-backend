using NetArchTest.Rules;

namespace ServiceDelivery.Architecture.Tests;

public class CleanArchitectureTests
{
    private const string DomainNamespace = "ServiceDelivery.Domain";
    private const string ApplicationNamespace = "ServiceDelivery.Application";
    private const string InfrastructureNamespace = "ServiceDelivery.Infrastructure";
    private const string ApiNamespace = "ServiceDelivery.Api";

    [Fact]
    public void Domain_Should_Not_Reference_Application()
    {
        var result = Types.InAssembly(typeof(ServiceDelivery.Domain.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain must not reference Application.");
    }

    [Fact]
    public void Domain_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ServiceDelivery.Domain.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain must not reference Infrastructure.");
    }

    [Fact]
    public void Domain_Should_Not_Reference_Api()
    {
        var result = Types.InAssembly(typeof(ServiceDelivery.Domain.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain must not reference Api.");
    }

    [Fact]
    public void Application_Should_Not_Reference_Infrastructure()
    {
        var result = Types.InAssembly(typeof(ServiceDelivery.Application.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application must not reference Infrastructure.");
    }

    [Fact]
    public void Application_Should_Not_Reference_Api()
    {
        var result = Types.InAssembly(typeof(ServiceDelivery.Application.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application must not reference Api.");
    }

    [Fact]
    public void Infrastructure_Should_Not_Reference_Api()
    {
        var result = Types.InAssembly(typeof(ServiceDelivery.Infrastructure.Class1).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, "Infrastructure must not reference Api.");
    }
}
