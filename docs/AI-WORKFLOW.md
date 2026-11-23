# Working with a coding agent

How changes to this repository are made with an AI coding agent (Claude Code),
what is delegated to it and what is not, and how its work is checked. The
decision is [ADR 0022](adr/0022-working-with-a-coding-agent.md); the rules the
agent itself reads are in [`AGENTS.md`](../AGENTS.md).

## The flow

```
issue  ->  plan  ->  branch  ->  agent draft  ->  review  ->  CI  ->  merge
```

1. **Issue.** Every unit of work starts as an issue with a "Done when" list,
   in a milestone. Deferred ideas stay open with the `future` label.
2. **Plan.** Before any code, the agent reads the issue, the ADRs it touches and
   the code, and writes a short plan: the files, the approach, the tests, what
   is left out. The maintainer accepts it, changes it or rejects it. A plan that
   needs a new decision stops here until the decision is made, as an ADR.
3. **Branch.** One branch per issue, named as in build plan section 4.2.
4. **Agent draft.** The agent implements the plan with its tests, runs the
   build and the suites, and opens the pull request with the template filled
   in, including what it ran and what it could not.
5. **Review.** The maintainer reads the diff on GitHub: the code, the tests'
   assertions, the documents it changes. Questions and corrections go back to
   the agent on the same branch.
6. **CI.** Build with warnings as errors, all three test projects against
   PostgreSQL, the migration script, the container build and CodeQL. Red is
   fixed, not retried until green.
7. **Merge.** The maintainer squash-merges. A release, a tag, a deploy and a
   migration against the deployed database are the maintainer's alone.

## What is delegated and what is not

| Delegated to the agent | Kept by the maintainer |
|---|---|
| Implementation inside a design that is already decided | Architecture decisions, and accepting their ADRs |
| Unit and integration tests | The assertions of tests on authentication, tokens and permissions |
| Refactoring where tests already cover the behaviour | Anything security-sensitive: authentication, token handling, hashing, authorization policies, CORS, headers, rate limits |
| Documentation, kept true to the code | Merging, releasing, tagging, deploying, migrating the deployed database |
| Dependency triage: reading changelogs, running the suites on an update | Deleting branches, closing issues and pull requests |
| CI diagnosis and proposed fixes | Repository, Render, Neon and Codecov settings, and every secret |
| Drafting issues, pull request descriptions and ADR text | Deciding what an issue, a pull request or an ADR says in the end |

The agent can draft in any area. It can change the kept ones only from a plan
the maintainer wrote, and only in a pull request the maintainer reads line by
line.

## How a change is verified

The agent's report is where checking starts, not where it ends.

- **The agent runs, and shows.** Every pull request lists the commands it ran
  and their results. A suite that could not run (no Docker, a sleeping
  instance) is named.
- **CI runs the same checks again** on a clean runner. A green local run on one
  machine is not taken as proof.
- **The maintainer reads the tests first.** A test is checked for what it
  asserts, not for whether it passes: that it would fail without the change,
  and that it tests behaviour rather than the implementation.
- **Claims are checked against the code.** A document that says "enforced" or
  "always" has to point to the test, lint rule or CI step that makes it so.
- **The pull request records it.** The template's AI assistance section says
  how the change was made, and "What I verified myself" lists what the
  maintainer checked by hand: a request in Scalar, a migration read as SQL, a
  log line, a failure provoked on purpose.

## Marking the work

- A commit the agent wrote or co-wrote ends with
  `Co-Authored-By: Claude <noreply@anthropic.com>`.
- The pull request's AI assistance is one of:
  - `none`: written without an agent
  - `drafted`: the agent wrote a first version, the maintainer reviewed and
    changed it
  - `reviewed-by-agent`: the maintainer wrote it and an agent reviewed it
- Commits before ADR 0022 do not carry the trailer; the history is not
  rewritten to add it.
