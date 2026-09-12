using System.Reflection;

using UserManagementAPI.Application.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.UnitTests.Architecture;

/// <summary>
/// The layer rules BUILD-PLAN section 6.2 and docs/architecture.md describe,
/// asserted instead of reviewed. They held through every milestone because the
/// helper script for each pull request grepped for them; those scripts are not
/// in the repository, so until now nothing failed when someone reached through
/// a layer.
///
/// Read from the compiled assemblies rather than the csproj files: the compiler
/// drops a reference nothing uses, so this is what the code actually depends
/// on, which is the thing the rule is about.
/// </summary>
public sealed class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;

    private static readonly Assembly ApplicationAssembly = typeof(RequestParameters).Assembly;

    [Fact]
    public void Domain_DependsOnNothingButTheBaseClassLibrary()
    {
        var offenders = DomainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !IsBaseClassLibrary(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "the domain project holds no PackageReference and the model owes nothing to any framework");
    }

    [Fact]
    public void Application_DependsOnNoPersistenceOrWebFramework()
    {
        string[] forbidden = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql"];

        var offenders = ApplicationAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => forbidden.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "queries stay in Application and their handlers moved to Infrastructure (ADR 0016) to keep this true");
    }

    private static bool IsBaseClassLibrary(string assemblyName) =>
        assemblyName.StartsWith("System", StringComparison.Ordinal)
        || assemblyName is "netstandard" or "mscorlib";
}