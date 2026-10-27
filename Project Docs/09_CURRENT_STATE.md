# Current State

## 1. Purpose

This document records the current state of the Plant Encyclopedia project.

Unlike the product requirements and roadmap documents, this document describes what is currently known, decided, implemented, partially implemented, or still pending.

The purpose is to prevent AI coding agents and developers from confusing planned functionality with implemented functionality.

This document should be updated whenever the actual implementation state changes significantly.

---

# 2. Project Status

## Overall Status

**Phase: Early Implementation — backend and database foundation**

The product concept, UX direction, architecture, database design, API contract, and development roadmap have been defined.

The application is not yet considered production-ready.

The implementation should proceed incrementally according to `DEVELOPMENT_ROADMAP.md`.

---

# 3. Product Definition

## Product

Plant Encyclopedia is an image-first plant discovery application.

The primary user scenario is:

> "I know this is a rose, but what exact variety or cultivar is it?"

The application expands this into a broader discovery experience:

```text
Discover
→ Identify
→ Compare
→ Learn
→ Find
→ Save
→ Discover More
```

The application is intended to serve general plant-curious users rather than a narrow group of professional botanists or horticultural specialists.

---

# 4. Product Positioning

The current product direction is:

> A fast, image-first plant discovery experience with subtle botanical interactions.

The product should prioritize:

1. Trustworthy information
2. Fast perceived performance
3. Frictionless UX
4. Clear information hierarchy
5. Strong visual design
6. Responsible AI usage

The project should feel like a real consumer product rather than a collection of technical demonstrations.

---

# 5. Confirmed Technology Direction

The current planned stack is:

| Area                  | Technology                     |
| --------------------- | ------------------------------ |
| Web frontend          | Angular                        |
| Frontend language     | TypeScript                     |
| Backend               | ASP.NET Core                   |
| Backend language      | C#                             |
| Database              | PostgreSQL                     |
| ORM                   | Entity Framework Core          |
| Containerization      | Docker                         |
| Cloud                 | AWS                            |
| Object storage        | Amazon S3 where appropriate    |
| CI/CD                 | GitHub Actions                 |
| Mobile                | .NET MAUI                      |
| Initial mobile target | Android                        |
| AI                    | External vision-capable AI API |

The exact choice of AI provider may remain replaceable through a provider abstraction.

iOS is currently deferred because the project does not have access to a Mac development environment.

---

# 6. Documentation State

The following project documents have been defined:

* `PROJECT_INSTRUCTIONS.md`
* `PRODUCT_REQUIREMENTS.md`
* `UX_SPECIFICATION.md`
* `DECISIONS.md`
* `ARCHITECTURE.md`
* `DATABASE_DESIGN.md`
* `API_SPEC.md`
* `DEVELOPMENT_ROADMAP.md`
* `CURRENT_STATE.md`
* `TODO.md`

### Current documentation status

| Document                  | State            |
| ------------------------- | ---------------- |
| `PRODUCT_REQUIREMENTS.md` | Defined          |
| `UX_SPECIFICATION.md`     | Defined          |
| `DECISIONS.md`            | Defined          |
| `ARCHITECTURE.md`         | Defined          |
| `DATABASE_DESIGN.md`      | Defined          |
| `API_SPEC.md`             | Defined          |
| `DEVELOPMENT_ROADMAP.md`  | Defined          |
| `PROJECT_INSTRUCTIONS.md` | Defined          |
| `CURRENT_STATE.md`        | Current document |
| `TODO.md`                 | Pending          |

The documentation should be treated as a coordinated system rather than independent notes.

---

# 7. Implementation State

## Backend

**Status: Foundation partially implemented**

Implemented and verified:

* ASP.NET Core solution `backend/PlantEncyclopedia.sln` (.NET 10) with Api, Application, Domain, and Infrastructure projects.
* EF Core persistence foundation (see §8).
* `GET /health` — `200 {"status":"healthy"}` when PostgreSQL is reachable, `503 {"status":"unhealthy"}` otherwise (API_SPEC §68).
* Global exception handling — unhandled exceptions return `500` with the fixed `INTERNAL_SERVER_ERROR` body (API_SPEC §12); details are logged server-side with the default ASP.NET Core console logging.
* Test project `backend/PlantEncyclopedia.Tests` (xUnit 2): PostgreSQL integration tests (see §8) and API tests. The API tests run the real API in memory (`WebApplicationFactory`) in a `Testing` environment, so User Secrets are never loaded, and point it only at the disposable test container.

