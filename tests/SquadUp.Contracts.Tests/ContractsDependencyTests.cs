namespace SquadUp.Contracts.Tests;

public sealed class ContractsDependencyTests
{
    private static readonly string[] ForbiddenDependencyFragments =
    [
        ".Domain",
        "EntityFrameworkCore",
        "MassTransit"
    ];

    [Fact]
    public void ContractsMustNotReferenceDomainOrInfrastructureDependencies()
    {
        var referencedAssemblyNames = typeof(ContractsAssembly)
            .Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            referencedAssemblyNames,
            assemblyName => ForbiddenDependencyFragments.Any(
                fragment => assemblyName.Contains(
                    fragment,
                    StringComparison.OrdinalIgnoreCase)));
    }
}
