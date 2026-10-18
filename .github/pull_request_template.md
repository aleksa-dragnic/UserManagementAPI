## What
One or two sentences.

## Why
The reasoning. Link the ADR if this implements a decision.

## Notes
Trade-offs, anything deliberately left out, anything the next pull request picks
up. For a bug fix: the test that failed first.

<!--
Definition of done (docs/BUILD-PLAN.md section 11):
- Build passes with warnings as errors
- New behaviour is covered by a test at the appropriate level
- No business logic sits outside the domain
- Public endpoints appear correctly in the OpenAPI document
- Errors return RFC 9457 problem details with the right status code
- No secret, connection string or key is in the diff
- The description explains why, not only what
- An ADR is written if this implements a decision
-->
