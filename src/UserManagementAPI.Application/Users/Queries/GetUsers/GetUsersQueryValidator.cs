using FluentValidation;

using UserManagementAPI.Domain.Common;
using UserManagementAPI.Domain.Users;

namespace UserManagementAPI.Application.Users.Queries.GetUsers;

/// <summary>
/// A status that is not a status is a mistake worth reporting — silently
/// ignoring it would return every user to a client that asked for some. An
/// unknown sort field, by contrast, is ignored and the default order applies.
/// </summary>
public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public const int MaxSearchTermLength = 100;

    private static readonly string[] StatusNames =
        Enumeration.GetAll<UserStatus>().Select(status => status.Name).ToArray();

    public GetUsersQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(BeAKnownStatus)
            .When(query => !string.IsNullOrWhiteSpace(query.Status))
            .WithMessage($"Status must be one of: {string.Join(", ", StatusNames)}.");

        RuleFor(query => query.SearchTerm)
            .MaximumLength(MaxSearchTermLength);
    }

    private static bool BeAKnownStatus(string? status) =>
        StatusNames.Any(name => string.Equals(name, status?.Trim(), StringComparison.OrdinalIgnoreCase));
}