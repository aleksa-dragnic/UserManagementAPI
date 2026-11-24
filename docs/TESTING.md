# Testing

380 test cases in 304 test methods, counted on 2026-11-24 from a local run of
the whole suite. A `[Theory]` with several `[InlineData]` rows counts once per row.

| Project | Methods | Cases | What it covers |
|---|---|---|---|
| `UserManagementAPI.Domain.UnitTests` | 88 | 100 | Every aggregate method on its allowed and its refused path, every value object, the seedwork |
| `UserManagementAPI.Application.UnitTests` | 85 | 96 | Handlers with faked repositories, the pipeline behaviors, validators, query composition, and the layer rules read from the compiled assemblies |
| `UserManagementAPI.IntegrationTests` | 131 | 184 | Mappings, migrations, generated SQL, event dispatch, the outbox, concurrency, and real HTTP over the whole stack — all against PostgreSQL |

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
  when a project references a layer it may not, or a controller depends on
  Infrastructure (ADR 0001). `ClockTests` reads the compiled method bodies of
  all four projects and fails when anything but the registration in
  `AddApplication` reads the system clock (#84). That only `Program.cs` and `Extensions/` touch
  Infrastructure is a review rule.

## Load test

`loadtests/users.js` runs k6 against the compose stack, never the demo, with
thresholds on p95, p99 and errors that fail the run (`loadtests/README.md`,
#116). Locally or through the **Load test** workflow, by hand only.

**First run, 2026-11-24.** Windows 11, Docker Desktop VM with 8 CPUs and
3.45 GiB, compose stack in Development, 130 seeded users. Two minutes each
unless the row says otherwise.

| Run | p50 | p95 | p99 | Throughput | Errors | Thresholds |
|---|---|---|---|---|---|---|
| capacity: reads, 20 readers, no pauses, 1 min | 23 ms | 55 ms | 80 ms | 736 req/s | 0 % | (none on latency) |
| the same, 2 min, an earlier run | 30 ms | 93 ms | 160 ms | 504 req/s | 0 % | met |
| writes alone, 5/s | 17 ms | 368 ms | 1.2 s | 15 req/s | 0 % | met |
| **mixed, 50 + 5 iterations/s** | reads 12 ms, writes 91 ms | reads 1.9 s, writes 5.4 s | reads 5.4 s, writes 12.5 s | 124 req/s | 0 % | **missed** |
| mixed, `DOTNET_gcServer=0` | reads 9 ms, writes 21 ms | reads 0.9 s, writes 2.3 s | reads 2.2 s, writes 5.1 s | 148 req/s | 0 % | missed |

Reads alone hold their thresholds at four to six times the mixed run's rate;
two runs of the same capacity test reached 504 and 736 req/s, so on this
machine capacity varies by a third between runs. Together
with registrations, the tail goes to seconds while the median stays low, so
requests queue behind something rather than being uniformly slow. The working
hypotheses are GC pauses from Argon2id's large allocations and thread-pool
starvation from hashing synchronously on request threads; neither is proven.
Tracked as #128, which also asks for the same run on the GitHub runner.
