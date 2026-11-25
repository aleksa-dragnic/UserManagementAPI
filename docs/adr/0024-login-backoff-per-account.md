# 0024 — A growing delay per email slows password guessing, without a lockout

- **Status:** Accepted
- **Date:** 2026-11-24
- **PR:** [#131](https://github.com/aleksa-dragnic/UserManagementAPI/pull/131)

## Context

Login is rate limited per caller address: ten attempts a minute
(`RateLimiting:Auth`). An attacker who spreads attempts over many addresses can
still try passwords against one account at that rate per address, and the
address limit never trips. #82 recorded this as not yet worth the state; it is
part of the production-hardening milestone now.

Two things such a defence must not become: a way to tell which emails are
registered, and a way for anyone to lock a real user out of their account.

## Decision

Wrong passwords in a row are counted **per email**, in PostgreSQL, and after
enough of them sign-in for that email is refused for a while.

- **Threshold and delay:** the fifth wrong password in a row refuses the email
  for **one second**; each further one doubles it - two, four, eight - up to
  **fifteen minutes**. A refusal never shortens one already in place.
- **While refused:** login answers **429** with `Retry-After` and the error code
  `Auth.TooManyFailedAttempts`, the same shape as the rate limiter's 429. The
  right password gets the same answer, and no password is checked: the
  refusal reveals nothing about the account.
- **Known and unknown emails alike.** The row is keyed by a SHA-256 of the
  trimmed, lower-cased email, not by a user id, so an unregistered email is
  throttled exactly like a registered one and the table holds no addresses.
- **Not a lockout.** The refusal ends by itself, after fifteen minutes at the
  very most. An attacker can slow a real user's sign-in; they cannot lock the
  account, and there is nothing for the owner to unlock.
- **Forgetting.** A successful sign-in deletes the row. A run whose last failure
  is a day old counts from one again, and an hourly cleanup deletes such rows,
  which bounds a table that unknown emails would otherwise grow.
- **A locked or deactivated user with the right password** is neither counted
  nor forgotten: it was no guess, and no success.

**How it is safe under concurrency.** The count is one
`INSERT … ON CONFLICT (email_hash) DO UPDATE … RETURNING`, so two failures at
once count twice on any number of replicas. Every throttle statement runs on a
context of its own and commits at once: a refused login returns a failed
Result, and the transaction behavior rolls the request's own work back, which
must not take the count with it (the same reason refresh-token revocation uses
its own context, ADR 0009).

**Where it lives.** `ILoginThrottle` in Application, `LoginThrottle` in
Infrastructure on the `login_failures` table. The login handler checks it
before looking at the password, counts a wrong password or an unknown email,
and forgets on success. A new `ErrorType.TooManyRequests` maps to 429, and a
`RateLimitedError` carries the wait the Api writes into `Retry-After`. Time
comes from `TimeProvider` (#84).

## Consequences

Guessing one account's password from many addresses goes from ten tries a
minute per address to a handful before the delays dominate: past the
threshold, the n-th further try waits 2^n seconds, and from the fifteenth on,
fifteen minutes each.

- A real user who mistypes five times waits a second, then two. That is the
  cost, and it is small.
- An attacker who knows a user's email can keep that user's sign-in slow, by
  failing for it every few minutes. They cannot stop it; the address rate limit
  bounds how often they can fail.
- The console already handles a 429 on sign-in with a countdown from
  `Retry-After` (its inventory, section 2.7), so it needs no change.
- Each failed or throttled login costs one or two small statements on their own
  connection.

## Alternatives considered

### A lockout after N failures

The usual answer, and the one that turns the defence into the attack: anyone
who knows an email can lock its owner out. Rejected.

### Counting on the User aggregate

Would keep the state with the user, but unknown emails would not be counted,
and the difference in behaviour - throttled or not - would tell an attacker
which emails are registered. Rejected.

### A CAPTCHA after N failures

Stops scripts, not people; needs a third-party service and a console flow. Out
of scope for an API with no public sign-up.
