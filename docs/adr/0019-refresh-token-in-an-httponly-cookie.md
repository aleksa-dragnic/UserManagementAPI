# 0019 — The refresh token travels in an HttpOnly cookie

## Context

Login and refresh returned both tokens in the response body, and refresh and
logout took the refresh token back in the request body. For a client running on
a server that is fine: it keeps the token wherever it keeps its other secrets.
A browser client has nowhere good to keep it. Anything a page's script can
read, a script injected into that page can read too, and the refresh token is
the valuable one — it lives seven days and mints a new access token each time
it is exchanged ([ADR 0009](0009-short-lived-jwts-with-rotating-refresh-tokens.md)).

The API now has a browser client: an admin console, a single-page application
served from its own origin under the same registrable domain as the API.

## Decision

The refresh token leaves every body. Login and refresh set it as a cookie:

| Attribute | Value | Why |
|---|---|---|
| Name | `umapi_rt` | |
| `HttpOnly` | set | No script can read it. |
| `Secure` | set | Never sent over plain HTTP. |
| `Domain` | absent | Host-only: no other subdomain of the domain ever receives it. |
| `SameSite` | `Strict` | Not sent on a cross-site request, which is what protects the three endpoints that read it without a separate anti-forgery token. |
| `Path` | `/api/v1/auth` | Sent to login, refresh and logout and to nothing else. `/api/v1/auth/refresh` would be narrower and would never reach logout, which revokes the token it receives. |
| `Max-Age` | the token's remaining lifetime | The cookie and the token expire together. Without it the cookie would die with the browser while the token lived on. |

The login and refresh bodies carry `accessToken` and `accessTokenExpiresAtUtc`
and nothing else. Refresh and logout take no body: the cookie is the
credential. A token sent in a body is ignored, so a refresh with a body token
and no cookie is 401. Accepting both would leave the old path live and untested
beside the new one.

A refresh with no cookie is 401 `Auth.InvalidRefreshToken`, the same answer as
an unknown token, so the response says nothing about why. Every refusal the
refresh handler returns clears the cookie, reuse included, since the token it
carries is of no further use. Losing the race against a simultaneous refresh of
the same token (409) leaves the cookie alone: in a browser the two answers can
arrive in either order, and a loser that cleared the cookie would erase the
winner's new one. The controller decides this by success or failure, not by
reading an error code.

Logout revokes the token in the cookie and clears the cookie. With no cookie it
still answers 204, as an idempotent logout already did for a token it did not
recognise.

CORS allows the console's exact origin with credentials, both from
configuration (`Cors:AllowedOrigins`, `Cors:AllowCredentials`). ASP.NET Core
refuses a wildcard origin together with credentials. `X-Correlation-Id` joins
the exposed headers, so the browser can show the id a log line carries.

The cookie is an HTTP concern and stays in the Api project. The application
layer still issues and receives the raw token; only the controller moves it
between the cookie and the command.

## Consequences

The refresh token is out of reach of any script on the page. A script injected
into the page can still make requests while the page is open, but it can no
longer carry the token away and use it for a week.

The cookie is first-party only when the console and the API share a registrable
domain. From an unrelated origin `SameSite=Strict` never sends it and refresh
does not work at all, so the deployment puts both under one domain.

A client other than a browser needs a cookie jar, or reads `Set-Cookie` and
sends `Cookie` itself. One that honours `Secure` sends the cookie back only over
HTTPS.

The response bodies of login and refresh changed shape, and so did the OpenAPI
document. A client that read `refreshToken` from the body has to move to the
cookie. There is no transition period in which both work, because the one
browser client was built against the cookie from the start.

Rotation, reuse detection and the concurrency token on `refresh_tokens`
described in ADR 0009 are unchanged. This record moves the token; it does not
change what the token does.