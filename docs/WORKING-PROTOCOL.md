# Working protocol — implementation packs via dry-run scripts

Project-agnostic. Pair it with a project-specific handoff (repo tree, decisions
made so far, pins, secrets locations). This file says HOW we work; the handoff
says WHAT the repo currently is.

---

## 1. Roles

- **Aleksa** owns the repo, runs every command, commits, opens PRs, merges, tags.
  Communicates in Serbian. Wants direct, no-filler answers.
- **Claude** reads the pack and the repo state first, then produces one
  PowerShell script per PR (or step group), reads the pasted output, and says
  what's next. Claude does not run git or dotnet; Claude does not redesign the
  pack; Claude does not produce code before being asked.

## 2. Language

- Prose to Aleksa: **Serbian**.
- Everything that lands in the repo or a tool: **English** — code, comments,
  commit messages, PR descriptions, ADRs, script output, file names.

## 3. Flow for every PR

1. Aleksa uploads the pack (zip) or pastes a step. Claude reads **all** of it
   and the relevant existing files (tree, csproj, Program.cs, fixtures) before
   answering. Reading is not narrated; findings are.
2. Claude reports only concrete problems: won't compile, test will fail,
   contradicts the current repo, security/secrets. For each: minimal fix applied
   and listed, or a decision requested when it is a design choice. If Aleksa
   says "uradi kako misliš", Claude decides and explains in 2–4 sentences.
3. Claude delivers `apply-<pr>.ps1` as a file plus two commands:
   ```
   powershell -ExecutionPolicy Bypass -File .\apply-<pr>.ps1 -DryRun
   powershell -ExecutionPolicy Bypass -File .\apply-<pr>.ps1
   ```
4. Aleksa runs dry run, pastes output. Claude confirms or fixes. Aleksa runs
   execute, pastes output.
5. Manual steps the script cannot do (database migrations against a live DB,
   browser consoles, compose verification) are printed by the script at the end
   as a numbered checklist with expected results. Aleksa pastes results.
6. Claude gives: delete-script line, explicit `git add <paths>`, commit command
   (pack subject + why-body), push, PR title + full What/Why/Notes description
   with every deviation as a Notes bullet, then the post-merge cleanup commands
   and the milestone tag when applicable.
7. Aleksa merges (squash), pulls, deletes branch, tags. Pastes the pull output.
   Claude sanity-checks the diff stat (whole-file diffs = line endings, etc.).

## 4. Script contract

- `[CmdletBinding()] param([switch]$DryRun)`; `$ErrorActionPreference='Stop'`.
- Colour-coded helpers: Plan (yellow, what would happen), Done (green),
  Warn (red). Dry run and execute print the same plan; only execute writes.
- **Pre-flight**: sln present; on `main` and in sync with `origin/main` (fetch +
  `rev-list --count main..origin/main`) before creating `feat/...`, or already
  on the target branch; working tree clean apart from `apply-*.ps1`; previous
  PR's artefacts present; external services reachable when needed (Docker).
- **Packages**: `dotnet add ... package X` only for packages not yet pinned in
  `Directory.Packages.props`; already-pinned ones get a bare `PackageReference`
  written into the csproj (CPM `dotnet add` can bump pins).
- **Files**: complete contents in single-quoted here-strings, keyed by
  repo-relative path. UNCHANGED / CREATE / WARN-overwrite semantics.
- **Edits**: exact anchor text, must exist exactly once, else "edit by hand";
  preserve the file's newline style; absolute paths for .NET file APIs.
- `if ($DryRun) { exit 0 }` before any write.
- **Verify**: architecture invariants as greps (e.g. Domain has no
  `PackageReference`, no public setters on aggregates, no secrets in tracked
  files, layer boundaries), `dotnet build-server shutdown`, build, test with the
  expected test count stated in chat, `git status --short`, next-steps block.
- **Encoding**: script file UTF-8 **with BOM**, plain hyphens outside
  here-strings (Windows PowerShell 5.1). Source files UTF-8 without BOM, CRLF.
- Helper scripts are never committed: delete before `git add`, and stage
  explicit paths instead of `.`.

## 5. Known PowerShell 5.1 / Windows traps (check every script against these)

- BOM-less UTF-8 .ps1 → parser errors on em dashes and non-ASCII.
- `Select-String` on a missing path throws even with `-ErrorAction`; `Test-Path` first.
- Native stderr (`git`, `dotnet`, `docker`) becomes a terminating error under
  `Stop`; wrap in `Continue` when capturing output.
- `[System.IO.File]::*` relative paths resolve against the process cwd, not
  `Get-Location` — always `Join-Path (Get-Location) ...`.
- `2>$null` on native commands still surfaces as an error record.
- psql via `docker compose exec`: SQL in single quotes; inner double quotes are
  stripped — avoid quoted identifiers.
- A stale compiler/MSBuild server can hang `dotnet build` indefinitely —
  `dotnet build-server shutdown` before every build.
- `git add .` twice for two commits: the first takes everything, the second is
  empty. Stage per commit explicitly.
- GitHub truncates PR titles longer than ~70 chars into the body — set the full
  subject by hand.
- A failed `git pull` (SSH passphrase) leaves `main` stale; branching from it
  silently misses the last merge. Pre-flight catches it.

## 6. Test conventions

- Assert behaviour, not type: a read-only wrapper IS an `ICollection<T>`; test
  `IsReadOnly` and that `Add` throws.
- One container per test run via a collection fixture; truncate between tests;
  never the EF in-memory provider.
- Theory rows count individually in `dotnet test` totals — state the expected
  number.
- A static helper named like a type shadows the type inside the class —
  fully qualify.
- Every pack test is run as written unless it cannot pass; the change and the
  reason go into the PR Notes.

## 7. When a pack conflicts with the repo

Precedence: current repo state > BUILD-PLAN.md > milestone guide > pack. The
pack is a proposal generated from the guides; the repo is what actually merged.
Typical conflicts: an interface the pack creates already exists; a schema
detail differs (keys, FKs); a package API changed since the pack was written.
Resolve minimally, record in PR Notes and in the project handoff table.

## 8. Handoff hygiene

At the end of each milestone, update the project handoff: tags, new pins, new
decisions/deviations, test counts, open items (e.g. ADRs promised but not
written), and anything that moved between layers. That table is what the next
session reads first.

## 9. Producing the next pack

The pack for milestone N is generated by a separate AI from the project files.
Its prompt must include: the pack format (README + PRnn/INSTRUCTIONS.md + files/
mirroring the repo root, complete files), the current repo state and every
deviation from the guides, the pins and toolchain constraints, the test
conventions above, and a request for a deviation log, open questions, and
expected test counts per PR. Claude reads that pack in full before writing the
first script.
