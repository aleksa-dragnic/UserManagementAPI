# 0004 — The domain model is the persistence model; no separate DB entities

## Context

A common layout keeps rich domain classes and a parallel set of anaemic
"database entities", with a mapper between them. It promises that persistence
concerns cannot leak into the domain.

In practice, the two models are the same shape, so every field is written three
times — domain class, database entity, mapper — and a change means editing all
three. The mapper also has to reconstruct aggregates from rows, which means
reaching past private constructors and invariants, so the guarantees the domain
model exists to provide are bypassed on every load.

## Decision

There is one model. `User`, `Role` and `Permission` are mapped directly to
tables. There is no separate persistence type and no mapper between them.

The domain stays clean of persistence by putting all mapping in
`IEntityTypeConfiguration<T>` classes inside Infrastructure. No attribute goes
on a domain class, and the Domain project keeps zero package references — the
constraint is asserted in CI rather than remembered.

Domain classes carry what EF Core needs and nothing more: a private
parameterless constructor for materialisation, private setters, and collections
exposed read-only over a backing field the configuration writes through.

## Consequences

One place to change when a field changes. The aggregate is rehydrated by EF
Core through its own field access, so invariants written in methods still hold
for a loaded instance.

The cost is that EF Core's requirements shape the domain slightly: a private
parameterless constructor exists purely for materialisation, and `DomainEvents`
must be explicitly ignored so it is not treated as a mapped property. That is a
small, visible cost, paid in one file per aggregate.

The read path does not use this model at all. Queries project straight into DTOs
(M5), so nothing loads an aggregate to display it.