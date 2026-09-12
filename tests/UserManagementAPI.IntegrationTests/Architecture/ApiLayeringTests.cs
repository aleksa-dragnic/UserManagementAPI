using System.Reflection;

using Microsoft.AspNetCore.Mvc;

using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.IntegrationTests.Architecture;

/// <summary>
/// The third rule in docs/architecture.md: Infrastructure reaches the Api
/// project only in Program.cs and Extensions/, where the container is wired, and
/// no controller names an infrastructure type. Asserted here rather than in
/// Application.UnitTests because only this project references both assemblies.
///
/// Signatures only - constructors, declared properties, method parameters and
/// return types, generic arguments expanded. A method body could still mention
/// an infrastructure type and reflection cannot see that; the constructor is
/// what carries the weight, since a dependency has to arrive through it.
/// </summary>
public sealed class ApiLayeringTests
{
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void NoControllerNamesAnInfrastructureType()
    {
        var controllers = typeof(Program).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        // Without this the assertion below passes an empty sequence and proves
        // nothing, which is the usual way an architecture test rots.
        controllers.Should().NotBeEmpty("the Api assembly must expose controllers for this rule to mean anything");

        var offenders = controllers
            .SelectMany(SignatureTypes)
            .Where(type => type.Assembly == InfrastructureAssembly)
            .Select(type => type.FullName!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static IEnumerable<Type> SignatureTypes(Type controller)
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var constructor in controller.GetConstructors(Declared))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var type in Expand(parameter.ParameterType)) { yield return type; }
            }
        }

        foreach (var property in controller.GetProperties(Declared))
        {
            foreach (var type in Expand(property.PropertyType)) { yield return type; }
        }

        foreach (var method in controller.GetMethods(Declared))
        {
            foreach (var type in Expand(method.ReturnType)) { yield return type; }

            foreach (var parameter in method.GetParameters())
            {
                foreach (var type in Expand(parameter.ParameterType)) { yield return type; }
            }
        }
    }

    /// <summary>A type and, recursively, its generic arguments: Task&lt;ActionResult&lt;T&gt;&gt; hides T.</summary>
    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in Expand(argument)) { yield return nested; }
        }
    }
}