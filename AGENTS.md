# Instructions for coding agents

This repository is maintained with the help of AI coding agents (Claude Code).
The owner reviews every change. These instructions apply to any agent working
here; they restate rules the repository already follows.

## What this is

A .NET 10 REST API for users, roles and permissions: four projects with
dependencies pointing inward (Domain ← Application ← Infrastructure, Api),
CQRS through a hand-written dispatcher, EF Core on PostgreSQL, and a deployed
demo instance. Start with `README.md`, `docs/architecture.md` and
`docs/adr/README.md`.

## Commands

```bash
dotnet build UserManagementAPI.sln -c Release      # warnings are errors
dotnet test tests/UserManagementAPI.Domain.UnitTests -c Release
dotnet test tests/UserManagementAPI.Application.UnitTests -c Release
dotnet test tests/UserManagementAPI.IntegrationTests -c Release   # needs Docker
docker compose up -d --build                        # API on :8080, PostgreSQL, Grafana
```

## Rules

- **English only**: code, comments, test names, commit messages, pull requests
  and documentation.
- **Follow `docs/BUILD-PLAN.md`**: Conventional Commits with the scopes in
  section 4.3; one branch and one pull request per unit of work; pull request
  description in three parts, What / Why / Notes; squash merge.
- **Definition of done** is section 11 of the build plan. A change is not done
  until the build has no warnings and the tests that cover it pass in CI.
- **A bug fix starts with a failing test.** Integration tests run against real
  PostgreSQL through Testcontainers; never add the EF in-memory provider.
- **Business rules live in the domain.** Handlers orchestrate. The two
  exceptions are recorded in `docs/architecture.md`.
- **Decisions are recorded.** A decision gets an ADR in the pull request that
  implements it; a departure from the plan gets a row in build plan section 14.
- **Documents must stay true.** Change the README, `docs/` or an ADR in the same
  pull request as the behaviour it describes. Nothing is called enforced unless
  a test, an analyzer or CI enforces it.
- **The scope is closed.** New ideas become issues labelled `future`
  (section 13); they are not built in passing.

## Do not

- Commit secrets, connection strings or keys. Configuration values that are
  secret live in user secrets or environment variables.
- Change `Directory.Packages.props` versions outside a dependency update, or
  move FluentAssertions to 8.x (licence, section 14).
- Edit an applied migration. Add a new one.
- Rewrite history, force-push `main`, or push to `main` without a pull request.
- Close or delete `future` issues.