* `GET /api/plants/{plantId}` — Plant Detail for Published plants only (API_SPEC §14): `id`, `name`, `plantGroup`, `plantGroupId` (group slug), `scientificName`, `description`, `heroImage` (always `null` for now), `quickCare` (Verified care only, numeric temperature). `404 PLANT_NOT_FOUND` for unknown or unpublished plants, `400 VALIDATION_ERROR` for a malformed ID. Minimal API endpoint → `IPlantQueries` (Application) → EF Core query (Infrastructure).
* `GET /api/categories`, `GET /api/categories/{categoryId}`, `GET /api/plant-groups/{plantGroupId}` (API_SPEC §17–19): category and plant-group IDs are slugs; name ordering; category-pure group lists; Published plants only with page/pageSize pagination (default 20, max 50); `CATEGORY_NOT_FOUND` / `PLANT_GROUP_NOT_FOUND` 404s and `VALIDATION_ERROR` 400s. `imageUrl` fields are `null` until images exist. Covered by 11 API tests.
* `GET /api/explore` (API_SPEC §20–23, Phase 7): the discovery feed for visitors without behavioral data. Published plants with a group, each once; categories take turns, plant groups take turns inside a category, and plants with an image come first inside a group. The order is deterministic, so pages are stable. Same paging limits as plant groups (`pageSize` ≤ 50, `400 VALIDATION_ERROR` otherwise). Cards are `{ id, name, plantGroup, imageUrl }`. Covered by 8 API tests (every Published plant once, no Drafts, turn-taking across categories and groups, images first, stable pages, shape, validation); the turn-taking test was confirmed to fail when the order is broken. Personalization is not implemented.
* `GET /api/search` (API_SPEC §24–25): Published plants; every word must appear in a display, scientific, or cultivar name, an alternative name (any language), or the group name; case-, accent-, and punctuation-insensitive (database `search_key()` function, NFKC-normalized input); relevance ordering (exact → starts with → all words in main names → other matches), then name and ID; page ≤ 50. Input validation (required, ≤ 100 characters, no control characters, a letter or digit, ≤ 10 words), 30 requests/minute per client IP (`429 RATE_LIMITED` with `Retry-After`), 5-second timeout (`503 REQUEST_TIMEOUT`), and search text kept out of logs. Parameterized queries only; `%`, `_`, `\` are never wildcards. Covered by 28 API tests and 11 database tests. Search itself never calls external services (see the Catalogue of Life lookup below). Server-side recent searches are not implemented.
* `GET /api/search/suggestions` (API_SPEC §26): up to 8 database-backed suggestions (`plant`, `cultivar`, `scientificName`, `commonName`, `synonym`, `plantGroup`) from Published plants and groups with Published plants; word-start matching with the same folding as search; starts-with first, then shorter, then alphabetical; identical texts merged; items are `{ text, type }` (no plant ID: selecting a suggestion runs a search); text under 2 characters returns `[]`. Same text validation as search; its own limit of 120 requests/minute per IP; 2-second timeout; typed text kept out of logs. One parameterized SQL statement (no SQL built from user text). The trigram indexes were confirmed usable for each name source with `EXPLAIN` on the development data; whether the planner chooses them inside the full query depends on data volume and has not been measured at scale. Covered by 17 API tests.
* `POST /api/search/external` — Catalogue of Life lookup, Phase 11c (API_SPEC §27.1, ARCHITECTURE §23–24).
  * **Behaviour:** body `{ "q" }`, validated like search, max 1 KB. Only name-shaped text (2–4 words of letters) reaches GBIF, sent in botanical casing.
  * **Responses:** `found` with a search-result-shaped plant (only if Published), `pendingReview`, or `notFound`. When GBIF is unavailable, the response is `503 EXTERNAL_LOOKUP_UNAVAILABLE`.
  * **Limits:**
    * per client IP: 5 per minute
    * app-wide: 30 external calls (GBIF and ChecklistBank together) per minute and 1,000 per day
    * a 24-hour in-memory cache of names that cannot be imported (never failures)
    * a 5-second request timeout
    * the text is never logged
  * **Attribution:** `GET /api/plants/{id}` now returns `attributions` (e.g. Taxonomy: "Catalogue of Life, via GBIF"); it is empty for curated plants.
  * **Configuration:** GBIF is enabled only in `appsettings.Development.json`. It is still off in `appsettings.json` and forced off in `Testing`.
  * **Tests:** 49 new backend tests:
    * endpoint statuses, validation, 413, 429, 503 and no logging
    * the name check
    * call budget windows
    * the cache (what is stored, 24-hour expiry, keyed by exact name, hits do not use the budget)
    * Plant Detail attributions
  * **Total:** 268 backend tests pass with 0 build warnings. No migration; no live GBIF requests were made in 11c.
  * **Manually verified** by the developer on 2026-10-17 in the browser against real GBIF (Development).
* Dependency and secret scanning in CI, 2026-10-27.
  * **Job:** `security` in `.github/workflows/ci.yml`, on every push and pull request and weekly (SECURITY §20).
  * **Secrets:** gitleaks found 3 matches in the 46-commit history, all GBIF's public Catalogue of Life dataset key (not a credential); they are ignored by exact fingerprint. A planted fake token fails the scan.
  * **Dependencies:** .NET has no vulnerable packages. `npm audit fix` (within existing ranges) updated zod 4.6.6, undici 8.12.3, fast-uri 3.1.9 and the Angular CLI and schematics to 22.2.4; browser code now has no known vulnerabilities. Left: `vite` 8.3.0 pinned by `@angular/build` (high, development server only).
  * **Verified locally:** each check passes on the repo; the .NET check fails on a scratch project with System.Text.Json 8.0.4 (high); actionlint finds no errors; 151 frontend tests pass and the build succeeds after the lock-file update. On GitHub (commit b0ebf94, run 40573216342) all four jobs passed; the security job took 60 s.
* End-to-end tests, Docker and CI (D-075), 2026-10-27.
  * **Docker:** `docker-compose.e2e.yml` runs the whole app at `https://localhost:8443`: Postgres 16 (`plant_encyclopedia_test`, port not published), a one-shot setup (EF Core migrations bundle, the sample data, six fake test shops), the API (`backend/Dockerfile`, as "Testing": no outside services) and the website (`frontend/Dockerfile`: the production build on nginx with a throwaway self-signed certificate; `/api`, `/health`, `/signin-*` and unknown `/media` files go to the API; `/api/dev/` is blocked).
  * **Forwarded headers:** opt-in `ForwardedHeaders` settings (`Enabled`, `KnownNetworks`); the headers are applied only from those networks (4 tests: trusted, untrusted, off by default, an invalid network stops startup).
  * **SkiaSharp:** `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.153.1 added for Linux.
  * **Suite:** `frontend/e2e/` (Playwright 1.64.0, axe-core 4.14.0), `npm run e2e`: 52 page scans (13 pages × phone/desktop × light/dark: no WCAG 2.1 A/AA violations, no sideways scrolling, the right theme, no failed requests) and 11 journeys (Back to Explore, search, the signed-out prompt, identification results and not-identified with a stubbed answer, theme kept, skip link, reduced motion, Where to Buy with location granted and refused, sign-up → confirmation → Favorites → Arrange → Undo). 63/63 pass: five runs on one stack, plus one from a clean build.
  * **Found and fixed:** the build's font files live under `/media`, the same path as stored plant images, so behind a proxy that sent `/media` to the API every font failed (nginx now serves its own files first). The account journey had raced the instant bookmark (the QA script hid it with a fixed wait); it now waits for each save.
  * **CI:** `.github/workflows/ci.yml`: backend build and tests; frontend Prettier check, e2e type check, unit tests, theme check and build; then the Docker stack and the suite, with the report kept on failure. actionlint 1.7.12 finds no errors and every command was run locally; the first run on GitHub (commit bc3252c, run 40464368936, 2026-10-27) passed all three jobs: backend 114 s, frontend 35 s, e2e 167 s. Test counts and retries are in the job logs, which need a GitHub sign-in and were not read.
  * **Tests:** 529 backend tests pass (0 warnings, Debug and Release); 151 frontend tests pass; `prettier --check src e2e` is clean (five older files formatted).
* Browser QA pass, 2026-10-27 (headless Chromium via Playwright, run from outside the repo; nothing added to the project).
  * **Pages:** 13 pages × phone (390 px) and desktop (1280 px) × light and dark (52 loads): no axe-core WCAG 2.1 A/AA violations, no horizontal scrolling, the right theme on every load, no failed requests. The only console message is the browser's own log of the expected `401` from `GET /api/me` for signed-out visitors.
  * **Journeys (all pass):** Back to Explore keeps the scroll position; search suggestions, results and recent searches; the signed-out bookmark opens the sign-in prompt and Esc closes it; identification results, More, and the not-identified state with Try Again and Choose Another Photo (with a stubbed API response, no Pl@ntNet call); theme switch kept after reload with no flash; skip link first in tab order; titles visible at once with reduced motion; Where to Buy with location refused (note, then choosing Kamloops); sign-up → email confirmation (development email file) → saving two plants → Arrange → remove and Undo keeping the order.
  * **Fixed:** Back to Explore restored the top instead of the user's place — the position was read when the page was destroyed, after the next page had reset the scroll; it is now taken at navigation start (the Explore test now models this and failed before the fix). The sidebar theme switch showed its icon above its label (the shared `.icon-button` is a grid); it is now a row with nav-item sizing (new test).
  * **Test data:** QA accounts `qa-<number>@example.org` were created in `plant_encyclopedia_dev` (two runs), each with two favorites.
  * **Not covered:** a real identification (would spend Pl@ntNet calls), Google and GitHub sign-in (no real OAuth round trip headless), password reset, the personalized feed after enough activity, touch gestures (drag, long press), and physical phones.
  * **Tests:** 151 frontend tests pass; `ng build` without warnings; `check:theme` passes.
* Light and dark themes, display type and motion (D-074), 2026-10-27.
  * **Themes:** light "Greenhouse Daylight" and dark "Night Greenhouse" as colour tokens in `src/styles.css` (`html[data-theme]`). `index.html` picks the theme before the app starts (stored choice, else the device setting); `core/theme/theme.ts` follows the device until the visitor chooses, remembers the choice (guarded storage), updates the browser's theme colour and cross-fades the change. A `ThemeToggle` button in the sidebar (with a label when expanded) and the top bar.
  * **Colours:** new tokens for the accent, text on the accent, borders, errors and shadows; primary calls to action use the accent; about 30 hard-coded colours replaced. `npm run check:theme` (frontend) prints every contrast ratio — light 4.21–11.94 for focus and text pairs, all text pairs ≥ 4.6; dark ≥ 6.43 — and fails on a hard-coded colour outside masks, provider logos, photo overlays and the theme colour.
  * **Type:** Martian Mono for page titles and the sidebar name (`@fontsource-variable/martian-mono` 5.3.0, OFL-1.1; the Latin file is 23.5 kB), about 34–64 px; Plant Detail's hero name 28–48 px.
  * **Motion:** `TitleRise` directive (words rise over the untouched title; stops if the title changes), card lift and slower zoom, pill fill, theme cross-fade, leaf sway once on hover (D-054). All off with reduced motion.
  * **Tests:** 16 new frontend tests (theme: stored values, painted theme, toggle with storage and meta, blocked storage, device changes before and after a choice, stored choice over device, short cross-fade and none with reduced motion; toggle: name, icon, switch, label; shell: toggle in sidebar and top bar; title rise: reduced motion, hidden copy over untouched text, title change mid-animation — confirmed to fail without the change watcher —, cancel on destroy, the real title shown again when the animation library fails to load; the GitHub sign-in mark drawn in the text colour so it shows on dark buttons). 150 frontend tests pass; `ng build` without warnings; `check:theme` passes.
  * **Not verified yet:** both themes and the motion in a browser on a phone and desktop; then the full QA pass.
* Where to Buy screens, Phase 9b (PRD §27–28, UX §33–35, D-031–D-035, D-073), 2026-10-26.
  * **Entry card:** on Plant Detail after Quick Care: "Where to Buy / Find nurseries, garden centres & flower shops near you / Explore →". Opening Plant Detail never asks for location.
  * **Page (`/plants/:plantId/where-to-buy`):** asks the browser for the location only here, once per visit (10-second limit); the position is rounded to about 100 m before it is sent and is kept in memory only. Refused, unavailable or slow: Vancouver, BC, with a note saying so. A location chooser (`Use my location`, Vancouver, BC, and the towns in the shop data) and a Distance / Relevance switch. Each shop shows its kind, distance, address when known and a website link (new tab, `noopener`). A note says stock is not known and to check before going; no ratings or prices. "No shops found within 25 km" offers 50 km, then 100 km. OpenStreetMap credit under the list. No Online section (D-073).
  * **Code:** `RetailerApi` (core/api), `ShoppingLocationService` (core/location), `WhereToBuyPage` (features/where-to-buy), a `map-pin` icon.
  * **Tests:** 5 new frontend tests (location asked only here and sent rounded — confirmed to fail without the rounding —, shops as places to check with credit; refused location → Vancouver with a note and choosing a town; sort switch; wider search; the Plant Detail card without a location request). 134 frontend tests pass; `ng build` without warnings.
  * **Not verified yet:** in a browser on a phone and desktop (the permission prompt, the chooser, the list).
* Where to Buy backend, Phase 9a (PRD §27–28, API_SPEC §45–51, D-031–D-035, D-073), 2026-10-26.
  * **Data:** `retailers` table (migration `AddRetailers`, applied to `plant_encyclopedia_dev`). First import on 2026-10-26 (approved one-time Overpass query): 354 BC shops — 192 florists, 138 garden centres, 24 nurseries; 96 of them mapped as areas and imported at their centre point; 79 have a town. OpenStreetMap data, © OpenStreetMap contributors, ODbL 1.0.
  * **Refresh:** `dotnet run --project PlantEncyclopedia.Api -- import-retailers` (needs `ExternalPlantData:Gbif` enabled for its contact URL). Untrusted records are cleaned (text, http(s) links only, coordinates); a snapshot under half the stored size is refused.
  * **Endpoints:** `GET /api/plants/{plantId}/retailers?latitude&longitude&radius&sort` (distance or relevance; shop kinds by plant category; availability always unknown; no ratings or prices; attribution) and `GET /api/retailers/places` (towns for choosing a location). The location is never stored or logged.
  * **Tests:** 15 new backend tests (Overpass request and parsing against a response constructed from the documented OSM JSON shape, failures, cleaning, the size guard, relevance; nearest first within the radius — confirmed to fail without the distance check —, kinds by category, validation, unknown plants, places, refresh). 525 backend tests pass; 0 build warnings.
  * **Not yet:** the screens (9b).
* Identification imports and the More page, Phase 8c (PRD §30, UX §37, D-072), 2026-10-26.
  * **Imports:** a likely Pl@ntNet suggestion missing from the encyclopedia is imported through Catalogue of Life (same pipeline and rules as "Search Catalogue of Life"; at most three per photo) and shown at its rank. One whose genus has no plant group is Published without a group: it has a detail page and cards, but stays out of Explore and categories. Near-duplicates are still held for review. Search imports are unchanged.
  * **Database:** migration `AllowPublishedPlantsWithoutGroup` drops `ck_plants_published_requires_group`; applied to `plant_encyclopedia_dev` on 2026-10-26 with the developer's approval. Identification request timeout raised to 30 seconds.
  * **More:** `GET /api/identification/related?plantId=…` (1–3 candidates) and the page `/identify/more?plants=…`: related Published plants, closest first (same group, genus, family, then category), as an image grid with bookmarks and infinite scroll; an empty state with Explore. The More tile on the results page now opens it.
  * **Tests:** backend 12 new: 2 service tests (import shown at its rank; at most three imports, failures skipped), 2 ingestion tests (published without a group; near-duplicates still held), 1 API test of a real import through the pipeline with fake providers (confirmed to fail without the new option), 7 related-plants API tests (closeness order — confirmed to fail with the tiers swapped —, first candidate first, stable pages, validation). 510 backend tests pass; 0 build warnings. Frontend 4 new More page tests and an updated results test. 129 frontend tests pass; `ng build` without warnings.
  * **Not verified yet:** a real identification that imports a species (uses Pl@ntNet and GBIF), and the More page in a browser.
* Identify screens, Phase 8b (PRD §8–9, UX §6–11, §44; D-012–D-014, D-046), 2026-10-25.
  * **Identify (`/identify`, the home screen):** Take a Photo (opens the back camera on phones) and Choose from Photos; no search field. A preview with Retake and Identify; nothing is sent until Identify. Other file types and photos over 10 MB are refused before uploading.
  * **Identifying:** the photo stays in view, dimmed slightly, with "Identifying your plant…"; no spinner or percentages.
  * **Failures (UX §44):** the photo stays; "We couldn't identify this plant / The image may be unclear, or the plant may not be in our database yet." with Try Again and Choose Another Photo; separate messages for unavailable, too many tries, offline and unusable photos.
  * **Identification Results (`/identify/results`):** a small copy of the user's photo, then #1 large (image, name, plant group, scientific name), then #2, #3 and More in one row. Candidates open Plant Detail, and Back returns to the results (D-014); Back from the results shows the same photo. More is outside the numbered list (not a fourth candidate) and opens the #1 candidate's plant group until the contextual More page exists (8c, the developer's choice). On desktop, hovering #2 or #3 fades the image and shows the scientific name (UX §11). No confidence values. Opened without a current identification (e.g. after a reload), it returns to Identify.
  * **Code:** `IdentificationApi` (core/api), `IdentifySession` (core/identification: the photo and results for this visit, in memory), `IdentifyPage` and `IdentificationResultsPage` (features/identify), an `image` icon. Backend: `plantGroupId` added to identification candidates.
  * **Tests:** 7 new frontend tests (the two choosers and the back camera, preview without sending, refusing other types and large photos — confirmed to fail without the size check, identifying with the photo in view then #1/#2/#3 and More with More outside the list, not identified with Try Again and then unavailable, the results page without results, Back to the same photo). 125 frontend tests pass; `ng build` without warnings. 20 identification backend tests pass with `plantGroupId`.
  * **Not verified yet:** in a real browser on a phone and desktop (camera, preview, results, hover), and a real identification that matches an encyclopedia plant.
* Development setup fixed for browser checks, 2026-10-25.
  * **Problem:** the dev proxy forwarded http://localhost:4200 to the API over plain HTTP, but the session and antiforgery cookies require HTTPS, so `GET /api/me` and every CSRF-protected request failed with 500 in the browser.
  * **Fix:** the proxy (`frontend/proxy.conf.json`) now targets `https://localhost:7071`; run the API with its `https` launch profile (`dotnet run --project PlantEncyclopedia.Api --launch-profile https`) and the frontend with `ng serve`, then open http://localhost:4200. Browsers accept the Secure cookies on localhost. For the Google and GitHub sign-in start and callbacks, a request for the public host takes the scheme of `Accounts:PublicBaseUrl` (`UseExternalSignInAddress`), so the return addresses stay `http://localhost:4200/signin-google` and `/signin-github` as registered; production (HTTPS everywhere) is unchanged.
  * **Verified:** through the running proxy, `GET /api/me` answers 401 with the cookies, a CSRF-protected sign-in request reaches the sign-in logic, and both providers' redirect addresses are the registered ones. 1 new backend test (confirmed to fail without the middleware); 498 backend tests pass; 0 build warnings.
