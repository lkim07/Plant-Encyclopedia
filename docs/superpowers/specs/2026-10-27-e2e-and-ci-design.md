# End-to-end tests and CI with Docker — design

Date: 2026-10-27. Status: approved in conversation by the developer ("All good, go ahead").

## Intent

The developer wants proof that the app works end to end, checked automatically on every push: a Playwright
end-to-end suite kept in the repo, the whole app runnable in Docker, and a GitHub Actions workflow that builds,
tests and runs the end-to-end suite against the Docker stack. This finishes the automated part of Stage 10 and is the
CI half of Stage 11.

## Goals

1. **Docker for the whole app**:
   * an API image (ASP.NET Core 10);
   * a website image: the Angular production build served by nginx over HTTPS, passing `/api`, `/health`, `/media`
     and `/signin-*` through to the API;
   * Postgres 16;
   * one Compose file that starts all three, applies the EF Core migrations and loads the development sample data,
     usable locally with one command as well as in CI.
2. **A Playwright end-to-end suite** in `frontend/e2e/`, covering the journeys of the 2026-10-27 QA pass: Explore →
   plant → Back keeps the scroll position; search with suggestions, results and recent searches; the signed-out
   bookmark prompt; identification results, More, and the not-identified state (identification answered by a stub in
   the browser); theme switch kept after reload; skip link; reduced motion; Where to Buy with location granted and
   refused; sign-up → email confirmation → Favorites → Arrange → Undo; and an accessibility (axe, WCAG 2.1 A/AA)
   scan of every page at phone and desktop width in both themes.
3. **A GitHub Actions workflow** (`.github/workflows/ci.yml`) on every push and pull request: backend build and
   tests; frontend format check, unit tests, theme check and production build; then the Docker stack with the
   end-to-end suite. Any failure fails the run; the Playwright report is kept as an artifact when it fails.

## Non-goals

* No deployment, no image registry, no AWS (the CD half of Stage 11 comes later).
* No change to product behaviour.
* No real external services in CI: Pl@ntNet, GBIF, ChecklistBank, iNaturalist, Wikimedia and R2 stay off; the suite
  never needs network access beyond the local stack.

## Anti-goals

* A stack that works only because security was relaxed: cookies stay `Secure`, CSRF stays on, forwarded headers are
  trusted only from the configured proxy network, never from any client.
* Secrets in the repo or in the workflow: the stack needs none; the Postgres password for the throwaway database comes
  from the Compose file's default for this stack only and is not the developer's.
* Flaky tests that wait on fixed timers where a condition can be awaited.

## Decided

* CI only for now; local `docker compose` use too (the developer agreed to all four guesses).
* HTTPS in the stack with a throwaway self-signed certificate, made when the website container starts.
* The API runs with `ASPNETCORE_ENVIRONMENT=Development` in this stack: outside Development and Testing it refuses
  to start without a real email service (D-070 consequence), and account emails must be readable by the suite. The
  outside services are switched off by environment variables. The stack is never exposed beyond the machine or runner.

## How

### Forwarded headers (API change)

Behind nginx the API sees plain HTTP from nginx's address. It gains an opt-in `ForwardedHeaders` section:
`Enabled` (default false) and `KnownNetworks` (CIDR list). When enabled, `UseForwardedHeaders` applies
`X-Forwarded-For` and `X-Forwarded-Proto` only from those networks, before anything else in the pipeline, so:
secure cookies and antiforgery see HTTPS, per-client rate limits see the real client address, and the OAuth return
address scheme logic keeps working. With it off, behaviour is exactly as today. Tests: a request from a trusted proxy
address with `X-Forwarded-Proto: https` gets the antiforgery cookie; the same header from an untrusted address is
ignored (the request stays HTTP).

### SkiaSharp on Linux (package)

`SkiaSharp.NativeAssets.Linux.NoDependencies` 4.153.1 (matching SkiaSharp 4.153.1) in Infrastructure, so image
processing works in the Linux container and the backend tests pass on the Linux runner. Closes the TODO item.

### Database setup in the stack

A `migrator` stage of the API Dockerfile produces an EF Core migrations bundle; a one-shot Compose service runs it,
then loads `backend/dev-data/seed-dev-data.sql` and an end-to-end retailer fixture (a few clearly fake shops near
Vancouver and Kamloops, source `e2e-fixture`). The database is named `plant_encyclopedia_test`, which the seed
script's guard allows. The Postgres port is not published, so it never clashes with the developer's dev database on
5433.

### The suite

`@playwright/test` 1.64.0 and `@axe-core/playwright` 4.14.0 as frontend dev dependencies; `frontend/e2e/` with its
own `playwright.config.ts` (base URL `https://localhost:8443` by default, `ignoreHTTPSErrors` for the throwaway
certificate, Chromium only, retries 1 in CI and 0 locally, traces on first retry). Account emails are written to a
bind-mounted folder the suite reads to confirm the account. Each run creates its own `e2e-<timestamp>@example.org`
account in the throwaway database.

### Formatting

`prettier --check` runs on all of `frontend/src` in CI, so the three older templates it flags today
(`search-box.html`, `search-page.html`, `shell.html`) are formatted once, with the unit tests run to make sure the
reformatting changes nothing (a past reformat broke a search test).

## Left to the builder

* Exact Dockerfile layering and nginx configuration, within the above.
* How the suite waits for the stack (Compose health checks plus `--wait` are the natural choice).
* Test file layout inside `frontend/e2e/`.

## Verification

* The workflow file is valid and every job's commands run locally first: backend build and tests, the frontend
  checks, `docker compose -f docker-compose.e2e.yml up --build --wait`, and the suite against it.
* The first real GitHub run happens when the developer pushes; its result is reported honestly.
