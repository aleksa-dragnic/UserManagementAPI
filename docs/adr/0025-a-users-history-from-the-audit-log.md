# 0025 — A user's history is read from the audit log, by field name only

- **Status:** Accepted
- **Date:** 2026-11-24
- **Issue:** [#133](https://github.com/aleksa-dragnic/UserManagementAPI/issues/133)

## Context

The admin console (Umapi-Console) shows a user's detail but not how it came to
be. The API already keeps the answer: every write to a user goes into the
append-only `audit_log` with its actor, the time, and the names of the fields it
changed, never their values. Nothing reads it over HTTP.

Two gaps stood in the way. A role assignment was recorded under the
assignment's own id with no fields, so once the role was removed and its
`user_roles` row deleted, the log could not say whose role it had been or which.
And EF Core replaces an owned value object whole, so a new last name was also
recorded as a new first name.

## Decision

The maintainer decided the four questions on 2026-11-24:

- **Endpoint.** `GET /api/v1/users/{id}/history`: the user's changes, newest
  first, at most a hundred, under the same conditional GET as the rest of the
  API; `User.NotFound` for an unknown id.
- **Permission: `users.read`**, the same as reading the user. The history adds
  no value the reader cannot already see, so it needs no permission of its own,
  and the read-only demo account sees it.
- **Actor by email.** Whoever can read the directory already reads every
  email. A system write - the seeder - has no actor.
- **Field names only.** The log keeps what it always kept. Values stay out: a
  value can be a password hash, and an email history is a record of personal
  data the API has no reason to hold.
- **Role rows name their user and role.** A `UserRole` row's payload adds
  `userId` and `roleId`. They are references, not values of a field, and not
  personal data; the role's name is read at request time.

The response names each change: `registered`, `updated` with its `fields`
(`email`, `firstName`, `lastName`, `password`, `status`), `roleAssigned` and
`roleRemoved` with the role. An update whose only fields are bookkeeping
(`UpdatedAtUtc`) is the user's side of a role change and is not reported twice.
A replaced owned value object now records only the fields whose value moved.

## Consequences

- An administrator, and the demo account, can see who changed a user and when,
  without the API storing anything it did not store before.
- **Older role rows.** A role assignment written before this change is tied to
  its user through `user_roles` while the role is still held. A removal from
  before then cannot be tied to anyone and is not reported.
- **What is not said.** "Status changed" does not say from what to what: the
  value is not kept. A console that wants "locked" or "unlocked" would need the
  status values in the log, which is a change to this decision.
- **A hundred entries.** Enough for any user of a demo; a user with a longer
  history would need paging, the first thing to add if one appears.
- The `UserRole` rows are found by a jsonb containment over `audit_log`, which
  has no index for it. At the size of this log that is a scan of a small table;
  an expression index on `payload->>'userId'` is the change if it grows.

## Alternatives considered

- **The outbox.** Only registration and locking raise domain events, and
  processed messages are plumbing, not a record. The audit log already records
  every write.
- **A permission of its own (`audit.read`).** More restrictive, but it would
  hide a history that reveals nothing `users.read` does not, and the demo
  account could not show the feature.
- **Old and new values.** The panel could then say "from A to B", at the cost of
  a store of personal data and a password-hash history to defend. Rejected.
