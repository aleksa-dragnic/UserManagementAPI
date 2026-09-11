# 0015 — Multi-tenancy and OIDC explicitly out of scope

## Context

Two features sit next to a user-management API and get asked about first.

**OpenID Connect.** Letting users sign in with Google or a corporate identity
provider, or making this API an identity provider for other applications.

**Multi-tenancy.** Serving several organisations from one deployment, each
seeing only its own users, roles and permissions.

Both are real, both are common, and both are the kind of thing that, added
"while we are here", turns a finished project into an unfinished one.

## Decision

Neither is in scope for v1.0.

The API issues its own credentials: Argon2id passwords, HS256 access tokens,
rotating refresh tokens (ADR 0009). It is not an OAuth 2.1 authorization server
and does not act as an OIDC relying party. Implementing either correctly means
implementing a specification — discovery, JWKS, PKCE, consent, token
introspection — or taking a dependency on a framework that does, and either way
the identity flows would dwarf the user-management domain they were meant to
serve.

Every table is single-tenant. There is no `tenant_id`, no row-level filter, no
tenant resolution from the request. Adding it later touches every aggregate,
every query, every index and every test; adding it now, speculatively, would
cost the same and be exercised by nothing.

## Consequences

The domain stays small enough to model properly, which was the point of the
project. Authentication is complete for what it claims to do and is honest
about what it does not.

Both features are recorded as GitHub issues labelled `future`, with this ADR
linked, so a reader sees a decision rather than an omission. If either is ever
needed, the boundaries are in place: OIDC would replace `JwtTokenService` and
the login handler behind `ITokenService`, and multi-tenancy would start at the
aggregates and radiate outward through the configurations and the read path.

Recording what was deliberately excluded, and why, reads as judgement. Scope
that grows without a record reads as drift.