* Plant identification backend, Phase 8a (PRD §8–9, API_SPEC §36–42, D-071), 2026-10-25.
  * **Endpoint:** `POST /api/identification` (multipart, field `image`), open to everyone; CSRF token required; 6 per minute per client; 20-second timeout. Returns up to three Published plants (`rank`, `plant` with id, name, plant group, scientific name, image), no scores; `422 IDENTIFICATION_FAILED` when nothing matches; `503 IDENTIFICATION_UNAVAILABLE` when the provider is off, over budget or failing.
  * **Photo handling:** at most 10 MB; JPEG, PNG or WebP from the bytes; 64–16,000 px; decoded at reduced scale where possible; turned upright; re-encoded as a JPEG of at most 1280 px without metadata; never stored or logged.
  * **Provider:** Pl@ntNet (`Identification:PlantNet` settings: `Enabled`, `ApiKey` in User Secrets, `Project` = `all`), behind `IPlantIdentificationProvider`; off unless configured, always off in tests. App-wide budget 20 per minute and 400 per day; 24-hour cache of suggested names by photo hash. Any non-200 answer counts as unavailable (Pl@ntNet does not document its error codes).
  * **Matching:** suggestions with a score of at least 0.05, in the provider's order, matched to Published plants by scientific identity (species before its cultivars); unmatched suggestions are dropped, never imported.
  * **Tests:** 20 new backend tests: Pl@ntNet request shape and parsing against a response constructed from the documented fields (not a recorded live response), error statuses as unavailable with the key never logged, invalid answers, settings; photo shrinking and format, metadata removal and orientation (confirmed to fail without the rotation), rejection of other files; cache and budget; candidate rules; and API tests for published-only matches in order (confirmed to fail without the Published filter), no match, no provider, upload checks, CSRF and the rate limit. 497 backend tests pass; 0 build warnings.
  * **Live check (2026-10-25, approved by the developer, 3 Pl@ntNet calls):** the real response has the documented fields (`query`, `predictedOrgans`, `language`, `preferedReferential`, `bestMatch`, `results`, `version`, `remainingIdentificationRequests`; each result with `score` and `species.scientificNameWithoutAuthor`), and the adapter read all 10 results. Our Dog rose photo (a close-up of a stem with one thorn) came back as *Smilax rotundifolia* (0.22) with roses at 0.016, so the API correctly answered `422 IDENTIFICATION_FAILED`. A plain generated image was not rejected with an error: Pl@ntNet answered 200 with scores below 0.01, which the 0.05 cut-off turns into `422`. Pl@ntNet's error status codes are still unverified. The API key appeared in no log.
  * **Not verified yet:** a photo that should match a plant in the encyclopedia. (The screens were added in 8b.)
