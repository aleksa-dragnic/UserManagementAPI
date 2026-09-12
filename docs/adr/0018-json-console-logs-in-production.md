# 0018 — JSON console logs in production

## Context

CodeQL reported two medium-severity log-forging findings in
`GlobalExceptionHandler`, where the request method and path are written to the
log. The method is not a vector: Kestrel rejects a request line whose method
token contains CR or LF. The path is one. ASP.NET Core percent-decodes the path
before it reaches `HttpContext.Request.Path`, so a request for

```
GET /users%0A2026-09-12%20INF%20Administrator%20signed%20in
```

arrives with a genuine newline inside the value. Rendered through a plain-text
output template, that newline ends the line and everything after it reads as a
separate log record — one an operator has no way to distinguish from a real one.

The obvious fix is to strip control characters from the two values the query
pointed at. It would close those two call sites and silence the tool, which is
the problem with it: `UseSerilogRequestLogging` writes `RequestPath` for every
request that reaches the application, not only the ones that threw. That call
lives in a library, CodeQL does not report it, and sanitising our own two
statements would leave the larger surface untouched while the alert count went
to zero.

## Decision

In production the console sink renders each event with
`CompactJsonFormatter` instead of an output template. Development — which
covers a workstation, the compose stack and the functional test host — keeps
the readable template.

The sink moves from `appsettings.json` into the `UseSerilog` callback, so the
choice cannot be lost by a configuration file that fails to reach a published
image. Levels and per-source overrides stay in configuration, where they
belong.

## Consequences

Log forging stops being possible rather than being filtered. A newline inside a
JSON string is two characters, `\n`, and cannot begin a new record whatever the
value contains. This holds for every sink write, including the ones inside
Serilog's request logging middleware, and for any log statement added later by
someone who has not read this file.

No new dependency. `Serilog.Formatting.Compact` already arrives as a dependency
of `Serilog.AspNetCore`, so there is no package to add and no version to pin
under central package management.

Production logs stop being pleasant to read by eye. On a platform log viewer
each line becomes a JSON object, which is worse for skimming and better for
everything else: `RequestPath`, `StatusCode`, `Elapsed` and the correlation id
become fields to filter on instead of text to grep. The trade is deliberate and
it is why development was left alone.

The CodeQL findings may stay open. The query looks for user input reaching a
log call, and it does reach one — what changed is what the sink does with it.
If they persist they are dismissed with a reference to this record, which is
honest: the class is closed, the data flow the query describes still exists.