# 0021 — Optimistic concurrency on users, through a version and If-Match

- **Status:** Accepted
- **Date:** 2026-10-18
- **PR:** #74

## Context

Until now the last write to a user won. The ETag on a user is a cache
validator: a hash of the body, compared weakly, used for conditional GET and
304. Users carried no concurrency token and the API did not read `If-Match`, so
two administrators editing the same user overwrote each other without either
knowing. The README listed it as the first Known limit.

The admin console decided not to build on the ETag for this (its build plan,
decision 12). `If-Match` needs a strong comparison, and the edge in front of the
deployed instance weakens a tag for a client that does not ask for compression;
a version the client reads from the body and sends back does not depend on the
edge at all. Refresh tokens already use PostgreSQL's `xmin` as a concurrency
token, and a lost race there answers 409 (ADR 0009).

## Decision

A user has a version: PostgreSQL's `xmin`, mapped as the users table's
concurrency token. Every write to a user updates its row, so the version changes
on every write. No column is added; the `AddUserVersion` migration only records
the token in the model snapshot.

`GET /api/v1/users/{id}` returns it as `version`, a string. The five writes on a
user — `PUT /api/v1/users/{id}`, `POST` and `DELETE /api/v1/users/{id}/lock`,
`POST /api/v1/users/{id}/roles` and `DELETE /api/v1/users/{id}/roles/{roleId}` —
accept it back as `If-Match: "<version>"`.

- A version that is not the current one is refused with **412 Precondition
  Failed**, `errorCode` `User.VersionMismatch`, before anything changes.
- A write that passes the check but loses a race to another write between its
  read and its save is refused the same way: the update's `WHERE xmin = …`
  matches no row, and `AppDbContext` reports it as the same error.
- **No `If-Match` means no check**, as before. `*` means no check. A weak tag, an
  unquoted value or anything that is not a version is a 400,
  `Request.InvalidIfMatch`.
- 428 Precondition Required is deliberately not used: requiring the header would
  break every client written against 2.0.

Version 2 has no write endpoints, so its user resource does not carry the
version.

## Consequences

A client that sends the version it read cannot overwrite a newer state, and is
told so with a status that means exactly that. A client that does not send it
behaves as before, so the change is additive and ships as a minor version,
`v2.1.0`.

The version is opaque: a client compares it and sends it back, nothing more.
`xmin` is a 32-bit transaction id that wraps around, which is harmless for
equality between a read and the write that follows it.

Two kinds of conflict now answer differently on purpose: a user changed by
someone else is 412, a refresh token presented twice at once is 409, because the
client's next step differs — read the user again, or start a new session.

The ETag stays a cache validator. Making it the version as well would tie write
safety to how an edge rewrites headers, which is the reason the console gave for
keeping them apart.
