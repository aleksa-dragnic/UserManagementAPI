using System.Reflection;
using System.Reflection.Emit;

using UserManagementAPI.Application.Auth;
using UserManagementAPI.Domain.Users;
using UserManagementAPI.Infrastructure.Persistence;

namespace UserManagementAPI.IntegrationTests.Architecture;

/// <summary>
/// Production code reads the time from <see cref="TimeProvider"/> and nowhere
/// else (#84): the domain takes it as a value, handlers and infrastructure
/// through the container, so a test can state it. Asserted on the compiled
/// method bodies of all four projects, like the layer rules, so a lambda or
/// an async method is covered too - the compiler moves those into generated
/// types, and those are scanned with the rest.
///
/// The one place allowed to name the system clock is where it is registered.
/// </summary>
public sealed class ClockTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(User).Assembly,
        typeof(TokenIssuer).Assembly,
        typeof(AppDbContext).Assembly,
        typeof(Program).Assembly
    ];

    private static readonly HashSet<string> SystemClock = new(StringComparer.Ordinal)
    {
        "System.DateTime.get_UtcNow",
        "System.DateTime.get_Now",
        "System.DateTime.get_Today",
        "System.DateTimeOffset.get_UtcNow",
        "System.DateTimeOffset.get_Now",
        "System.TimeProvider.get_System"
    };

    private const string Registration =
        "UserManagementAPI.Application.DependencyInjection.AddApplication -> System.TimeProvider.get_System";

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opCode => opCode.Value);

    [Fact]
    public void NoProductionCode_ReadsTheSystemClock_ExceptWhereTheClockIsRegistered()
    {
        var reads = ClockReads().ToList();

        // Without this the assertion below would also pass for a scanner that
        // sees no calls at all, which is the usual way an architecture test rots.
        reads.Should().Contain(Registration, "the scan must see the one read that is allowed");

        reads.Where(read => read != Registration).Should().BeEmpty(
            "the time comes from TimeProvider, injected or passed down as utcNow (#84)");
    }

    private static IEnumerable<string> ClockReads() =>
        from assembly in ProductionAssemblies
        from type in assembly.GetTypes()
        from method in MethodsOf(type)
        from callee in CalledMethods(method)
        let name = $"{callee.DeclaringType?.FullName}.{callee.Name}"
        where SystemClock.Contains(name)
        select $"{Owner(method)} -> {name}";

    private static IEnumerable<MethodBase> MethodsOf(Type type)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic |
                                      BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return type.GetMethods(declared).Cast<MethodBase>().Concat(type.GetConstructors(declared));
    }

    /// <summary>
    /// The methods a body calls, read from its IL. Every instruction is decoded
    /// with its operand size from the opcode table, so an operand byte is never
    /// mistaken for an opcode.
    /// </summary>
    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();

        if (il is null)
        {
            yield break;
        }

        var position = 0;

        while (position < il.Length)
        {
            var value = il[position] == 0xFE ? (short)(0xFE00 | il[position + 1]) : il[position];
            var opCode = OpCodesByValue[value];
            position += opCode.Size;

            if (opCode.OperandType == OperandType.InlineMethod &&
                Resolve(method, BitConverter.ToInt32(il, position)) is { } callee)
            {
                yield return callee;
            }

            position += OperandSize(opCode.OperandType, il, position);
        }
    }

    private static MethodBase? Resolve(MethodBase method, int token)
    {
        try
        {
            return method.Module.ResolveMethod(
                token,
                method.DeclaringType is { IsGenericType: true } type ? type.GetGenericArguments() : null,
                method.IsGenericMethod ? method.GetGenericArguments() : null);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static int OperandSize(OperandType operandType, byte[] il, int position) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, position),
        _ => 4
    };

    /// <summary>
    /// The method a reader would look for: a lambda or an async state machine
    /// is reported under the method it was written in, not its generated name.
    /// </summary>
    private static string Owner(MethodBase method)
    {
        var type = method.DeclaringType!;

        while (type.DeclaringType is not null && type.Name.Contains('<', StringComparison.Ordinal))
        {
            type = type.DeclaringType;
        }

        // A lambda is "<Method>b__0_0" in a generated class; an async or iterator
        // body is MoveNext in a generated "<Method>d__3".
        var name = method.Name.StartsWith('<')
            ? Written(method.Name)
            : method.DeclaringType!.Name.StartsWith('<')
                ? Written(method.DeclaringType.Name)
                : method.Name;

        return $"{type.FullName}.{name}";
    }

    private static string Written(string generated) => generated[1..generated.IndexOf('>', StringComparison.Ordinal)];
}
