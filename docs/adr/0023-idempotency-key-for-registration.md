# 0023 — An Idempotency-Key makes registering a user safe to retry

- **Status:** Accepted
- **Date:** 2026-11-24
- **PR:** [#127](https://github.com/aleksa-dragnic/UserManagementAPI/pull/127)

## Context

`POST /api/v1/users` is not idempotent. A client whose request timed out
cannot know whether the user was registered: retrying either registers it or
answers 409 `User.EmailNotUnique`, and the 409 does not say whether the first
attempt or someone else took the address. The unique email already prevents a
duplicate user; what is missing is a retry that gets the original answer.

#79 recorded this as not yet worth a table and a retention job. It is now,
because retry-safe writes are part of the production-hardening milestone.

## Decision

`POST /api/v1/users` accepts an optional `Idempotency-Key` header, after the
IETF `Idempotency-Key` draft and the way Stripe uses it.

- **Without the header nothing changes**, so the change is additive.
- **A key** is 1 to 255 visible ASCII characters; anything else is 400
  `Request.InvalidIdempotencyKey`, before anything runs.
- **Scope:** the caller (`sub`) and the operation (method and route template).
  Two callers' keys never collide, and one key on two endpoints is two keys.
- **First request:** runs. A 2xx response is stored with a SHA-256 of the
  request body: status, `Location`, content type and body, byte for byte.
- **Same key, same body:** the stored response again, with
  `Idempotency-Replayed: true`. No second user.
- **Same key, different body:** 422 `Idempotency.KeyReused`.
- **Same key while the first is still running:** 409 `Idempotency.InProgress`.
- **A response that is not 2xx gives the key back**, so a corrected retry runs.
  Only successes are replayed: a 4xx replayed for a day would punish a client
  for fixing its request.
- **Retention:** 24 hours, then the key is unclaimed. A cleanup service deletes
  expired rows every hour; an expired row is already treated as free, so the
  cleanup only bounds the table.

**How it is made safe under concurrency.** The plan on #79 proposed claiming the
key in the registration's own transaction. That does not work: a concurrent
request with the same key must *see* the claim to answer 409, and it cannot see
an uncommitted row. So the claim is its own committed statement,
`INSERT … ON CONFLICT DO NOTHING` on the primary key `(caller_id, operation,
key)`, which lets exactly one of two concurrent requests in on any number of
replicas. The response is recorded, or the key released, by a second statement
after the request. A claim whose request died between the two is taken over
once it is older than a **one-minute lease**, longer than any request here, so
a crash cannot lock a key for a day.

**Where it lives.** An `[Idempotent]` resource filter in the Api: it runs after
authorization, so an anonymous request claims nothing, and before model
binding, so it can hash the body and leave it for the action. The store is an
`IIdempotencyStore` in Application, implemented in Infrastructure on the
`idempotency_keys` table with SQL of its own.

## Consequences

A client can retry a registration after a timeout and get the original 201 and
`Location`. The key is opt-in, so no existing client is affected.

- The body hash is of the raw bytes: the same JSON with different whitespace is
  a different body and answers 422. Clients send the same bytes on a retry, so
  this is the strict and simple choice.
- A replay returns the stored representation even if the retry asks for another
  media type. The first answer is the answer.
- Rows hold a response body for a day. The registration response is an id; a
  larger response under a key would make the table larger in proportion.
- Only registration takes the header. Other writes are already idempotent (PUT
  with `If-Match`, role assignment that refuses a duplicate) or can opt in with
  the same attribute.
