# 0014 — Mapperly for compile-time mapping

## Context

Keeping HTTP contracts separate from commands (ADR 0011) means mapping between
them on every request. Written by hand it is boilerplate that drifts — a field
added to the contract and forgotten in the mapper is a silent `null`. The usual
library answer is a reflection-based mapper configured at startup, which finds
that mistake at runtime, costs a reflection pass per call, and in its current
version carries a commercial licence.

## Decision

Riok.Mapperly. A `[Mapper]` partial class declares the mapping methods and the
source generator writes their bodies at compile time. An unmapped target member
is a build warning, and the build treats warnings as errors, so the forgotten
field fails the build rather than the request.

Mapping methods are extension methods on the request type, so a controller
reads `request.ToCommand(id)`. Route values are passed as additional
parameters and matched to the command's constructor parameters by name.

## Consequences

Zero reflection at runtime — the generated code is the same assignment a
person would have written. The generated source is visible in the IDE, so a
mapping is never a mystery.

The generator is a compile-time dependency only; nothing from it ships in the
runtime graph.

Mapperly maps by name. A contract field and a command field that mean the same
thing but are spelled differently need an explicit `[MapProperty]`. That is a
feature — the difference is visible in one attribute rather than hidden in a
configuration profile.