* Explore feed restored on Back, Phase 7c (UX §15), 2026-10-25.
  * **Behavior:** leaving Explore remembers the loaded cards, page, `feedAt` and scroll position (in memory, this visit only). Back or Forward to Explore shows them again and scrolls to the same place without reloading the feed; further pages continue the same order. Arriving any other way (sidebar, links) loads a fresh feed. A remembered feed is only restored for the same user (or signed-out visitor), so a personalized feed is never shown to someone else.
  * **Code:** `ExploreFeedMemory` (features/explore) and the Explore page; detection uses the router's `popstate` navigation trigger. Category pills still reload.
  * **Tests:** 3 new frontend tests (Back restores cards and scroll without a feed request and continues paging; a link loads a fresh feed; another user's feed is not restored — confirmed to fail without the check). 118 frontend tests pass; `ng build` without warnings. No backend change.
  * **Not verified yet:** in a real browser (phone and desktop, including the smooth scrolling on desktop).
* Personalized Explore, Phase 7b (PRD §10–12, D-020–D-023, API_SPEC §20–22), 2026-10-25.
  * **Who:** signed-in users with enough activity (total signal weight 3, e.g. three plant views or one saved plant). Everyone else gets the default order.
  * **Rules:** `PersonalizedFeed` (Application/Explore). Views count 1, saved plants 3, searches 2 shared among the plant groups they match; each halves every 14 days. Every 10 cards: 6 from groups with interest, 2 from other groups of those categories, 2 from untouched categories (an empty kind gives its turn to the next). Groups take turns inside each kind; plants already viewed or saved come after the others in their group. Each plant once.
  * **Stable pages:** responses return `feedAt`; Explore sends it with later pages, so activity while scrolling does not reorder the feed. A removed search stops counting at once.
  * **Tests:** 8 new unit tests (threshold, 6/2/2 in every 10, turn-taking, each plant once, empty kind, ageing, seen plants later, same order for the same input) and 5 new API tests (feed follows viewed group and stays mixed, feedAt keeps one feed's order while a new feed uses new activity — confirmed to fail without the cutoff, removed search, another user's activity, feedAt validation). 477 backend tests pass; 0 build warnings. Frontend: the Explore paging test now checks `feedAt` (confirmed to fail without it). 115 frontend tests pass; `ng build` without warnings.
  * **Not yet:** a manual check in the browser.
* Search and view history, Phase 7a (PRD §16–17, API_SPEC §28–29), 2026-10-25.
  * **Scope:** signed-in users only (the developer's choice). Visitors who are not signed in have no server-side history; their recent searches stay in their browser as before.
  * **Database:** `search_history` and `view_history` tables (migration `AddHistory`, applied to `plant_encyclopedia_dev` on 2026-10-25). Newest 50 searches and 200 views kept per user; deleted with the user (views also with the plant).
  * **Endpoints:** `GET`, `POST /api/history/searches`, `DELETE /api/history/searches/{searchId}`, `POST /api/history/views`. Sign-in required; changes need the CSRF token; 120 requests per minute per user. A repeated search (ignoring case) moves to the top; the same plant opened again within 30 minutes is one view. `GET /api/history/views` is not implemented (view history is not shown).
  * **Frontend:** Recent Searches come from the server when signed in (changes show at once, requests run in order, a failure reloads the list) and from this browser otherwise; signing out shows this browser's list again. Plant Detail records a view of each loaded plant for a signed-in user, also when the sign-in check finishes after the plant loaded.
  * **Tests:** 8 new backend tests (sign-in required, newest first with repeats moved up and normalized text, validation, ownership on removal, search cap, view repeat window and hidden plants, view cap, CSRF); the cap and repeat-window tests were confirmed to fail when those rules are broken. 464 backend tests pass; 0 build warnings. 5 new frontend tests (server list without browser storage, removal also right after adding, reload after a failure, back to the browser list after sign-out, one recorded view). 115 frontend tests pass; `ng build` without warnings.
  * **Not yet:** a manual check in the browser. (Explore uses the history since 7b.)
* Favorites Arrange mode, Phase 6d (PRD §20, UX §40), 2026-10-25.
  * **Entering:** an Arrange button on the Favorites page (shown on phones too), or a half-second long press on a card on touch screens (a short tap or a scroll does not count). Done or Esc leaves.
  * **While arranging:** cards jiggle (a dashed outline with reduced motion), are inert (no link, no bookmark), and show a minus control that removes with "Removed from Favorites. Undo". Cards can be dragged (mouse and touch) or moved with Move earlier / Move later buttons for keyboard and screen-reader users; each move is announced ("Fern moved to position 2 of 5").
  * **Saving:** each move saves the whole order at once with `PATCH /api/favorites/order`, after any save or removal still on its way; if it fails, the order goes back to the server's and a message says so.
  * **Package:** @angular/cdk 22.2.4 (MIT), drag-and-drop only, loaded with the Favorites page.
  * **Tests:** 5 new frontend tests (Arrange, Done and Esc with inert cards and no bookmarks; move buttons with announcement and the saved order; rollback on a failed save; minus control with Undo and the order waiting for the removal — confirmed to fail without the wait; long press on touch only, not after a move). 110 frontend tests pass; `ng build` without warnings. Dragging itself is not covered by automated tests.
  * **Not verified yet:** dragging, the jiggle and the long press in a real browser (phone and desktop).
* Favorites screens, Phase 6c (PRD §18–21, UX §38–41), 2026-10-24.
  * **Bookmark toggle:** outlined when a plant is not saved, filled when it is, on plant cards (Explore, Plant Group, Search results) and next to the name on Plant Detail. It changes at once and the request follows; requests for one plant are sent in order; on a failure the message says why and the list is reloaded from the server. Elsewhere than the Favorites page, toggling shows no message (the developer's choice). The button sits beside the card link, so it never opens the plant, and reports its state with `aria-pressed`.
  * **Signed out:** the bookmark opens "Save this plant / Sign in to save plants to your Favorites" (bottom sheet on phones, dialog on larger screens). Sign in remembers the plant for 30 minutes in this tab (sessionStorage) and saves it after signing in, then returns to the same page.
  * **Unconfirmed email:** the bookmark goes back and the message offers "Resend email".
  * **Favorites page:** saved plants in the user's order; unbookmarking removes the card at once with "Removed from Favorites. Undo" (Undo restores the position); empty state with three decorative photos from the Explore feed and "Explore Plants →"; a sign-in prompt when signed out. The list is reloaded each time the page opens.
  * **Shared:** `Favorites` service (`core/favorites`), `BookmarkButton` and `SavePrompt` (`shared/bookmark`), `Snackbar` (`shared/snackbar`, one message, 6 seconds, paused on hover or focus), and a filled option on `Icon`.
  * **Tests:** 9 new frontend tests (immediate toggle and save, rollback with a message, unconfirmed email with resend, quick clicks in order — confirmed to fail without the per-plant queue, sign-in prompt keeping the save, Cancel forgetting it, remove and Undo position, empty state, signed-out page). 105 frontend tests pass; `ng build` without warnings.
  * **Not verified yet:** a manual check in the browser.
* Favorites API, Phase 6b (API_SPEC §31–34), 2026-10-23.
  * **Database:** `favorites` table (migration `AddFavorites`, applied to `plant_encyclopedia_dev`): one row per user and plant, an integer position kept 1…n per user, deleted with its user or plant.
  * **Endpoints:** `GET`, `POST` and `DELETE /api/favorites` and `PATCH /api/favorites/order`. Sign-in required; changes need the CSRF token; 60 requests per minute per user. Saving is repeat-safe (`200` with the existing favorite), needs a confirmed email, accepts an `order` for Undo, only takes Published plants and stops at 500 favorites.
  * **Safety:** every operation is limited to the signed-in user's favorites (another user's ID answers `404`); one user's changes are serialized with a PostgreSQL advisory lock.
  * **Tests:** 11 new backend tests (sign-in required, confirmed email, repeat-safe save, unpublished plants, remove and Undo position, all-or-nothing reorder, ownership, CSRF, simultaneous saves, the limit, hidden unpublished favorites). The ownership and simultaneous-save tests were confirmed to fail when the ownership filter and the lock are removed. The account tests' test browser moved to a shared `TestBrowser`. 456 backend tests pass; 0 build warnings.
* Sign-in with Google and GitHub, Phase 6a-2 (D-070), 2026-10-21. Microsoft sign-in was built and then removed at the developer's request (2026-10-22).
  * **Backend:** `GET /api/auth/providers`, `/api/auth/external/{provider}` and `/api/auth/external/callback`; providers are registered only when their keys are in User Secrets (none are configured yet, so no button shows).
  * **Matching:** a provider joins an existing account only when Google verifies the same email; GitHub emails are treated as unverified, so an address already in use is refused (no account takeover). New GitHub accounts get a confirmation email; a provider without an email is refused.
  * **Frontend:** "Continue with Google / GitHub" buttons with the providers' logos on Sign in and Create account, only for configured providers; messages for `email-in-use`, `email-required`, `locked` and `external-failed`. The dev proxy forwards `/signin-google` and `/signin-github`.
  * **Packages:** Microsoft.AspNetCore.Authentication.Google 10.0.12; AspNet.Security.OAuth.GitHub 10.0.0 (Apache-2.0).
  * **Tests:** 18 new backend tests (new account and returning sign-in, verified linking, takeover refused — confirmed to fail when the check is removed, unconfirmed new accounts, no email refused, display names, trusted claims, provider list, redirect to Google with the site callback, callback without an answer, safe return paths) and 3 new frontend tests. 445 backend and 96 frontend tests pass; 0 build warnings.
  * **Not verified yet:** a real sign-in with each provider (needs the developer's OAuth apps).
* Accounts with email + password, Phase 6a-1 (D-070), 2026-10-21.
  * **Backend:** ASP.NET Core Identity in PostgreSQL (migration `AddUserAccounts`, applied to `plant_encyclopedia_dev`); `GET /api/me` and `/api/auth/register`, `login`, `logout`, `forgot-password`, `reset-password`, `resend-confirmation`, `confirm-email` (API_SPEC §6).
  * **Security:** HttpOnly Secure SameSite=Lax session cookie (14 days, sliding); antiforgery tokens on every state-changing account request; passwords 8–128 characters; lockout after 5 failures for 15 minutes; 10 requests/minute per client; no account discovery through sign-in or reset; links valid 3 hours. Emails are written to `App_Data/emails` in Development; other environments refuse to start without an email service.
  * **Frontend:** Sign in, Create account, Forgot password, Reset password and Email confirmed pages; the sidebar shows Sign in, or the user's initial and name with Sign out; the mobile top bar shows the initial or a Sign in link. Sign-in returns only to paths inside the app.
  * **Packages:** Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.12.
  * **Tests:** 15 new backend tests (cookie flags, CSRF rejection — confirmed to fail when the check is removed, validation, 8-character minimum, taken email, same answer for unknown email and wrong password, lockout, sign-out, reset without account discovery and with a forged token, confirmation, rate limit, no passwords in logs) and 7 new frontend tests. 427 backend and 93 frontend tests pass; 0 build warnings; `ng build` without warnings.
  * **Not verified yet:** a manual check in the browser. Google and GitHub sign-in come in 6a-2.
* Cloudflare R2 image storage, Phase 11d-3, 2026-10-20 (ARCHITECTURE §19).
  * **Setup:** bucket `plant-encyclopedia-images`, an Account API token limited to Object Read & Write on it (keys in User Secrets), public reads through `https://pub-1a7886f8403245e981ef4d9f1944e825.r2.dev`. Development uses `Storage: R2`; the default is still Local.
  * **Code:** `R2ImageStorage` (AWSSDK.S3 4.0.104.5, Apache-2.0); startup validation of account ID, keys, bucket and public address; `check-r2` and `migrate-images-to-r2` commands.
  * **Live check:** `check-r2` uploaded a test file, read it back through the public address with matching bytes, and deleted it.
  * **Migration:** 89 images (267 files) moved from `App_Data/plant-images` to R2 with 0 missing files; a second run moved nothing; a moved file loads publicly as `image/webp` with the one-year cache header. The local folder is kept as a backup.
  * **Tests:** 15 new backend tests (request shape and signing, refused keys, upload failure, settings validation, migration with a fake R2); 412 backend tests pass with 0 build warnings. Tests never contact Cloudflare.
* Image review and plant-group images, 2026-10-20 (ARCHITECTURE §22, DATABASE_DESIGN §12, API_SPEC §18).
  * **Rejecting:** migration `AddPlantImageRejectedAt` (applied to `plant_encyclopedia_dev`). Rejected images are hidden from Plant Detail, cards, search, Explore and group cards; the import skips every source image already stored for the plant, so the next acceptable candidate is chosen. The startup backfill includes plants without a usable photo.
  * **Development tool:** `POST /api/dev/plants/{id}/hero-image/reject`, mapped only in Development and Testing.
  * **Authors:** Commons placeholder author text no longer counts as an author.
  * **Group cards:** `GET /api/categories/{id}` now gives each plant group the image of one of its Published plants, photos first, the same all day and different the next.
  * **Result:** the Capsicum pubescens photo (a can) was rejected with the Development endpoint on 2026-10-20 and replaced automatically by "Ripe-manzanos.jpg" (Adam mckenna, CC BY-SA 3.0, Wikimedia Commons), a fruiting plant; checked visually, not yet confirmed by the developer. Sugar maple, Peppermint and Rosa rugosa were re-picked the same way and each new image was checked visually; the developer approved the Capsicum pubescens, Rosa rugosa and Peppermint results: Sugar maple now shows green maple leaves on the tree ("Acer_saccharum_1-jgreenlee", Superior National Forest, CC BY 2.0, Commons; chosen after the developer asked for another photo than the autumn-leaf ring), Rosa rugosa the shrub with leaves and hips (iNaturalist, CC BY), and Peppermint a young mint plant ("Caybacha.jpg", public domain, Commons). Peppermint took 12 rejections, because its Commons category starts with a series of 12 microscope photos (Botanischer Garten Düsseldorf 0057–0068); a way to choose an image by hand is in TODO.
  * **Tests:** 11 new backend tests; 397 backend tests pass with 0 build warnings.
* Plant images, Phase 11d-2 (ARCHITECTURE §22–23, DATABASE_DESIGN §12, API_SPEC §14/§16).
  * **Flow:** a plant created by an import is queued; a background worker picks a photo, or an illustration when no photo is acceptable (11d-1 rules plus the Commons species category as a third photo source), downloads them from the sources' image hosts, re-encodes them to WebP at 400/800/1600 pixels wide with SkiaSharp, saves them under `App_Data/plant-images`, and records the licence code, author, source and alt text. The lookup does not wait for this.
  * **Backfill:** in Development, at startup, up to 50 species-level plants that are not Archived and have no photo are queued. Cultivars never get species images.
  * **Limits:** a separate image budget (40 requests/minute, 2,000/day); download hosts limited to the iNaturalist open-data bucket and Wikimedia upload hosts; 15 MB, 15 s; JPEG/PNG/WebP within 12,000 px a side and 50 MP; no redirects.
  * **API:** Plant Detail returns `heroImage` (photo, else illustration) with a `credit`; card `imageUrl` values are filled in search, plant groups and the lookup result. Files are served at `/media` (`.webp` only, cached for a year, `nosniff`).
  * **Frontend:** cards show the images. Plant Detail shows the image whole as the hero background over a blurred copy, fading into the page, with the names on a dark-brown panel (UX §26), and a credit line ("Photo: author · licence · source", links when known). The dev proxy forwards `/media`.
  * **Revised after review (2026-10-19):** the cropped hero and the separate illustration section were replaced as above; illustrations are only fetched and shown when there is no photo; photos are also taken from the species' Commons category (2 more read-only requests recorded under `Fixtures/Wikimedia`).
  * **Database:** migration `AddPlantImageKind` (`kind`, longer `source_id`, unique source image per plant), applied to `plant_encyclopedia_dev` on 2026-10-17.
  * **Packages:** SkiaSharp 4.153.1 (MIT) and Microsoft.Extensions.Hosting.Abstractions 10.0.12, in Infrastructure.
  * **Tests:** 36 new backend tests (image queueing on import, image processing and limits, download failures, refused hosts, budget, storage, import service, backfill selection, worker, API fields and credits, `/media` serving) and 3 new frontend tests. 378 backend and 77 frontend tests pass; 0 build warnings; `ng build` without warnings.
  * **Not verified yet:** a manual check in the browser with real downloads. EXIF orientation handling has no automated test.
* Automatic image choice, Phase 11d-1 (ARCHITECTURE §21–22). Used by the image import since 11d-2.
  * **Sources:** iNaturalist taxon photos, then the English Wikipedia article's main image (licence read from Commons), then (since 11d-2) the species' own Commons category, for the photo; the Commons category "{name} - botanical illustrations" for the illustration.
  * **Rules:** public domain, CC0, CC BY or CC BY-SA only (commercial use allowed); a known author when credit is required; JPEG, PNG or WebP with a longer side of at least 1000 pixels. The first acceptable candidate wins.
  * **Recorded results:** Rosa canina gets a CC BY-SA iNaturalist photo and a public-domain drawing; Mentha × piperita gets no photo (no usable licence or author) and a public-domain drawing.
  * **Live requests:** 10 read-only requests (iNaturalist 4, Wikipedia 2, Commons 4), approved and made once on 2026-10-17, recorded under `PlantEncyclopedia.Tests/Fixtures/INaturalist` and `Fixtures/Wikimedia`.
  * **Tests:** 49 new backend tests: request shapes, recorded parsing, licence table, credit, size and type rules, Commons filtering (restrictions, non-free, foreign hosts, unscaled thumbnails), genus redirects, failures, cancellation, source order.
  * **Total:** 342 backend tests pass with 0 build warnings. No migration, no new package, no frontend change.
* Common names for imported plants, Phase 11c.1 (ARCHITECTURE §21–22).
  * **Source:** ChecklistBank, `GET https://api.checklistbank.org/dataset/3LXR/taxon/{COL ID}/vernacular`, called by `ChecklistBankCommonNameClient` only for a plant about to be created. The GBIF taxon details endpoint returned 404, so it is not used.
    * Limits: HTTPS to `api.checklistbank.org` only, no redirects or retries, 1.5-second timeout, 1 MB limit, the same User-Agent, no request logging.
    * Every failure returns no names, and the plant is imported under its scientific name.
    * Each call uses the shared app-wide budget.
  * **What gets stored:** names in en, fr, de and ko, whichever exist, as `plant_names` Common rows (normalized, case duplicates once, at most 40). Other languages are dropped.
  * **Display name:** the English name the source lists most often (case, hyphen and spacing variants count as one); a tie goes to the only referenced one; otherwise the scientific name. Recorded results: Rosa canina → "Dog rose", Mentha × piperita → "Peppermint". The detail page shows it with the scientific name below.
  * **Live requests:** 2 read-only ChecklistBank requests (Rosa canina, Mentha × piperita), approved and made once on 2026-10-17, recorded under `PlantEncyclopedia.Tests/Fixtures/ChecklistBank`. Neither plant has Korean names there.
  * **Tests:** 25 new backend tests: client request shape, recorded parsing, failures, timeout, cancellation, dot IDs; name selection on both recordings and edge cases; budget sharing; ingestion with names, without an English name, and end to end on both recordings; no request logging.
  * **Total:** 293 backend tests pass with 0 build warnings. No migration, no new package, no frontend change.
  * **Not done:** plants imported before 11c.1 keep their scientific display name (no backfill). Manually verified in the browser on 2026-10-17 (Rosa canina → "Dog rose", Mentha piperita → "Peppermint", common-name search, no-match case).
* External plant import, Phase 11b (ARCHITECTURE §21–23, DATABASE_DESIGN §20/§23). Since 11c it is used by `POST /api/search/external`; before that it had no endpoint.
  * **Adapter:** `IPlantDataProvider` is implemented by `GbifPlantDataProvider`, which calls the GBIF v2 species match API against Catalogue of Life XR (`checklistKey 7ddf754f-…`).
    * Limits: HTTPS to `api.gbif.org` only, no redirects or retries, 3-second timeout, 1 MB limit.
    * User-Agent: the project URL.
    * Every failure returns `Unavailable`, and the searched name is never logged.
  * **Import rules:** `PlantIngestionService` imports only exact matches of accepted Plantae species, subspecies or varieties.
    * The import reuses existing plants (by identity or provider taxon), never overwrites them, and returns the winner on a concurrent `ux_plants_identity` conflict.
    * A new plant is `Published` only if its genus is in the configured 11-genus list (no `Solanum`) and that group exists, and no near-duplicate `search_key` name exists. Otherwise it is `NeedsReview`.
  * **What gets stored:** the display name is the scientific name (since 11c.1, an English common name when one stands out, see above). Provenance is a `gbif-col-xr` Taxonomy source with the COL XR ID and no URL.
  * **Package:** `Microsoft.Extensions.Http` 10.0.12 was added to Infrastructure.
  * **Live requests:** 8 read-only GBIF requests were made once on 2026-10-16 and recorded under `PlantEncyclopedia.Tests/Fixtures/Gbif`. Tests use only these recordings and fakes.
  * **Tests:** 86 new tests:
    * adapter request shape and mapping
    * each recorded match accepted or rejected as documented
    * HTTP failures, timeout, cancellation and log redaction
    * acceptance policy, genus map and options validation
    * ingestion on the test database: publish / NeedsReview rules, reuse without a provider call, nothing saved on rejection or failure, concurrent imports creating one plant, and recorded GBIF matches end to end
    * startup wiring
  * **Total:** 219 backend tests pass with 0 build warnings. No migration was added; the schema is unchanged.
* The backend was started locally and `GET /health` returned `healthy` against `plant_encyclopedia_dev` (manual check).

Verified by automated tests (23 passing in total; 7 for the Plant Detail endpoint, including an end-to-end check that a database failure returns the safe 500 body): healthy and unhealthy `/health` responses (exact JSON and status codes, no password in logs), the safe 500 error body, and that the API refuses to start without a connection string.

Not yet implemented: other feature endpoints (explore, favorites, …), request correlation IDs, structured logging, authentication.

Planned responsibilities include:

* Plant data
* Taxonomy
* Search
* Categories
* Plant groups
* Favorites
* Search history
* View history
* Identification
* Recommendations
* Similar plants
* Retailer/location information
* Authentication
* External provider integration

---

# 8. Database

## Status

**Initial schema implemented and verified locally**

Implemented:

* PostgreSQL 16 via Docker Compose (host port 5433, database `plant_encyclopedia_dev`).
* EF Core 10 with Npgsql 10.0.3 and snake_case naming (`EFCore.NamingConventions`).
* `PlantEncyclopediaDbContext` and Fluent API configurations in Infrastructure.
* Eight tables from the `InitialCreate` migration: `categories`, `plant_groups`, `taxonomies`, `plants`, `plant_names`, `plant_images`, `plant_sources`, `plant_care`.
* `AddSearchSupport` migration: `unaccent` and `pg_trgm` extensions, the `search_key()` function, and five GIN trigram indexes on name columns (DATABASE_DESIGN §43); no table changes. Applied to the Testcontainers test database on every test run (index usage verified with `EXPLAIN`) and to `plant_encyclopedia_dev` (both extensions and all five indexes present). Manually verified against the dev data: `peace rose` → Rosa 'Peace', `MR.LINCOLN` → Rosa 'Mr. Lincoln', `double` → no results (Draft only), `!!` → 400, and repeated searches → 429.
* `AddPlantIdentityKey` migration: `plant_scientific_identity()` and `plant_cultivar_identity()` functions and the unique index `ux_plants_identity` — one plant per normalized scientific name + cultivar name, across every status (DATABASE_DESIGN §59); no table or data changes. Applied to `plant_encyclopedia_dev` (index and functions verified; 17 plants unchanged, no collisions). Covered by 43 database tests: normalization outputs, rejected duplicates (case, spacing, `×`/`x`, accents, `NULL`/blank cultivar, quoted cultivars), allowed distinct plants (cultivars, species vs species + cultivar, hyphens, ranks, `Mr.`/`Mr`, `Ophélia`/`Ophelia`), any status, and a concurrent insert that waits and then fails with `unique_violation`. 133 backend tests in total.
* Local connection string via ASP.NET Core User Secrets (`ConnectionStrings:PlantEncyclopedia`).
* `dotnet-ef` 10.0.12 pinned in `backend/dotnet-tools.json`.

Verified:

* `InitialCreate` applied to `plant_encyclopedia_dev`; `dotnet ef migrations has-pending-model-changes` reports no changes.
* The migration SQL applied cleanly to a fresh temporary database. 41 constraint tests passed (CHECK, UNIQUE incl. `NULLS NOT DISTINCT` and partial indexes, FK delete behavior), run inside a rolled-back transaction.
* A one-off harness confirmed client-side UUIDv7 keys and `CreatedAt` / `UpdatedAt` maintenance through `SaveChanges`.

Automated tests (`dotnet test backend/PlantEncyclopedia.sln`, requires Docker):

* Run against a disposable PostgreSQL 16 container (Testcontainers) with a random password and port; the real migrations are applied to it on every run. A guard refuses `plant_encyclopedia_dev` and port 5433.
* 9 tests, all passing: migration applied / no pending model changes / correct test database; EF round trips for `PlantName.NameType` (`Synonym`) and `PlantSource.SourceType` (`Care`) through a fresh DbContext; CHECK constraints rejecting invalid `name_type` / `source_type` text and an out-of-range enum cast; and a documented limitation test showing `required` does not prevent `RetrievedAt = default`.

Development sample data (`backend/dev-data/seed-dev-data.sql`):

* Loaded into `plant_encyclopedia_dev`: 6 categories, 12 plant groups, 13 taxonomy rows, 17 plants (16 Published with a group, 1 Draft without), 12 alternative names (en, fr, de, ko common names; 2 scientific synonyms).
* Unverified sample content: every plant description says "Development sample data — not verified." No sources, care data, or images are included.
* Covered by 3 automated tests (applies cleanly, re-running adds nothing, refuses other databases); 16 tests pass in total.

Not yet implemented: verified/curated plant data.

### Design Background

PostgreSQL and Entity Framework Core have been selected.

The conceptual database model has been designed around:

* Plants
* Plant names
* Cultivars
* Plant groups
* Categories
* Taxonomy
* Images
* Image provenance
* Sources
* External identities
* Care information
* Blooming information
* Health information
* About/history information
* Users
* Favorites
* Search history
* View history
* Identification requests
* Identification candidates
* Similar plants
* Retailers
* Locations
* Pricing/availability information

The exact schema should be implemented according to `DATABASE_DESIGN.md`.

No assumption should be made that these entities are already implemented simply because they are documented.

---

# 9. API

## Status

**API contract defined; implementation pending**

The main API surface has been specified.

Important planned endpoints include:

```text
GET    /api/plants/{plantId}

GET    /api/categories
GET    /api/categories/{categoryId}

GET    /api/plant-groups/{plantGroupId}

GET    /api/explore

GET    /api/search?q={query}
GET    /api/search/suggestions?q={query}

GET    /api/history/searches
DELETE /api/history/searches/{searchId}

POST   /api/history/views
GET    /api/history/views

GET    /api/favorites
POST   /api/favorites
DELETE /api/favorites/{favoriteId}
PATCH  /api/favorites/order

POST   /api/identification
GET    /api/identification/{requestId}

GET    /api/plants/{plantId}/similar
GET    /api/identification/{requestId}/more

GET    /api/plants/{plantId}/retailers

GET    /health
```

These endpoints are contracts to implement, not evidence that the endpoints already exist.

---

# 10. Frontend

## Status

**Foundation implemented; features pending**

Implemented and verified:

* Angular 22 app in `frontend/` (standalone components, TypeScript, plain CSS, no SSR, no UI component or state-management library). Requires Node.js ≥ 22.22.3 or ≥ 24.15.0.
* Application shell: collapsible desktop sidebar (Identify, Explore, Favorites; disabled Login placeholder), mobile top bar with a slide-in drawer built on a native modal `<dialog>` (focus trap, Esc and outside-tap to close), skip link, and accessible names for icon-only controls. Mobile/desktop switch at 48rem.
* Routes: `/` → `/identify`; heading-only placeholders for `/identify` and `/favorites`; `/explore`, `/search`, `/categories/:categoryId`, `/plant-groups/:plantGroupId`, `/plants/:plantId`; a not-found page for unknown URLs.
* Design tokens (warm neutral / dark-brown palette, spacing, radii) as CSS variables in `src/styles.css`; reduced-motion respected.
* Visual refresh, Phase 22 (UX §2.1, §50–52), 2026-10-20:
  * **Fonts:** Nunito with Gowun Dodum for Korean, bundled through `@fontsource` (SIL OFL); only the character ranges a page uses are downloaded.
  * **Icons:** an `Icon` component with 16 outlined icons drawn for the project, in brown (`--color-icon`), replacing every emoji (sidebar, top bar, Back, Search, close, Quick Care, card placeholder).
  * **Motion:** sidebar labels grow on hover/focus; cards fade in as they come into view (`Reveal`); card images zoom slightly on desktop hover; page changes cross-fade (router View Transitions); the Plant Detail hero eases in and drifts on scroll on desktop, and Explore's title and pills slide in (GSAP 3.15 with ScrollTrigger, loaded on demand); smooth scrolling on desktop across the site (Lenis 1.3, loaded on demand).
  * **Guardrails:** only transforms and opacity are animated, nothing blocks navigation, and reduced motion or a touch screen turns the effects off or shortens them (UX §51–52). GSAP's licence is its free "standard no-charge" licence (commercial use allowed); Lenis is MIT.
  * **Size:** initial download 85.7 kB compressed; GSAP (25 kB), ScrollTrigger (17 kB) and Lenis (5 kB) are separate chunks.
  * **Tests:** 4 new frontend tests (icon, card placeholder, card fade-in, reduced motion); 86 frontend tests pass; `ng build` without warnings.
  * **Not verified:** how the motion looks and feels in a real browser, and a Lighthouse performance measurement (none was run). The card-image-to-hero transition is not implemented.
* Typed API models and `PlantApi`, `CategoryApi`, `PlantGroupApi`, `SearchApi` clients (`src/app/core/api/`).
* Explore page: a Search icon (→ `/search`), category pills (horizontal scroll, hidden scrollbar) linking to category pages, and the discovery feed as image cards linking to Plant Detail, 20 at a time by infinite scroll with a Load more fallback (duplicates skipped). Loading skeletons, "No plants to explore yet.", and errors with Try Again for the pills, the feed and a later page. Infinite scroll uses a shared `WhenVisible` directive, now also used by Search. Not implemented: restoring the feed and scroll position on Back, personalization, masonry, hover treatment, bookmarks, pill edge fades and desktop drag scrolling. 82 frontend tests pass (5 new for the feed).
* Search page at `/search?q=…`: sticky search bar with Back (fallback `/explore`) and clear; suggestions after a 250 ms pause for 2+ characters (outdated requests cancelled, failures hidden), using the ARIA combobox pattern (↑/↓, Enter, Esc); recent searches in browser localStorage only (10 max, newest first, case-insensitive de-duplication, individually removable, untrusted stored data filtered, works when storage is blocked); results as image cards linking to Plant Detail, loaded 20 at a time by infinite scroll (IntersectionObserver) with a Load more fallback, de-duplicated, stopping at page 50 with a note; loading skeleton, UX §43 no-results message with an Identify link, "Something went wrong" with Try Again, a specific rate-limit message, and client-side text validation mirroring the API. Loaded pages and scroll position are not restored on Back; bookmark controls are not shown.
* Category page at `/categories/:categoryId`: Back (in-app history, otherwise `/explore`), title, and image cards linking to that category's plant groups only. Plant Group page at `/plant-groups/:plantGroupId`: category link, title, and image cards linking to Plant Detail; 20 plants per page with a "Load more" button (appends, skips duplicates, keeps loaded plants and offers Try Again if a later page fails); Back falls back to the group's category. Both pages have loading skeletons, "No plants available yet.", not-found (404), and "Something went wrong" with Try Again. Cards show a placeholder because no images exist; bookmark, hover treatment, and masonry layout are not implemented. Loaded pages and scroll position are not restored on Back.
* Back fallback (shared by all pages with a Back button): the fallback page replaces the current history entry instead of adding one, and Back from a fallback page uses its own fallback. A directly opened page therefore walks up the hierarchy (Roses → Flowers → Explore) instead of looping back to the page just left.
* Plant Detail page at `/plants/:plantId`: Back button (in-app history, otherwise the plant's group page, or `/explore` if it has no group), hero placeholder, naming hierarchy (name, plant group, italic scientific name), description, Quick Care cards (verified care only; expandable when a description exists; temperature shown as `15–25°C` / `From 15°C` / `Up to 25°C`) or "Care information isn't available yet.". Loading skeleton, "Plant not found" (404/400), and "Something went wrong" with Try Again. Blooming, Health, About and Similar Plants are not shown yet (the Bookmark came in Phase 6c, the Where to Buy card in Phase 9b).
* Dev-server proxy (`proxy.conf.json`) forwards `/api`, `/health` and `/media` to `http://localhost:5034`; no backend CORS configuration.
* Catalogue of Life lookup on the Search page (Phase 11c, UX §43). When a name-shaped search (2–4 words of letters) finds nothing, a "Search Catalogue of Life" button is offered. It calls `POST /api/search/external` only on click.
  * States: loading, found (the plant as a card plus "Taxonomy: Catalogue of Life, via GBIF"), pending review, no match, unavailable, rate-limited, and error. Unavailable, rate-limited and error each have Try Again.
  * A new search resets the lookup.
* Plant Detail shows credits for external sources ("Taxonomy: Catalogue of Life, via GBIF") when `attributions` is not empty.
* `npm run build` succeeds without warnings; 74 unit tests pass (Vitest + jsdom, HTTP mocked).
* Manually verified end to end (browser → dev proxy → API → `plant_encyclopedia_dev`): a Published sample plant renders; the Draft plant and a malformed ID show "Plant not found"; stopping the API shows the error state and Try Again recovers; Back and the mobile layout work.

Not yet implemented: real Identify and Favorites screens, the Explore feed, images, animations, authentication.

### Design Background

The intended web frontend is Angular + TypeScript.

The intended navigation model is:

### Desktop

```text
Sidebar
├── Identify
├── Explore
├── Favorites
└── Login / Account
```

The sidebar is collapsible.

### Mobile

```text
Top App Bar
├── Menu
├── Plant Encyclopedia
└── Login / Account
```

The sidebar becomes a slide-in navigation.

There is no persistent mobile bottom navigation.

---

# 11. Explore

## Status

**Implemented: default feed (Phase 7), personalization for signed-in users (Phase 7b), feed and scroll position restored on Back (Phase 7c)**

Explore is discovery-first.

The initial Explore page should not be dominated by a search bar.

Instead:

* Explore title
* Search icon
* Category pills
* Image-first plant feed

Search is activated from the Explore search icon.

The feed should support progressive/infinite loading.

---

# 12. Explore Categories

The current categories are:

* Flowers
* Trees
* Fruits
* Vegetables
* Herbs
* Succulents

All categories should be available from the beginning.

The category selector should be horizontally scrollable where necessary.

No additional categories are currently planned.

---

# 13. Category Browsing

## Status

**Defined; implementation pending**

Category pages should remain category-pure.

For example:

```text
Explore
  ↓
Flowers
  ↓
Roses
  ↓
Rosa 'Peace'
  ↓
Plant Detail
```

Category pages show plant-group image cards rather than introducing a separate subcategory-pill system.

Example Flowers page:

```text
Roses
Tulips
Peonies
Orchids
Lilies
Hydrangeas
...
```

A plant-group page then displays individual plants/cultivars.

---

# 14. Search

## Status

**Defined; implementation pending**

Search is accessed through Explore.

Search should support:

* Common names
* Scientific names
* Cultivar names
* Plant groups
* Relevant taxonomy terms

Search suggestions should be database-backed.

Search results use an image-first grid with infinite scrolling.

Strong query relevance must take priority over personalization.

---

# 15. Plant Detail

## Status

**UX and content structure defined; implementation pending**

All plant entry points should use the same Plant Detail page.

Entry points include:

* Explore
* Search
* Identification
* Category browsing
* Favorites
* More

The intended structure is:

```text
Hero
↓
Name / Classification
↓
Quick Care
↓
Blooming
↓
Health
↓
About
↓
Where to Buy
↓
Similar Plants
```

---

# 16. Plant Detail Hero

The hero should be immersive and image-first.

It should include:

* Back button
* Bookmark
* Large plant image
* Plant naming information over the image

As the user scrolls:

```text
Image prominence
      ↓
gradually decreases

Background prominence
      ↓
gradually increases
```

The transition should feel continuous rather than creating a hard image boundary.

The background uses a warm neutral/beige direction.

---

# 17. Plant Naming

When a cultivar is known:

```text
Rosa 'Peace'
Hybrid Tea Rose
Rosa × hybrida
```

When no cultivar is available:

```text
Rose
Rosa
```

The UI prioritizes user-friendly names while retaining scientific information.

Detailed taxonomy may exist internally without being displayed prominently.

---

# 18. Quick Care

## Status

**Defined; implementation pending**

The initial Quick Care section contains four cards:

* Light
* Water
* Temperature
* Soil

Cards can expand to reveal additional information.

Example:

```text
Light
Full Sun
```

```text
Water
When the top 2–3 cm of soil is dry
```

```text
Temperature
15–25°C
```

```text
Soil
Well-drained
```

The actual values must come from validated plant data rather than invented AI output.

---

# 19. Plant Detail Content

### Blooming

Short summary.

### Health

Short summary of common issues and reliable health-related information.

### About

May contain:

* Origin
* History
* Background

The About section can be expandable.

### Growing

**Excluded from MVP**

A dedicated long-form growing manual is not currently part of the MVP.

This avoids prematurely expanding the content model into:

* Pruning
* Propagation
* Fertilization
* Winter protection
* Bouquet handling
* Other specialized growing instructions

---

# 20. Where to Buy

## Status

**Implemented (Phases 9a–9b): Nearby shops from an OpenStreetMap snapshot; no Online section yet (D-073)**

The feature will initially focus on Vancouver / British Columbia.

The intended flow is:

```text
Plant Detail
    ↓
Where to Buy
    ↓
Nearby retailers
```

Potential retailer types:

* Nurseries
* Garden centres
* Flower shops

Online retailers may be displayed separately.

The system should not claim live inventory unless a reliable inventory source is available.

Prices should be treated as approximate and associated with source/date where appropriate.

---

# 21. Location

Location permission should be requested only when entering Where to Buy or when location is genuinely required.

If permission is:

### Granted

Use the available location for nearby results.

### Denied / unavailable

Default to Vancouver, BC and allow the user to change the location manually.

The user should be able to use the rest of the application without granting location access.

---

# 22. Similar Plants

## Status

**Defined; implementation pending**

Plant Detail includes a Similar Plants section.

Initial presentation:

* Approximately 3 cards
* Horizontal carousel
* Additional cards available through scrolling
* View More opens a broader contextual discovery page

The MVP matching approach may use:

* Taxonomy
* Plant group
* Curated relationships

Visual similarity can be introduced later.

---

# 23. More

## Status

**Defined; implementation pending**

More is contextual to the current identification or plant context.

It is not:

* A fourth identification candidate
* A replacement for Explore
* A global random plant feed

For identification:

```text
#1
#2
#3
More
```

`More` is a navigation action to a broader related result set.

---

# 24. Plant Identification

## Status

**Implemented: backend (Phase 8a, Pl@ntNet), screens (Phase 8b), Catalogue of Life imports and the More page (Phase 8c)**

The intended flow is:

```text
Take a Photo / Choose from Photos
        ↓
Image Preview
        ↓
Identify
        ↓
Identification Processing
        ↓
Identification Results
```

The user sees the selected image during processing.

The primary loading message is:

> Identifying your plant…

No fake progress percentage should be displayed.

---

# 25. Identification Results

## Status

**Defined; implementation pending**

The result layout is:

```text
          ┌────────────────┐
          │                │
          │      #1        │
          │                │
          └────────────────┘

          ┌──────┬──────┬──────┐
          │  #2  │  #3  │ MORE │
          └──────┴──────┴──────┘
```

There are three actual candidate plants:

1. Highest-ranked candidate
2. Second candidate
3. Third candidate

`More` is not a fourth candidate.

No confidence percentages are displayed.

AI candidates must be validated against structured plant data before appearing as encyclopedia results.

---

# 26. Favorites

## Status

**UX defined; implementation pending**

Favorites require authentication.

The primary navigation item is a bookmark.

MVP uses one collection:

```text
All Favorites
```

The Favorites page uses an image-first grid.

Unbookmarking should remove the plant immediately and provide an Undo option where appropriate.

---

# 27. Favorites Empty State

When the user has no favorites:

* Three decorative plant images
* Images are examples only
* Images are not interactive
* CTA directs the user to Explore

The intended action is:

```text
Explore Plants →
```

Once favorites exist, the empty state disappears.

---

# 28. Favorites Arrange Mode

## Status

**Defined; implementation pending**

Mobile:

* Long press enters Arrange mode
* Cards visually indicate edit mode
* Delete/minus controls appear
* Cards can be reordered by dragging

Desktop:

* Arrange/Edit must be explicitly activated
* Do not rely on hover alone

MVP stores logical ordering rather than arbitrary pixel coordinates.

Folders are deferred.

---

# 29. Recommendation State

## Status

**Rule-based personalization implemented for signed-in users (Phase 7b, `PersonalizedFeed`)**

New or anonymous users should receive broad and diverse discovery.

Users with meaningful behavior may receive approximately:

```text
60% strongly related
20% adjacent / somewhat related
20% substantially different / novel
```

The recommendation system should use understandable rule-based logic initially.

Possible signals include:

* Viewed plants
* Bookmarked plants
* Search activity
* Categories
* Plant groups
* Recent interactions

Exact repeated plants/cultivars should not be repeatedly inserted into the same recommendation feed.

---

# 30. Plant Data Sources

## Status

**Architecture defined; integration pending**

Primary structured external sources currently planned:

* iNaturalist
* GBIF

The application's own database remains the user-facing source of truth.

External data should follow:

```text
External Source
    ↓
Retrieve
    ↓
Normalize
    ↓
Validate
    ↓
Persist
    ↓
Serve from Application Database
```

Raw provider responses should not be directly exposed as encyclopedia content.

---

# 31. Image Data

## Status

**Design defined; implementation pending**

The application should distinguish between:

* Plant data
* Image data
* Image provenance
* External image identity
* Persistent application image storage

Images should be optimized for application delivery.

Planned performance techniques include:

* Thumbnails
* Responsive images
* Lazy loading
* WebP/AVIF where appropriate
* CDN/object storage where appropriate

---

# 32. AI Identification

## Status

**Implemented in the backend (Phase 8a): Pl@ntNet behind `IPlantIdentificationProvider`, rate limit, budget and cache**

AI should primarily answer:

> "What plant candidates might this image represent?"

It should not become the authoritative encyclopedia database.

The conceptual flow is:

```text
Image
 ↓
Validation
 ↓
Image Hash / Cache
 ↓
AI Provider
 ↓
Candidate Ranking
 ↓
Structured Data Validation
 ↓
Results
```

The AI provider should be replaceable.

Rate limiting and cost protection are required before exposing image identification publicly.

---

# 33. Authentication

## Status

**Planned; implementation pending**

Authentication is required for Favorites.

Other anonymous features should remain usable where practical.

The exact authentication provider and implementation details should follow the architecture and API decisions rather than being invented during feature development.

---

# 34. Search History

## Status

**Implemented for signed-in users (Phase 7a); used by personalized Explore (Phase 7b)**

Search History represents explicit user search activity.

It is separate from View History.

Recent searches should:

* Be displayed when search has no active query
* Be individually removable
* Stop directly influencing recommendations when removed

Removing a search history item does not delete the underlying plant from the database.

---

# 35. View History

## Status

**Recorded for signed-in users (Phase 7a); used by personalized Explore (Phase 7b)**

View History records plant-detail interactions and may contribute to recommendations.

Anonymous history may be stored locally or associated with an anonymous identifier where appropriate.

Authenticated history may be associated with the user's account.

---

# 36. Loading States

## Status

**Defined**

General data loading:

**Skeleton UI**

Plant Detail:

**Structured skeleton**

The skeleton should reflect the expected page structure, including:

* Hero
* Name
* Quick Care
* Content sections

Identification:

**Keep the selected image visible with a short loading message.**

Do not use fake progress bars.

---

# 37. Empty States

## Status

**Defined**

### Search

```text
No plants found

We couldn't find a match for "[query]".
Try another name or search term.
```

An identification option may be offered where appropriate.

### Category / Plant Group

```text
No plants available yet.
```

Do not replace an empty category with unrelated plants.

### Favorites

Use the dedicated visual empty state described above.

---

# 38. Error States

## Status

**Defined**

Errors should be contextual and recoverable.

General example:

```text
Something went wrong

We couldn't load the plants.

Please check your connection and try again.

[Try Again]
```

Identification example:

```text
We couldn't identify this plant

The image may be unclear, or the plant may not be in our database yet.

[Try Again]
[Choose Another Photo]
```

The application must not claim that a plant does not exist merely because identification failed.

---

# 39. Accessibility

## Status

**Requirements defined; implementation pending**

Required considerations include:

* Keyboard navigation
* Accessible labels
* Meaningful alt text
* Sufficient contrast
* Adequate touch targets
* Non-color-only communication
* Reduced-motion support
* Accessible alternatives to hover-only behavior

Accessibility must be considered during implementation rather than postponed entirely until final polish.

---

# 40. Performance

## Status

**Requirements defined; implementation pending**

The current performance direction includes:

* Responsive images
* WebP/AVIF where appropriate
* Thumbnail generation
* Lazy loading
* CDN where appropriate
* Progressive feed loading
* GPU-friendly animation
* Minimal layout recalculation
* Fast core navigation

Exact numerical performance budgets have not yet been finalized.

Performance should take priority over decorative effects.

---

# 41. Visual Interaction Direction

## Status

**Implemented in part: two themes, title rises, card hover motion, smooth desktop scrolling (D-074, Phase 22); no custom cursor or falling leaves**

The visual direction is modern, clean, image-first, and interactive.

Subtle botanical effects may include:

* Desktop cursor interaction
* Occasional falling leaves
* Scroll-driven hero transitions
* Card hover effects
* Bookmark transitions

These are decorative enhancements, not core functionality.

They must respect reduced-motion preferences.

---

# 42. Mobile Interaction Direction

## Status

**Defined; implementation pending**

Mobile uses:

* Slide-in sidebar
* Top app bar
* Two-column masonry-style plant grids
* Tap-based navigation
* Bottom sheets for contextual actions where appropriate
* Long press for Favorites arrangement

Hover-dependent interactions must not be required on mobile.

---

# 43. Known Non-Goals

The following are intentionally not current priorities:

* iOS development before a Mac environment is available
* Microservices
* Kubernetes
* Advanced ML recommendation systems
* Live retailer inventory without reliable data
* Full growing manuals
* Pet safety information without reliable sources
* Google Images as a product data source
* Complex Favorites folder systems
* Excessive decorative animation

These may be reconsidered later if the project develops a clear need.

---

# 44. Current Implementation Gaps

The following major implementation areas remain:

* [ ] Repository/project setup
* [-] ASP.NET Core backend foundation (solution, layers, `/health`, error handling, and tests exist; feature endpoints pending)
* [x] Angular frontend foundation (app shell, routes, design tokens, dev proxy)
* [x] PostgreSQL database setup (local Docker Compose)
* [x] EF Core configuration
* [x] Initial database migrations
* [x] Plant domain/data models (initial eight entities)
* [-] Seed/curated plant data (unverified development sample data loaded locally; curated data pending)
* [x] Plant Detail API (`GET /api/plants/{plantId}`)
* [-] Plant Detail UI (names, description, Quick Care, loading/not-found/error states; other sections pending)
* [x] Search API (`GET /api/search`, `GET /api/search/suggestions`)
* [x] Search UI (browser-only recent searches; bookmarks pending)
* [-] Category browsing (API, Explore category pills, Category page; images pending)
* [-] Plant-group browsing (API and Plant Group page with Load more; images, bookmark, and hover pending)
* [ ] External plant data ingestion
* [ ] Authentication
* [ ] Favorites
* [ ] Explore feed
* [ ] Recommendation logic
* [ ] Identification pipeline
* [ ] AI provider integration
* [ ] Where to Buy
* [ ] Similar Plants
* [-] Automated tests (PostgreSQL persistence tests exist; API, unit, and frontend tests pending)
* [ ] Accessibility validation
* [ ] Docker configuration
* [ ] GitHub Actions CI
* [ ] AWS deployment
* [ ] Production monitoring/observability
* [ ] Portfolio polish

The exact implementation order is defined in `DEVELOPMENT_ROADMAP.md`.

---

# 45. Current Reality vs Planned State

AI coding agents must use the following distinction:

### "Defined"

The behavior or design has been documented.

### "Planned"

The feature is intended but has not necessarily been implemented.

### "Implemented"

The feature exists in the repository and has been validated.

### "Verified"

The feature has been tested sufficiently to support the relevant claim.

A documented feature must never be described as implemented unless the repository confirms it.

---

# 46. How This Document Should Be Updated

Update this document when:

* A major feature becomes implemented
* A major architecture component is established
* Database migrations are created
* API endpoints become available
* External providers are integrated
* Authentication is implemented
* Deployment becomes functional
* A significant product decision changes
* A planned item is intentionally removed

Do not update this document merely because something appears on the roadmap.

The document should represent the actual repository state.

---

# 47. Current Next Step

The project should now move from documentation toward implementation.

The immediate implementation direction should follow the earliest unfinished stage in `DEVELOPMENT_ROADMAP.md`.

Before beginning implementation:

1. Confirm the repository structure.
2. Establish the backend foundation.
3. Establish the database connection.
4. Establish the frontend foundation.
5. Create the first minimal end-to-end vertical slice.
6. Validate the architecture against real code.
7. Continue incrementally.

The first implementation should favor a small working slice over building every layer in isolation for a long period.

---

# 48. Guiding Principle

The most important purpose of `CURRENT_STATE.md` is truthfulness.

It should always answer:

> "What actually exists right now?"

not:

> "What do we eventually want to have?"

When in doubt, prefer describing the project as **not yet implemented** rather than implying that a planned feature already exists.
