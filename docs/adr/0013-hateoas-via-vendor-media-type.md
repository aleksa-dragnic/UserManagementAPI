# 0013 — HATEOAS on request, through a vendor media type

## Context

Hypermedia is the constraint that makes an API RESTful in Fielding's sense:
responses carry links, so a client follows them instead of hard-coding URLs.
It is also the constraint most APIs skip, because the links make every
response larger and most clients ignore them.

Two ways to add links were on the table. Always include them, which changes
every existing response and every existing client's payload. Or include them
only when asked, which needs a way to ask.

## Decision

Links are opt-in. A client that sends
`Accept: application/vnd.umapi.hateoas+json` gets the payload wrapped with its
links — `{ "value": ..., "links": [ { "href", "rel", "method" } ] }` — and a
collection additionally gets `self`, `previous`, `next` and `create`. A client
that sends `application/json`, or nothing, gets exactly the representation it
got before.

A `ValidateMediaType` filter on the read actions decides: it answers 406 when
Accept names nothing the endpoint can produce, and marks the request when the
vendor type is present. The action picks the representation; the formatter
writes it with the vendor content type.

Links are generated from route names through `LinkGenerator`, never
concatenated, so an href is always an address the routing table matches. The
same set of links is offered whatever the user's state — whether `lock` is
legal right now is the aggregate's decision, taken when the request arrives,
and repeating that rule in the link generator would give it a second place to
go stale.

`GET /api` is the root document: anonymous, version-neutral, links only.
`OPTIONS` on the users collection lists the allowed methods in `Allow`, and
`HEAD` is answered on every read endpoint with the GET's headers and no body.

## Consequences

No existing client changes. A client that wants to navigate by links can, and
the root document gives it a single URL to start from.

The representation depends on Accept, so the read endpoints send
`Vary: Accept` and the ETag is computed over whichever representation was
chosen — plain and linked responses never share a tag.

The OpenAPI document describes the plain contract; the linked envelope is
documented here and in the README rather than as a second schema per endpoint.
That is the one place the document is less than complete, and it is a
deliberate trade against doubling every read operation in it.
