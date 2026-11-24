# Roadmap

What this API deliberately does not do yet, and what each would take. These
were open GitHub issues labelled `future`; they were moved here on 2026-11-24 so
the issue tracker holds only work in progress, and each idea keeps its reasoning
and its link to the issue where it was first written down. None of them is
planned. Starting one begins with an issue and, where it needs a decision, an
ADR (`docs/AI-WORKFLOW.md`).

## Identity

| Idea | What it would take | Recorded in |
|---|---|---|
| **Email verification and deactivation endpoints** | The domain has both transitions (`User.VerifyEmail()`, `User.Deactivate()`) with their errors and events; the HTTP surface exposes neither. Verification needs a token sent by email, so an email provider and delivery through the outbox (ADR 0008). Deactivation is one endpoint, but it first needs a decision on what happens to the user's refresh tokens and audit trail. | #80 |
| **Asymmetric token signing and a JWKS endpoint** | Tokens are signed with HMAC-SHA256 and one shared key, so every service that validates a token could also issue one. RS256 or ES256 with a published JWKS would let another service validate without that power and rotate keys by `kid`. ADR 0015 groups JWKS with OpenID Connect; this would separate the two. The first consumer would be a second project that trusts these tokens. | #81 |
| **OpenID Connect** | Out of scope by ADR 0015. Either as a relying party or as an authorization server it means implementing a specification (discovery, JWKS, PKCE, consent) or depending on a framework that does, and the identity flows would outgrow the user-management domain. | #52 |

## Platform

| Idea | What it would take | Recorded in |
|---|---|---|
| **Multi-tenancy** | Out of scope by ADR 0015. A tenant id on every aggregate, tenant resolution on every request, and a tenant filter in every query, index and test: the whole codebase, for a requirement nobody has. | #51 |
| **Webhooks** | Events that leave the service already go through the outbox (ADR 0008). Webhooks add subscriber registration, signed deliveries, retries with backoff and a delivery log per subscriber on top of it. | #53 |
| **Publishing the container image** | The image is already scanned by Trivy and carries a CycloneDX SBOM in CI (#123); Render builds from the Dockerfile itself, so nothing consumes a published image. Publishing to GHCR waits for a consumer. | #83 |

## The maintainer's, outside the code

| What | Where it stands |
|---|---|
| **A timed restore drill on Neon** | The procedure is in `docs/RUNBOOK.md`; the first drill, which sets the RTO, needs the Neon account. Its result goes in the runbook's drill log (#117). |
| **The load test on a small machine** | On the GitHub runner every threshold of `loadtests/` is met; on a 3.45 GiB Docker Desktop VM the mixed run misses its p99 by seconds. Naming the resource the small VM runs out of is optional (#128). |
