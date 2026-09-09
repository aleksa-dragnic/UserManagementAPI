# 0007 — Domain events dispatched before commit, inside the same transaction

## Context

An aggregate raises a domain event when something happens that other parts of
the system care about. Something has to take those events off the aggregate and
run their handlers, and where that happens decides what the handlers can assume.

Dispatching after the commit means a handler runs against work that is already
durable, so a handler that fails leaves the system in a state where half of what
should have happened did. Dispatching inside a handler, by hand, means every
handler that writes has to remember to do it.

## Decision

A `SaveChangesInterceptor` collects domain events from every tracked aggregate
in `SavingChangesAsync`, clears them from the aggregates, and dispatches each
one — all before the write is committed.

Because the interceptor runs inside `SaveChanges`, the handlers execute in the
same transaction as the write that raised them. A handler that throws takes the
original write down with it.

Events are cleared before dispatch rather than after, so a handler that touches
the same aggregate and triggers a nested save cannot replay the events it is
currently handling.

## Consequences

Consistency is all-or-nothing across a write and its reactions, which is the
right guarantee for anything inside this service — an audit entry that exists
for a user registration that rolled back is worse than no audit entry.

It is the wrong guarantee for anything that leaves the service. An email sent
inside a transaction that then rolls back cannot be unsent, and a call to
another service inside a database transaction holds locks for the length of a
network round trip. Those go through the outbox instead (ADR 0008, M3 PR13):
the intent is written to a table in the same transaction, and a background
worker publishes it afterwards.

Handlers must therefore stay short and local. A slow handler is a long
transaction.