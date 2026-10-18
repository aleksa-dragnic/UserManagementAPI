# Observability

Traces, metrics and logs, and how to see them locally and from a deployed
instance.

## What is produced

| Signal | Source | Notes |
|---|---|---|
| Traces | ASP.NET Core, HttpClient and Npgsql instrumentation | One span per request and one per SQL command. Requests to `/health`, `/scalar` and `/openapi` are not traced: the platform polls readiness every few seconds and those traces would drown the rest |
| Metrics | ASP.NET Core, HttpClient, .NET runtime, and the `UserManagementAPI` meter | `umapi.auth.logins` (tags `outcome`, and `error_code` on a refusal) and `umapi.auth.refresh_reuse_detected` |
| Logs | Serilog to the console | JSON in Production (ADR 0018); every line written during a request carries the correlation id, and in Production the trace and span ids |

Nothing is exported unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Without it the
signals are produced and discarded; the trace and span ids still reach the logs.

## Locally

`docker compose up -d --build` starts `grafana/otel-lgtm` beside the API and
points the API at it. Grafana is at http://localhost:3000: traces in Tempo,
metrics in Prometheus, under the service name `UserManagementAPI`.

## From a deployed instance: Grafana Cloud

The free tier is enough for a demo instance. The credentials are set on the
host, never in the repository.

1. In Grafana Cloud, open the stack, then **Connections → OpenTelemetry
   (OTLP)**, and create a token for it. The page shows the OTLP endpoint (an
   `https://otlp-gateway-….grafana.net/otlp` address) and the value for the
   `Authorization` header.
2. On the host — Render, for the demo instance — set three environment
   variables:

   | Variable | Value |
   |---|---|
   | `OTEL_EXPORTER_OTLP_ENDPOINT` | The OTLP endpoint from step 1 |
   | `OTEL_EXPORTER_OTLP_HEADERS` | `Authorization=Basic <value from step 1>` |
   | `OTEL_EXPORTER_OTLP_PROTOCOL` | `http/protobuf` (already set in `render.yaml`) |

3. Redeploy. Send a request, for example `GET /api`, then open **Explore →
   Tempo** and search for the service `UserManagementAPI`. A request appears as
   an HTTP span with its SQL spans under it.

`render.yaml` names the three variables; the two secret ones are marked
`sync: false`, so their values live only in the Render dashboard.
