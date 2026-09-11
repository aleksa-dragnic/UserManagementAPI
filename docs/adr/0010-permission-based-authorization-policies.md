# 0010 — Permission-based authorization policies rather than role checks

## Context

Every mutating endpoint needs a gate. The habitual gate is a role check:
`[Authorize(Roles = "Administrator")]`. It reads well and it is wrong in a way
that only shows later: the code now encodes an organisational decision. When a
second role should also be allowed to lock users, the answer is a redeploy.
When a role is renamed, every attribute that named it is a bug.

## Decision

A gate names a **capability**, not a group: `[HasPermission("users.lock")]`.
Which roles hold `users.lock` is data in `role_permissions`, changed with an
`UPDATE`, never with a deploy. Roles remain, as containers that make granting
permissions to people manageable; nothing in code ever tests a role name.

At login the user's permission codes are resolved through their roles in one
query and written into the access token as `permission` claims. The policy
handler checks the claim. No lookup per request, no coupling to the database on
the hot path, and the check works identically on any replica.

Policies are built on demand by a custom `IAuthorizationPolicyProvider` from
the code in the attribute, so adding a permission is adding a constant, not
registering a policy. The codes live in the Domain (`PermissionCodes`) because
three layers read them — seed data, token issuance, policies — and a typo
between any two is an endpoint nobody can reach.

There is no bare `[Authorize]` in the project. `[Authorize]` says "someone";
every protected action says who.

## Consequences

Access changes are configuration. Auditing what a role can do is one query.

Permissions are snapshotted into the token, so a change reaches a live session
on its next refresh — within fifteen minutes. That is the same trade ADR 0009
makes for revocation, and it is accepted for the same reason.

The token grows by one claim per permission. With five codes that is nothing;
with five hundred it would be worth revisiting, and the answer then would be a
per-request lookup behind the same handler, not a return to roles.