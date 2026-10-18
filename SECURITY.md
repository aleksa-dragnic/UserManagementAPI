# Security policy

## Supported versions

| Version | Supported |
|---|---|
| 2.x | Yes |
| 1.x and earlier | No |

This is a personal project built to a production standard. The supported
version is the latest release on `main`; fixes are not backported.

## Reporting a vulnerability

Please do not open a public issue for a vulnerability.

Report it privately through GitHub: the repository's **Security** tab, then
**Report a vulnerability**. If that option is not offered, open an issue that
says only that you have something to report, with no details, and you will be
contacted.

A report is acknowledged within seven days. You will hear what is being done
about it and when a fix ships.

## Scope

In scope: this repository's code and its documented behaviour, including
authentication, refresh-token rotation, authorization, rate limiting and the
data the API stores.

Out of scope:

- The demo instance at `api.aleksadragnic.com` as a target of load or denial of
  service. It runs on free tiers and sleeps when idle.
- The published demo account. Its password is in the README on purpose, and it
  can only read.
- Requests for paths that do not exist (`/.env`, `phpinfo.php` and similar).
  Automated scanners send them all day and the API answers 404. They are not
  rate limited by the API; at this size that is left to the platform's edge.
- Findings that need a compromised machine, browser or Render account.

## What is already in place

How the API treats credentials, tokens and requests is described in the README
(Security, Known limits) and in ADRs 0009, 0010, 0018 and 0019.
