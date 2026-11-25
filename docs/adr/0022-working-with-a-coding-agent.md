# 0022 — Working with a coding agent

- **Status:** Accepted
- **Date:** 2026-11-23
- **PR:** [#119](https://github.com/aleksa-dragnic/UserManagementAPI/pull/119)

## Context

This API is built with the help of an AI coding agent (Claude Code). The agent
writes code, tests and documents quickly, and is wrong in confident prose often
enough that its output cannot be taken on trust: a test can pass for the wrong
reason, a document can call something enforced that nothing enforces, a
dependency can arrive with a licence this repository already rejected.

Until now the repository did not say how that work is done. #92 removed the
agent instruction files that were there (`AGENTS.md`, `CLAUDE.md`) without a
replacement, so a reader could see neither the rules the agent works to nor
where its work ends and the maintainer's begins. Earlier commits do not mark
which changes an agent drafted.

## Decision

The agent drafts; the maintainer reviews, decides and answers for the code.

- **One process for every change.** Issue, plan, branch, the agent's draft, the
  maintainer's review, CI, merge. A pull request drafted by the agent passes
  the same CI, the same definition of done (build plan section 11) and the same
  review as any other. Nothing reaches `main` without a pull request.
- **Delegated:** implementation inside an agreed design, tests, refactoring
  with tests in place, documentation, dependency triage, CI diagnosis, and
  drafting the text of issues, pull requests and ADRs.
- **Not delegated:** architecture decisions and their ADRs, the design of
  anything security-sensitive (authentication, tokens, hashing, authorization,
  CORS, headers, rate limits), the assertions of tests in those areas, merges,
  releases, deleting anything, and every repository, cloud and secret setting.
- **Written down.** `AGENTS.md` holds the rules the agent works to, written from
  the code as it is; `CLAUDE.md` only points to it. `docs/AI-WORKFLOW.md`
  describes the process and how a change is verified.
- **Visible on every change.** A commit the agent wrote or co-wrote carries
  `Co-Authored-By: Claude <noreply@anthropic.com>`. Every pull request states
  its AI assistance (`none`, `drafted` or `reviewed-by-agent`) and what the
  maintainer verified by hand.

## Consequences

A reader can tell, for any change from here on, whether an agent drafted it and
what was checked by hand. The claim this repository makes is not that a person
typed every line, but that a person decided every design and is accountable
for every merge, and the record of that is in the pull requests.

The rules cost something. A draft is not merged until it has been read, and
security-sensitive work moves at the speed of a careful review, not of the
agent. A pull request without the AI assistance section is incomplete.

`AGENTS.md` can go stale like any document. When it and the code disagree, the
code wins and the file is corrected in the same pull request that notices it.

History before this ADR is left as it is: rewriting it to add trailers would
break every link to it, and this repository does not force-push `main`.
