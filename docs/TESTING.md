# Testing

322 test cases in 290 test methods, counted on 2026-10-18 from the CI run on
`main`. A `[Theory]` with several `[InlineData]` rows counts once per row.

| Project | Methods | Cases | What it covers |
|---|---|---|---|
| `UserManagementAPI.Domain.UnitTests` | 85 | 97 | Every aggregate method on its allowed and its refused path, every value object, the seedwork |
| `UserManagementAPI.Application.UnitTests` | 83 | 94 | Handlers with faked repositories, the pipeline behaviors, validators, query composition, and the layer rules read from the compiled assemblies |
| `UserManagementAPI.IntegrationTests` | 122 | 131 | Mappings, migrations, generated SQL, event dispatch, the outbox, concurrency, and real HTTP over the whole stack — all against PostgreSQL |

## Running them

```bash
# Everything; Docker must be running for the integration tests.
dotnet test UserManagementAPI.sln --configuration Release

# Without Docker: the two unit test projects only.
dotnet test tests/UserManagementAPI.Domain.UnitTests --configuration Release
dotnet test tests/UserManagementAPI.Application.UnitTests --configuration Release
```

The integration tests start `postgres:17-alpine` with Testcontainers once per
run and reset it between tests, so no connection string is needed. CI runs all
three projects on every pull request and every push to `main`.

## Rules

- **No in-memory database provider.** It enforces no constraint and translates
  no SQL the way PostgreSQL does; a test that passes against it can fail in
  production (ADR 0012).
- **A bug fix starts with a failing test** that reproduces it.
- **Test names read as sentences**: `Register_StartsTheUserWithTheGivenRole`,
  `ConcurrentRegistrations_OfOneEmail_CreateOneUser_AndRefuseTheRestWith409`.
- **One concept per test.** Arrange, act and assert, separated by blank lines.
- **Coverage is a diagnostic, not a target.** It is uploaded to Codecov from
  CI; an untested branch is worth a test, a tested auto-property is not.
- **FluentAssertions stays on 7.x**: version 8 changed its licence (build plan
  section 14).

## What each level is for

- **Domain** tests need no mocks: the domain depends on nothing.
- **Application** tests prove orchestration with NSubstitute fakes. They do not
  prove that anything reaches the database.
- **Integration** tests prove it does. The persistence tests use a real
  `AppDbContext`; the functional tests send real HTTP through
  `WebApplicationFactory`, including authentication, authorization, rate
  limits, conditional GET, versioning and hypermedia.
- **Architecture** tests (`LayeringTests`, `ApiLayeringTests`) fail the build
  when a project reaches through a layer (ADR 0001).
