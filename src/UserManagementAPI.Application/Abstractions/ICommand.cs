namespace UserManagementAPI.Application.Abstractions;

/// <summary>
/// Common ancestor of both command shapes. It exists so a behavior can be
/// constrained to commands only — the transaction behavior wraps commands and
/// never queries, and that rule is expressed as a generic constraint rather than
/// a runtime type check.
/// </summary>
public interface IBaseCommand;

/// <summary>A request to change state that returns only success or failure.</summary>
public interface ICommand : IBaseCommand;

/// <summary>A request to change state that returns a value, such as the id of what it created.</summary>
public interface ICommand<TResponse> : IBaseCommand;