# 0020 — Accent-insensitive search through unaccent

## Context

The user search matched a lowercased term against the email, the first name and
the last name, each on its own. Two things followed from that.

The names this service holds are mostly Serbian, written with č, ć, š, ž and
đ. Someone typing on an English keyboard — most people who try the deployed
instance — searches for "petrovic", and that found no Petrović. Search is the
first thing anyone does on a directory screen.

Each field was compared separately, so "ana petrović", which is how the name
reads on screen, found no one either.

Filter, search and sort live in the Application as plain LINQ over
`IQueryable<User>`, unit-tested in memory and composed into one SQL query by the
handler in Infrastructure ([ADR 0016](0016-query-handlers-in-infrastructure.md)).
PostgreSQL's `unaccent` is a database function the Application cannot name.

## Decision

The search compares folded text: lower case, diacritics removed. The term is
folded once in C#; the stored name is folded by the database.

`SearchText.Fold(string)` in the Application is the rule. Its C# body drops
combining marks after Unicode decomposition and maps đ, which has no
decomposition, to d. `AppDbContext` registers the same method as a database
function translated to `lower(unaccent(...))`, so in a query against the
database the folding happens in SQL on the column, and in memory the C# body
runs. The rule stays in the Application and stays unit-testable; the handler is
unchanged.

The name is matched as one string, first and last joined by a space, so a term
can span both. The email is compared as stored, lowercase.

The `unaccent` extension is declared in the model next to `citext` and created
by the migration `AddUnaccent`.

## Consequences

"petrovic", "PETROVIĆ" and "ana petrović" all find Ana Petrović. "dordevic"
finds Đorđević. "djordjevic" does not: unaccent maps Đ to D, and the Serbian
convention of writing đ as dj when no đ is available is a transliteration rule,
not an accent. Supporting it would mean a second folding with its own rules,
for one letter.

The C# fold and unaccent agree on the letters the data holds. unaccent's rule
file covers more (ß, æ, ø and others); for those, an in-memory test and the
database could disagree. The integration tests run the database half.

The search scans: `unaccent` is not immutable, so it cannot back an expression
index directly, and a substring match cannot use a B-tree index anyway. At this
service's size a sequential scan is the right plan. If it ever is not, the
usual answer is an immutable wrapper function and a trigram index on its
result.

The migration must reach a database before code that searches it. Where the
schema is migrated by hand rather than on startup, it is applied first; the
extension is unused by older code, so applying it early is safe.