# 0017 — No data shaping on collection endpoints

## Context

Data shaping lets a client choose the fields of a response —
`GET /users?fields=id,email` — and is a standard chapter in REST API books. The
usual implementation reflects over the DTO and builds an `ExpandoObject` (or a
dictionary) holding only the requested properties.

The users collection already pages, filters, searches and sorts (M5 PR19), so
fields were the obvious next parameter.

## Decision

The API does not implement data shaping. Every response has the shape its
contract declares.

## Consequences

The OpenAPI document tells the truth. A shaped response is a
`Dictionary<string, object>` at runtime whatever the schema says, so a client
generated from the document either receives objects missing required members
or has to treat every field as optional. Typed clients — the ones most likely
to call this API — lose their types for a saving of a few bytes per user.

The projection stays static. `UserReadModels.ToDto` is one expression EF Core
translates into a fixed SELECT list; shaping would either select every column
and trim in memory, which gives back the saving it promised, or build the
projection dynamically from user input, which is a second surface to
whitelist.

ETags stay simple. A body that depends on a query parameter needs that
parameter in the cache key; here the representation depends only on the
resource and the negotiated media type.

What a client gives up is the ability to fetch less than a `UserResponse`. The
response is five fields. If a real client ever needs a narrower view, the
answer is a narrower contract with its own name — which is what the v2
endpoint (M5 PR21) demonstrates — not a query parameter that turns every
contract into a dictionary.
