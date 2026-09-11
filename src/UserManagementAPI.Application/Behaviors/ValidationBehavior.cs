using System.Reflection;

using FluentValidation;
using FluentValidation.Results;

using UserManagementAPI.Application.Abstractions;
using UserManagementAPI.Application.Common;
using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Behaviors;

/// <summary>
/// Runs every validator registered for the request and short-circuits with a
/// failed Result when any of them fails. It does not throw: a request with the
/// wrong shape is an expected outcome, not an exceptional one.
///
/// This checks shape — required, lengths, formats. Invariants — a unique email,
/// a legal state transition — live in the domain and are checked when the
/// aggregate is asked to change. The two are different questions and both exist.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TResponse : Result
{
    private static readonly MethodInfo GenericFailure = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Result.Failure) && method.IsGenericMethodDefinition);

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            var validationResult = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(validationResult.Errors);
        }

        if (failures.Count == 0)
        {
            return await next();
        }

        var errors = failures
            .Select(failure => new Error($"{ValidationError.CodePrefix}{failure.PropertyName}", failure.ErrorMessage))
            .ToList();

        return CreateFailure(new ValidationError(errors));
    }

    /// <summary>
    /// TResponse is either Result or Result&lt;T&gt;; the failure has to be built as
    /// the exact type the handler would have returned.
    /// </summary>
    private static TResponse CreateFailure(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(error);
        }

        var valueType = typeof(TResponse).GetGenericArguments()[0];

        return (TResponse)GenericFailure.MakeGenericMethod(valueType).Invoke(null, [error])!;
    }
}