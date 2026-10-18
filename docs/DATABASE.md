# Database

PostgreSQL 17 on Neon for a deployed instance, PostgreSQL 17 in a container
locally and in tests. The schema is owned by EF Core migrations; the full
column list is in [`architecture.md`](architecture.md#schema).

## Tables

| Table | Holds | Notes |
|---|---|---|
| `users` | The User aggregate's root row | `email` is `citext` with a unique index, so case does not make a second account; `xmin` is its concurrency token and the `version` clients send back (ADR 0021) |
| `user_roles` | The roles a user holds | Part of the User aggregate; unique per user and role |
| `roles`, `role_permissions`, `permissions` | Roles and what they grant | Seeded by `DatabaseSeeder`, not by migrations |
| `refresh_tokens` | Refresh tokens, as SHA-256 hashes only | Its own aggregate; `xmin` is its concurrency token (ADR 0009) |
| `outbox_messages` | Events waiting to leave the service | Written in the same transaction as the change (ADR 0008) |
| `audit_log` | Who changed which entity, and which fields | Append-only, see below |

## Indexes

Every uniqueness rule has an index that enforces it: `ix_users_email`,
`ix_roles_name`, `ix_permissions_code`, `ix_refresh_tokens_token_hash`,
`ix_user_roles_user_id_role_id` and `ix_role_permissions_role_id_permission_id`.
A violation of `ix_users_email` is reported as `User.EmailNotUnique` (409), the
same error the handlers' own check returns.

The others serve a lookup: `ix_refresh_tokens_user_id` (revoking every token of
a user), `ix_user_roles_role_id`, `ix_role_permissions_permission_id`,
`ix_audit_log_occurred_at_utc`, `ix_audit_log_entity_entity_id`, and the partial
`ix_outbox_messages_pending` on rows where `processed_on_utc IS NULL`.

The user search is a scan by design: `lower(unaccent(...))` cannot use an index,
and at this size a scan is the right plan (ADR 0020).

## The audit log is append-only

The `AddHardening` migration installs triggers that refuse `UPDATE` and `DELETE`
on `audit_log` for every role, the table's owner included. The application
connects as the owner, so revoking rights would not have helped. `TRUNCATE` is
deliberately not blocked, so the integration tests can reset the database, and
whoever owns the database can drop the triggers. The log records the names of
the fields that changed, never their values.

## Migrations

Eight, in order: `InitialSchema`, `DomainGeneratedKeys`, `AddOutbox`,
`AddRefreshTokens`, `AddAuditLog`, `AddHardening`, `AddUnaccent`,
`AddUserVersion`. `AddUserVersion` changes nothing in the database: `xmin` is a
system column every row already has, and the migration only records it in the
model snapshot.

- **Locally and in the compose stack** the API applies them on startup
  (`Database:MigrateOnStartup` is on in Development).
- **In a deployed instance** they are a deliberate step. They are applied from a
  workstation with `dotnet ef database update`, against the **direct** Neon
  connection string (`ConnectionStrings:Migrations`), before deploying code that
  needs the new schema. The service does not migrate on startup.
- The design-time factory reads `ConnectionStrings:Migrations` because the
  pooled endpoint runs in transaction mode and breaks the session-level steps a
  migration takes (ADR 0002).
- `AddUnaccent` creates the `unaccent` extension. It has to be applied before
  code that searches with it is deployed (ADR 0020).

## Neon

One project with two branches: `main` for the deployed instance and `dev` for
local work. A branch is copy-on-write, so the second one costs nothing and keeps
an experimental migration away from the demo. The application uses the pooled
endpoint. Neon suspends idle compute, so `EnableRetryOnFailure` is on and the
transaction behavior runs inside the execution strategy (ADR 0002).

## Seeding

`DatabaseSeeder` creates the permissions, the Administrator and Member roles,
the bootstrap administrator and, where a password is configured, the read-only
demo account. It is idempotent and runs when `Database:SeedOnStartup` is on.
`seed-demo-directory` adds 130 synthetic users, once and by hand (README).
