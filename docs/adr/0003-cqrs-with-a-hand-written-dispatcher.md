# 0003 — CQRS with a hand-written dispatcher instead of a mediator library

## Context

Commands and queries are separate request types with separate handlers, and a
set of cross-cutting steps — logging, validation, a transaction — has to run
around every command without each handler repeating it. Something has to take a
request, find its handler, and run the pipeline.

The usual answer is a mediator library. It does the job, but it brings a
dependency into the layer that is supposed to have the fewest, its recent
versions carry a commercial licence, and its pipeline model is general enough
that reading a handler no longer tells you what actually runs around it.

## Decision

A hand-written dispatcher. `IDispatcher` has three methods — send a command with
no response, send a command with a response, run a query. Handlers implement
closed generic interfaces (`ICommandHandler<TCommand>`,
`ICommandHandler<TCommand, TResponse>`, `IQueryHandler<TQuery, TResponse>`) and
are registered by scanning the Application assembly once at startup.

Behaviors implement `IPipelineBehavior<TRequest, TResponse>` and are registered
as open generics. The dispatcher resolves the behaviors for the concrete request
type and folds them around the handler, first registered outermost. A behavior
that should only apply to commands says so with a generic constraint on
`IBaseCommand`; the container skips it for request types that do not satisfy the
constraint.

The whole dispatcher is one class. Because a request arrives typed as its
interface, one closed wrapper per request type is created and cached; inside the
wrapper everything is statically typed and there is no further reflection on the
hot path.

## Consequences

No package, no licence, nothing to upgrade. The pipeline is explicit: the
registration order in `AddApplication` is the execution order, and a reader can
follow a request from controller to handler through code that is all in the
repository.

What is given up is the library's breadth — notifications with multiple
handlers, streaming requests, pre- and post-processors. None of those are needed
here. Domain events already have their own publisher (ADR 0007), and if a
second consumer of a request ever appears, the dispatcher is small enough to
extend rather than replace.

The cost is ownership. A bug in the dispatcher is this project's bug. That is
accepted for eighty lines with a test on each path.