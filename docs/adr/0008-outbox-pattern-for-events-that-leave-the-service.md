# 0008 — Outbox pattern for events that leave the service

## Context

Domain events are dispatched inside the write transaction (ADR 0007). That is
the right guarantee for reactions inside this service and the wrong one for
anything outside it. An email sent from inside a transaction that then rolls
back cannot be unsent. A call to another service from inside a database
transaction holds row locks for the length of a network round trip. And a
publish that happens after the commit, in the same request, is lost if the
process dies between the two — the user exists and nobody was told.

The textbook answer is a distributed transaction across the database and the
broker. Almost nothing supports that any more, and nothing should.

## Decision

An outbox. When a domain event has consequences outside the service, its handler
writes an `OutboxMessage` — the event serialized to `jsonb` with its type name —
through the same `DbContext` that is mid-save. The message is inserted by the
very `SaveChanges` that raised the event: same statement batch, same
transaction. If the write rolls back, the message was never there.

A hosted `OutboxProcessor` polls `outbox_messages` on an interval, takes a
batch of unprocessed rows in occurrence order, hands each to an
`IOutboxPublisher`, and marks it processed. A failure records the error,
increments the attempt count and schedules the next attempt with exponential
backoff. After a configured ceiling the message is abandoned and left for a
human — a poison message costs a few retries and then stops, it never blocks
the messages behind it.

The publisher in this repository writes a log line. There is no broker, and
the pattern is the point; a real transport replaces one registration.

## Consequences

At-least-once delivery without a two-phase commit. A message is published at
least once because it is durable before anyone tries; it may be published more
than once if the process dies between publishing and marking processed, so
consumers must be idempotent. That is the standard trade and it is stated here
so nobody later assumes exactly-once.

Latency is bounded by the polling interval, five seconds by default. A shorter
interval costs an idle query per tick; a listen/notify wake-up would remove the
polling and is a reasonable future change.

The processor runs in a single instance. With several replicas the poll would
need `FOR UPDATE SKIP LOCKED` so two workers never take the same row. That is
not needed for one container on Fly.io or Render, and is recorded as the first
thing to add if a second one ever appears.

`outbox_messages` and its processor are infrastructure. The domain raises the
same events it always did and knows nothing about the table.