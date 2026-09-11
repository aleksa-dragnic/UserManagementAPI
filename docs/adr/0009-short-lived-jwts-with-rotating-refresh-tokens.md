# 0009 — Short-lived JWTs with rotating refresh tokens and reuse detection

## Context

A bearer token is a credential anyone holding it can use. The longer it lives,
the longer a stolen one is useful, and a stateless JWT cannot be revoked before
it expires without keeping a denylist that defeats the point of statelessness.
A token that expires in minutes is safe to hold but forces a login every few
minutes, which nobody accepts.

## Decision

Two tokens with different jobs.

The **access token** is an HS256 JWT that lives fifteen minutes. It carries the
subject, the email, a unique id, and one `permission` claim per code the user
holds — no roles. It is validated statelessly on every request, with issuer,
audience, lifetime and signature checked and zero clock skew.

The **refresh token** is 256 random bits, opaque, and lives seven days. Only
its SHA-256 hash is stored, so a leaked table yields nothing usable. It is
exchanged for a new pair at `/auth/refresh`, and **every exchange rotates it**:
the presented token records the id of its replacement and is never valid again.

Rotation is what makes theft detectable. A token that has already been
exchanged and is presented a second time was either replayed by the legitimate
client, which does not happen, or stolen. On that signal every active token for
the user is revoked — the whole chain, not the one presented — and the request
is refused. The revocation runs on its own connection so it survives the
rollback of the refused request.

`RefreshToken` is its own aggregate, not part of `User`. It changes on every
exchange; inside `User` it would load and lock the entire user on every refresh.

## Consequences

A stolen access token is useful for at most fifteen minutes and cannot be
revoked; that is accepted in exchange for stateless validation. A stolen refresh
token is useful until the legitimate client next refreshes, at which point one
of the two presentations is a replay and both are cut off.

Refresh is a database write, so it is not free; at one per fifteen minutes per
active session that is nothing. Logout is a revocation of the presented token
and is idempotent.

Locked and deactivated accounts are refused at refresh as well as at login, so
locking a user ends their sessions within fifteen minutes without a denylist.

Because permissions are baked into the access token, a permission change also
takes up to fifteen minutes to reach a running session. That is the same
trade, stated the other way round.