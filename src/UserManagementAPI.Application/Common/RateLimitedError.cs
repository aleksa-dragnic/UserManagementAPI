using UserManagementAPI.Domain.Common;

namespace UserManagementAPI.Application.Common;

/// <summary>
/// A refusal that ends by itself: the caller may try again after
/// <see cref="RetryAfter"/>. The Api answers it with 429 and a Retry-After
/// header, the same shape as the rate limiter's own refusal.
/// </summary>
public sealed record RateLimitedError(string Code, string Description, TimeSpan RetryAfter)
    : Error(Code, Description, ErrorType.TooManyRequests);
