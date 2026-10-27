# TODO

## 1. Purpose

This document contains the actionable work remaining for the Plant Encyclopedia project.

It is derived from:

* `PRODUCT_REQUIREMENTS.md`
* `UX_SPECIFICATION.md`
* `DECISIONS.md`
* `ARCHITECTURE.md`
* `DATABASE_DESIGN.md`
* `API_SPEC.md`
* `DEVELOPMENT_ROADMAP.md`
* `CURRENT_STATE.md`

`TODO.md` is an execution document.

It should answer:

> What should be worked on next?

It should not redefine product requirements or architecture.

---

# 2. How to Use This Document

Tasks should be completed in dependency order unless there is a deliberate reason to change the order.

Use the following status markers:

* `[ ]` Not started
* `[-]` In progress
* `[x]` Completed
* `[~]` Deferred
* `[!]` Blocked

When a task is completed:

1. Mark it `[x]`.
2. Update `CURRENT_STATE.md` if the implementation state materially changed.
3. Update the relevant specification if the actual behavior differs from the documented behavior.
4. Add newly discovered follow-up work to this document.

Do not mark a task complete simply because code was written.

A task should be considered complete when its acceptance criteria have been satisfied.

---

# 3. Immediate Priority

The project is currently transitioning from documentation to implementation.

The immediate priority is:

```text id="qg5g6x"
Project Foundation
        ↓
Backend Foundation
        ↓
Database Foundation
        ↓
Frontend Foundation
        ↓
First End-to-End Vertical Slice
        ↓
Core Encyclopedia
```

Do not begin advanced AI, recommendation, retailer, or cloud infrastructure work before the core application foundation is stable.

---

# 4. Phase 0 — Project Foundation

## Repository

* [ ] Create/confirm repository structure
* [ ] Initialize Git repository if not already initialized
* [ ] Create appropriate `.gitignore` files
* [ ] Add root-level README
* [ ] Confirm project documentation is stored in the agreed location
* [ ] Confirm development environment requirements

## Development Environment

* [ ] Install/verify required .NET SDK
* [x] Install/verify Node.js (v24.21.0)
* [x] Install/verify Angular CLI (22.2.2, project-local via `npx` / `npm run`)
* [ ] Install/verify PostgreSQL/Docker development environment
* [ ] Install/verify Git
* [ ] Confirm IDE/editor configuration
* [ ] Confirm local environment variables strategy

## Initial Repository Structure

Establish a clean high-level structure similar to:

```text id="h8w2zn"
plant-encyclopedia/
├── backend/
├── frontend/
├── mobile/
├── docs/
└── README.md
```

The exact structure may change if the implementation provides a better justified organization.

---

# 5. Phase 1 — Backend Foundation

## ASP.NET Core

* [x] Create ASP.NET Core solution
* [x] Create backend projects according to `ARCHITECTURE.md`
* [ ] Establish Presentation layer
* [ ] Establish Application layer
* [ ] Establish Domain layer
* [ ] Establish Infrastructure layer
* [ ] Configure dependency injection
* [ ] Configure application settings
* [ ] Configure development environment
* [x] Add basic exception handling
* [x] Add API health endpoint
* [ ] Add basic logging

## Backend Quality

* [ ] Establish formatting conventions
* [ ] Establish nullable reference type policy
* [ ] Establish analyzer/linting configuration
* [x] Establish test project structure
* [x] Add first backend test

## Acceptance Criteria

* [x] Backend starts locally (manual check: `/health` returned `healthy` against the dev database)
* [x] `/health` responds successfully (verified by automated API tests)
* [x] Layers have clear dependency direction (architecture review, TODO #12)
* [x] No unnecessary architectural complexity has been introduced (architecture review, TODO #12)
* [x] A basic automated test runs successfully

---

# 6. Phase 2 — PostgreSQL and EF Core

## Database Setup

* [x] Configure PostgreSQL
* [x] Configure EF Core
* [x] Create `DbContext`
* [x] Configure database connection through environment/configuration
* [x] Verify local database connectivity

## Initial Domain Model

Implement the minimum useful plant model first.

* [x] Plant entity
* [x] Plant name data
* [x] Plant group relationship
* [x] Category relationship
* [x] Scientific/taxonomic information
* [x] Basic image relationship
* [x] Source/provenance relationship
* [x] Plant care (Quick Care) data

Do not implement every future entity before the first vertical slice works.

## Migrations

* [x] Create initial EF Core migration
* [x] Apply migration locally
* [x] Verify schema
* [x] Test migration on a clean database
* [x] Commit migration files

## Database Quality

* [x] Add appropriate primary keys
* [x] Add foreign keys
* [x] Add required constraints
* [x] Add appropriate indexes
* [x] Review delete behavior
* [x] Verify uniqueness rules
* [x] Avoid unnecessary denormalization

---

# 7. Phase 3 — Core Plant Data

## Initial Dataset

Create a small curated development dataset.

Unverified sample data: `backend/dev-data/seed-dev-data.sql` (loaded locally, covered by tests).

* [x] Define initial plant records (17 sample plants: 16 Published, 1 Draft)
* [x] Add categories
* [x] Add plant groups
* [x] Add common names (en, fr, de, ko)
* [x] Add scientific names (including 2 synonyms)
* [x] Add cultivar information where applicable (roses, apples)
* [-] Add initial images (11d-2: species-level plants get images automatically; the sample cultivars have none)
* [x] Add image provenance (source, source page, licence code, author, retrieval time)
* [ ] Add basic care information (deferred: needs sourced data; no AI-written care values)
* [ ] Add blooming information
* [ ] Add health information
* [ ] Add About information

The development dataset should be sufficient to test:

* Search
* Category browsing
* Plant groups
* Plant Detail
* Similar plants
* Explore

Do not wait for a massive encyclopedia dataset before implementing the application.

---

# 8. Phase 4 — First Vertical Slice

The first meaningful end-to-end feature should be:

```text id="y3ykzv"
Database
   ↓
Backend API
   ↓
Angular frontend
   ↓
Plant Detail
```

## Plant Detail API

* [x] Implement `GET /api/plants/{plantId}`
* [x] Create response DTO
* [x] Implement validation
* [x] Implement not-found behavior
* [x] Add mapping from domain/data model to DTO
* [x] Add API tests
* [x] Verify response against `API_SPEC.md` (§14 updated to the implemented fields)

## Plant Detail UI

* [x] Create Angular Plant Detail route (`/plants/:plantId`)
* [x] Implement back navigation
* [-] Implement hero (placeholder block; image display ready but no images exist yet; scroll transition pending)
* [x] Implement plant naming hierarchy
* [x] Implement Quick Care cards
* [ ] Implement Blooming (needs API data)
* [ ] Implement Health (needs API data)
* [ ] Implement About (needs API data)
* [ ] Implement Similar Plants placeholder/initial implementation
* [x] Implement loading state
* [x] Implement error state
* [x] Implement responsive layout
* [ ] Implement bookmark UI state (needs authentication / Favorites)

## Acceptance Criteria

* [x] A plant can be retrieved from PostgreSQL
* [x] API returns a stable DTO
* [x] Angular can consume the API (manual check through the dev proxy)
* [x] Plant Detail renders actual database data (manual check with the development sample data)
* [x] Loading state works (automated tests)
* [x] Error state works (automated tests)
* [x] Back navigation works (automated tests)
* [x] Mobile and desktop layouts are usable (manual check)

This vertical slice should validate the architecture before a large amount of functionality is built.

---

# 9. Phase 5 — Category and Plant Group Browsing

## Categories

* [x] Implement `GET /api/categories`
* [x] Implement `GET /api/categories/{categoryId}`
* [x] Implement category page
* [-] Display category pills (on Explore; edge fades and desktop drag scrolling pending)
* [x] Implement category navigation
* [x] Preserve category-pure behavior (API enforced and tested; Category page shows only the API's groups)

## Plant Groups

* [x] Implement `GET /api/plant-groups/{plantGroupId}` (Published plants only, paginated)
* [x] Implement plant-group page (20 per page, "Load more")
* [-] Display plant/cultivar image cards (placeholder image until images exist)
* [x] Implement navigation to Plant Detail
* [ ] Restore loaded pages and scroll position on Back navigation (UX §15, "where practical")

## Interaction

* [-] Implement image-first cards (shared card with image placeholder; real images pending)
* [ ] Implement bookmark affordance
* [ ] Implement desktop hover state
* [x] Implement mobile tap behavior (cards are plain links)
* [-] Implement masonry/grid behavior (responsive grid done; masonry pending)

---

# 10. Phase 6 — Search

## Search API

* [x] Implement `GET /api/search`
* [x] Implement query validation (length, control characters, letter/digit, word count, NFKC)
* [x] Search common names (all languages) and synonyms
* [x] Search scientific names
* [x] Search cultivar names
* [x] Search plant groups where appropriate
* [x] Implement relevance ordering
* [x] Add pagination/infinite-scroll support (page-based, page ≤ 50)
* [x] Case-, accent-, and punctuation-insensitive matching (`search_key()` + trigram indexes, migration `AddSearchSupport`)
* [x] Rate limit (30/min per IP), 5-second timeout, search text excluded from logs
* [x] Apply `AddSearchSupport` to `plant_encyclopedia_dev`
* [ ] Configure forwarded headers with a known-proxy list when deployment is decided, so rate limits see real client IPs
* [ ] Shared rate-limit store (e.g. Redis) if the API runs on more than one instance (the built-in limiter is per process)

## Suggestions

* [x] Implement `GET /api/search/suggestions` (max 8; 120/min per IP; 2-second timeout)
* [x] Implement prefix/exact matching (word-start, case/accent/punctuation-insensitive)
* [x] Return database-backed suggestions
* [x] Avoid excessive external API requests while typing (no external calls; frontend debounce in Search UI)
* [ ] Suggest taxonomic terms (genus, family)
* [ ] Measure suggestion query plans with realistic data volume (trigram index use inside the combined query)

## Search UI

* [x] Implement Explore search activation (🔍 icon → `/search`)
* [x] Implement sticky search bar
* [x] Implement back navigation
* [x] Implement recent searches (browser-only localStorage, 10 max; server-side history in Phase 8)
* [x] Implement search suggestions while typing (250 ms pause, 2+ characters, keyboard combobox)
* [x] Implement search result grid (bookmark controls pending Favorites)
* [x] Implement loading skeleton
* [x] Implement no-results state
* [x] Implement infinite scrolling (with Load more fallback; stops at page 50)
* [ ] Restore loaded pages and scroll position on Back to search results (UX §15)
* [ ] Move recent searches to server-side Search History when authentication exists (Phase 8)

---

# 11. Phase 7 — Explore

## Explore API

* [x] Implement `GET /api/explore`
* [x] Establish initial feed composition (categories and plant groups take turns; plants with images first)
* [x] Add category diversity
* [x] Prevent duplicate plant/cultivar cards
* [x] Support pagination/infinite scrolling
* [x] Restore the Explore feed and scroll position on Back (UX §15; 7c)
* [x] Manually check Back to Explore in the browser (automated browser QA, 2026-10-27: desktop; a bug was fixed)
  - Priority: Medium

## Anonymous/New User Feed

* [x] Implement broad category representation
* [-] Implement curated/diverse discovery (rule-based order; no hand-curated picks yet)
* [x] Avoid popularity-only recommendations
* [x] Avoid repeated plants

## Behavioral Personalization

* [x] Implement view signals (7b, signed-in users)
* [x] Implement bookmark signals
* [x] Implement search signals (whole search text matched to plant groups)
* [x] Implement plant-group signals
* [x] Implement category signals (a category's interest is the sum of its groups')
* [x] Implement approximate 60/20/20 recommendation philosophy (6/2/2 in every 10 cards)
* [x] Add diversity safeguards (turn-taking inside each kind, each plant once, seen plants later)
* [ ] Manually check the personalized feed in the browser
  - Priority: High
* [ ] Cache the personalized order if the catalogue grows large
  - Why: Every page request loads all feed plants to order them
  - Priority: Low (until the catalogue is much larger)

Start with transparent rule-based recommendation logic.

Do not build a machine-learning recommendation system at this stage.

---

# 12. Phase 8 — Search and View History

## Search History

* [x] Implement search history model (7a: signed-in users only)
* [x] Implement `GET /api/history/searches`
* [x] Implement deletion endpoint
* [x] Store recent searches (server for signed-in users, browser otherwise)
* [x] Remove recommendation influence when explicitly deleted (7b)
* [ ] Check history in the browser (the `AddHistory` migration is applied to the development database)
  - Priority: High

## View History

* [x] Implement view history model
* [x] Implement `POST /api/history/views`
* [~] Implement `GET /api/history/views` (view history is not shown in the MVP, PRD §17)
* [x] Record meaningful Plant Detail views (signed-in users; repeats within 30 minutes count once)
* [x] Use view history as a recommendation signal (7b)

## Anonymous State

* [x] Decide implementation for anonymous identifiers (no server-side history for visitors who are not signed in; developer's choice, 2026-10-25)
* [~] Store anonymous history safely (not stored on the server; recent searches stay in the browser)
* [x] Define behavior when anonymous user authenticates (browser recent searches stay in the browser and are not uploaded)

---

# 13. Phase 9 — Authentication

## Authentication Foundation

* [x] Select/confirm authentication approach (D-070: ASP.NET Core Identity, session cookie)
* [x] Implement authentication (6a-1: email + password; 6a-2: Google, GitHub)
* [ ] Create the Google and GitHub OAuth apps, add their keys to User Secrets, and sign in with each in the browser
  - Priority: High
* [ ] Let a signed-in user link another provider to their account (instead of "email in use")
  - Priority: Low
* [x] Implement user identity model (`users`, `user_logins`, `user_claims`, `user_tokens`)
* [x] Implement authorization (cookie authentication; 401 JSON for APIs)
* [x] Protect authenticated endpoints (account endpoints, Favorites)
* [x] Add authentication tests
* [-] Manually verify sign-up, confirmation, sign-in, sign-out and password reset in the browser (automated browser QA, 2026-10-27: sign-up and confirmation pass; sign-out and password reset still to check)
  - Priority: High
* [ ] Real email service for confirmation and reset emails (e.g. Amazon SES or Resend)
  - Priority: Before public release
* [ ] Check new passwords against known breached passwords
  - Why: The minimum is 8 characters (D-070); current NIST guidance recommends 15 for passwords used alone
  - Priority: Before public release
* [ ] Persist ASP.NET Core data protection keys outside the user profile
  - Why: They sign session cookies and email links; production instances must share and protect them
  - Priority: Before deployment

## UX

* [x] Implement login entry
* [x] Implement account state
* [ ] Handle unauthenticated actions
* [ ] Preserve intended action where appropriate

Do not make authentication a prerequisite for unrelated anonymous discovery features.

---

# 14. Phase 10 — Favorites

## Backend

* [x] Create Favorite entity
* [x] Implement `GET /api/favorites`
* [x] Implement `POST /api/favorites`
* [x] Implement `DELETE /api/favorites/{favoriteId}`
* [x] Implement `PATCH /api/favorites/order`
* [x] Enforce authorization
* [x] Prevent duplicate favorites

## Frontend

* [x] Implement Favorites page
* [x] Implement bookmark state
* [x] Implement save-to-favorites flow
* [x] Implement login-required bottom sheet/modal
* [x] Implement immediate removal
* [x] Implement Undo snackbar
* [x] Implement empty state
* [-] Manually verify bookmarks, the sign-in prompt, Undo, the empty state and Arrange mode (drag, long press, jiggle) in the browser (phone and desktop) (automated browser QA, 2026-10-27: bookmarks, prompt, Arrange move buttons and Undo pass; drag, long press and jiggle still need a touch device)
* [x] Implement Arrange mode

## Arrange Mode

* [ ] Mobile long-press entry
* [ ] Desktop explicit Arrange/Edit action
* [ ] Delete controls
* [ ] Drag-to-reorder
* [ ] Persist logical order

---

# 15. Phase 11 — External Plant Data Ingestion

## Provider Abstraction

* [x] Define plant data provider interface (`IPlantDataProvider`, 11b)
* [x] Implement iNaturalist provider (11d: taxon photos as an image source)
* [-] Automatic plant images (a photo; a botanical illustration when there is no photo)
  - Done (11d-1): sources (iNaturalist, Wikipedia, Commons), licence policy (PD, CC0, CC BY, CC BY-SA), automatic choice
  - Done (11d-2): `kind` column, download and checks, WebP sizes, local storage, background worker with its own budget, startup backfill, API fields, cards, whole-image detail hero with credits, Commons species photos, illustrations only as a fallback
  - Next: manually verify in the browser with real downloads (Priority: High)
  - Done (11d-3): Cloudflare R2 storage; existing images moved
* [ ] Serve R2 images from a custom domain instead of `r2.dev` before production
  - Why: Cloudflare rate-limits `r2.dev` and does not recommend it for production
  - Priority: Before deployment
* [ ] Choose a plant's image by hand from its candidates
  - Why: Automatic choice cannot see what a photo shows; Sugar maple (seeds), Peppermint, Rosa rugosa and Dog rose (a stem with one thorn) still have unsuitable images
  - Shape: list a plant's acceptable candidates with thumbnails, pick one, store it; needs authorization before it leaves Development
  - Priority: Medium (also listed under Future features in README.md)
* [x] Reject a wrong image and take the next candidate (`rejected_at`, Development endpoint)
* [x] Plant-group card images (one plant's image per group, stable for a day)
* [ ] Verify the iNaturalist photo page URL pattern before linking credits to it
  - Why: The API returns no photo page URL; none is invented
  - Priority: Medium
* [x] Implement GBIF provider (11b: v2 species match against Catalogue of Life XR; enabled only in Development; used by `POST /api/search/external` since 11c)
* [x] Isolate provider-specific response models (GBIF types are `internal` to Infrastructure)
* [x] Implement provider error handling (timeouts, HTTP errors, redirects, invalid or oversized responses, network errors → `Unavailable`)
* [x] Import common (vernacular) names (11c.1: ChecklistBank `3LXR` vernacular endpoint; en, fr, de, ko; English display name when one stands out)
* [ ] Verify the ChecklistBank API terms of use before public release
  - Why: Common names are fetched from `api.checklistbank.org`; only the Catalogue of Life data licence has been noted so far
  - Priority: High (before public release)
* [ ] Backfill common names for plants imported before 11c.1
  - Priority: Low (only development imports exist)
* [ ] Follow synonyms to the accepted name (e.g. `Lycopersicon esculentum` → `Solanum lycopersicum`)
  - Why: 11b rejects synonym matches
  - Priority: Medium
* [ ] Verify the public COL XR taxon page URL pattern, then fill `plant_sources.source_url` for imports
  - Priority: Medium
* [ ] Verify the Catalogue of Life licence and exact citation (CC BY 4.0 "unless otherwise indicated"; per-release DOI) before public release
  - Priority: High (before public release)

## Normalization

* [x] Normalize names (scientific names: NFC, whitespace, authorship removed, hybrid sign and rank markers kept)
* [x] Normalize taxonomy (kingdom → genus, plus the full species / subspecies / variety name)
* [x] Normalize plant groups (configured genus → plant group list, `ExternalPlantData:GenusPlantGroups`)
* [x] Normalize images (WebP at 400/800/1600 px, metadata dropped)
* [ ] Persistent image queue
  - Why: Queued plants are lost on restart; the startup backfill only covers plants with no image at all
  - Priority: Low (until production)
* [x] Linux native assets for SkiaSharp (`SkiaSharp.NativeAssets.Linux.NoDependencies`) before deploying to Linux (added 2026-10-27; the backend tests pass on the Linux CI runner, run 40464368936)
  - Priority: Before deployment
* [x] Store external identifiers (`plant_sources.external_id`, provider `gbif-col-xr`)
* [x] Store provenance (Taxonomy source; no source URL yet)
* [x] Store retrieval timestamps

## Validation

* [x] Validate external records before persistence (`ExternalTaxonPolicy`: exact, accepted Plantae species/subspecies/variety; column limits)
* [x] Detect likely duplicates (same `search_key`, different identity → `NeedsReview`)
* [x] Prevent duplicate cultivars/plants (identity key `ux_plants_identity`; imports reuse the existing plant, including after a concurrent conflict)
* [x] Handle incomplete provider data (missing or malformed fields are rejected and nothing is saved)

## Search Integration

* [x] Query local DB first (search never calls GBIF; the lookup checks stored plants before any external request)
* [x] Query external providers only when appropriate (user click, name-shaped text, 5/min per IP, 30/min and 1,000/day app-wide, 24-hour negative cache)
* [x] Persist validated results (through `PlantIngestionService`)
* [x] Return application-owned data (`found` / `pendingReview` / `notFound`; no provider details)
* [x] Manually verify the Catalogue of Life lookup end to end in the browser (Development, real GBIF and ChecklistBank; confirmed by the developer on 2026-10-17)
* [ ] Review tool for `NeedsReview` imports
  - Why: Imports without a mapped plant group, or with a near-duplicate name, stay hidden ("pendingReview") until someone can publish them
  - Priority: Medium
* [ ] Shared store for rate limits, the GBIF call budget and the negative cache
  - Why: All are in memory per API instance
  - Priority: Before running more than one instance

---

# 16. Phase 12 — Plant Identification

## Image Upload

* [x] Implement image selection (8b)
* [x] Implement camera/photo selection flow (Take a Photo opens the back camera on phones)
* [x] Implement image preview
* [x] Implement Retake
* [x] Implement Choose Another Photo
* [x] Validate image type (8a: from the bytes)
* [x] Validate image size (8a: 10 MB)
* [x] Validate image dimensions where appropriate (8a: 64–16,000 px, scaled decoding)

## Identification API

* [x] Implement `POST /api/identification` (8a)
* [x] Define identification request model (multipart `image`)
* [x] Define identification candidate model (`rank`, `plant`)
* [x] Implement validation
* [x] Implement error handling (`422 IDENTIFICATION_FAILED`, `503 IDENTIFICATION_UNAVAILABLE`)
* [x] Implement rate limiting (6 per minute per client; app-wide 20 per minute, 400 per day)

## AI Provider

* [x] Define AI provider abstraction (`IPlantIdentificationProvider`)
* [x] Implement selected vision API provider (Pl@ntNet, D-071)
* [x] Secure API credentials (User Secrets; never logged)
* [x] Handle provider failures
* [x] Prevent excessive usage/cost (budget, cache, rate limit)
* [x] Create a Pl@ntNet account and key, add it to User Secrets, and make an approved live identification (2026-10-25: response format confirmed; non-plant images come back as 200 with low scores)
* [ ] Confirm Pl@ntNet's error status codes (still undocumented and unseen; all are treated as unavailable)
  - Priority: Low
* [x] Fix the development setup: the dev proxy now reaches the API over HTTPS (run the API with the `https` launch profile); OAuth return addresses keep the public http://localhost:4200 scheme (2026-10-25)
* [ ] Review Pl@ntNet's terms of use (free plan, non-commercial conditions, query logging) before public release
  - Priority: Before public release

## Candidate Pipeline

* [x] Generate candidates
* [x] Normalize candidate names
* [x] Match candidates against own database (scientific identity, Published only)
* [x] Rank validated candidates (provider order; species before cultivars)
* [x] Return maximum three actual candidates
* [x] Keep More separate from candidates (8c: More page outside the ranked list)
* [x] Decide whether suggestions missing from the encyclopedia may be imported through Catalogue of Life (D-072: yes, published without a group when the genus is unmapped)
* [ ] Assign plant groups to plants published without one (part of the review tool)
  - Priority: Medium

## Result UI

* [x] Implement identification loading state (8b)
* [x] Keep selected image visible while processing
* [x] Implement #1 candidate
* [x] Implement #2 candidate
* [x] Implement #3 candidate
* [x] Implement More (8c: `/identify/more`, related plants by group, genus, family and category)
* [x] Implement failure state
* [x] Navigate candidates to Plant Detail
* [ ] Check Identify and the results in a real browser on a phone and desktop, with a photo that matches an encyclopedia plant
  - Priority: High

---

# 17. Phase 13 — Similar Plants and More

## Similar Plants

* [ ] Implement similarity relationship/model
* [ ] Implement `GET /api/plants/{plantId}/similar`
* [ ] Implement initial taxonomy/curated matching
* [ ] Implement carousel
* [ ] Implement View More

## More

* [x] Implement contextual identification results (8c: related to the candidates)
* [~] Implement `GET /api/identification/{requestId}/more` if required (not needed: `GET /api/identification/related?plantId=…` takes the candidates, since identifications are not stored)
* [x] Preserve identification context (candidate IDs in the More page address)
* [x] Prevent duplicate candidates (each plant once; candidates left out)
* [x] Keep More distinct from Explore

---

# 18. Phase 14 — Where to Buy

## Retailer Data

* [x] Define retailer entity (9a: `retailers`, one row per shop location, D-073)
* [x] Define location entity (address and coordinates on the same row)
* [x] Define retailer type (`Nursery`, `GardenCentre`, `FlowerShop`)
* [x] Define source/provenance (`source`, `source_id`, `retrieved_at`; OpenStreetMap attribution)
* [~] Define approximate pricing model (no price source exists)
* [~] Define availability model if reliable data becomes available (always "unknown" for now)
* [ ] Check OpenStreetMap's ODbL share-alike terms for the stored shop table before public release
  - Priority: Before public release
* [ ] Refresh the shop snapshot periodically (`import-retailers`)
  - Priority: Low

## API

* [x] Implement `GET /api/plants/{plantId}/retailers` (9a)
* [x] Implement distance sorting
* [~] Implement rating sorting where supported (OpenStreetMap has no ratings)
* [x] Implement relevance sorting (shop kinds by plant category)

## Location

* [x] Implement location permission flow (9b: asked only on Where to Buy, once per visit)
* [x] Handle permission denial (note and fallback)
* [x] Implement Vancouver, BC fallback
* [x] Implement manual location change (towns in the shop data)
* [x] Avoid unnecessary location collection (rounded to about 100 m, memory only, never stored or logged)

## UX

* [x] Implement Where to Buy entry card (9b)
* [x] Implement dedicated Where to Buy page
* [~] Separate nearby and online retailers (Online left out until there is a reliable source, D-073)
* [x] Avoid unsupported live inventory claims
* [~] Display approximate pricing with appropriate source/date information (no price source)
* [-] Check Where to Buy in a browser on a phone and desktop (permission prompt, chooser, list) (automated browser QA, 2026-10-27: granted and refused location, town chooser, list; a real phone still to check)
* [ ] Decide whether florists should rank below garden centres and nurseries for flowers that are shrubs (e.g. Dog rose downtown shows only florists, nearest first)
  - Priority: Medium
* [ ] Consider answering `GET /api/me` with `200 { "data": null }` when signed out, so browsers do not log an error on every page
  - Why: Chrome logs every 401 response as a console error
  - Priority: Low (changes the API contract)
  - Priority: High

---

# 19. Phase 15 — Mobile App

## .NET MAUI Foundation

* [ ] Create .NET MAUI project
* [ ] Establish shared models/contracts where appropriate
* [ ] Configure Android target
* [ ] Establish API client
* [ ] Establish authentication integration

## Mobile Features

Implement after the web core is stable:

* [ ] Navigation
* [ ] Explore
* [ ] Search
* [ ] Plant Detail
* [ ] Identification
* [ ] Favorites
* [ ] Where to Buy

## Mobile UX

* [ ] Slide-in sidebar
* [ ] Top app bar
* [ ] Two-column image grid
* [ ] Touch-friendly interactions
* [ ] Bottom sheets where appropriate
* [ ] No hover-dependent functionality

iOS remains deferred unless the development environment changes.

---

# 20. Phase 16 — Testing and QA

## Backend Unit Tests

* [ ] Plant business rules
* [ ] Search relevance
* [ ] Recommendation logic
* [ ] Candidate ranking
* [ ] External data normalization
* [ ] Validation
* [ ] Favorite rules

## Integration Tests

* [-] Database operations (persistence round trips and CHECK constraints covered)
* [ ] Plant Detail API
* [ ] Search API
* [x] Favorites API
* [ ] Identification boundary
* [ ] Authentication/authorization

## Frontend Tests

* [ ] Plant Detail states
* [ ] Search states
* [ ] Favorites interactions
* [ ] Navigation
* [ ] Identification states
* [ ] Error handling

## End-to-End Tests

Prioritize critical flows:

* [x] Explore → Plant Detail (and Back keeps the place; `frontend/e2e/journeys.spec.ts`)
* [-] Search → Plant Detail (suggestions, results and recent searches covered; opening a result not yet)
* [-] Identify → Results → Plant Detail (stubbed answer → Results → More; opening a plant not yet)
* [x] Plant Detail → Favorites (sign-up, confirmation, saving, Arrange, Undo)
* [ ] Favorites → Plant Detail
* [-] Plant Detail → Where to Buy (the page with location granted and refused; the link from Plant Detail not yet)

## Accessibility QA

* [ ] Keyboard navigation
* [ ] Screen-reader labels
* [ ] Focus behavior
* [ ] Contrast
* [ ] Touch target sizes
* [ ] Reduced motion
* [ ] Non-color-only feedback

---

# 21. Phase 17 — Docker

## Backend

* [x] Create backend Dockerfile (`backend/Dockerfile`, 2026-10-27)
* [x] Test local backend container (in `docker-compose.e2e.yml`)
* [x] Configure environment variables

## Frontend

* [x] Create production frontend build
* [x] Configure frontend container if appropriate (`frontend/Dockerfile`, nginx over HTTPS)
* [x] Verify production build

## Database

* [ ] Create development PostgreSQL container configuration
* [x] Verify migrations from clean environment (the migrations bundle on an empty database, at every stack start)
* [x] Verify local multi-container setup

## Compose

* [x] Create Docker Compose configuration where useful (`docker-compose.e2e.yml`)
* [x] Verify full local environment startup
* [x] Document local development commands (header of `docker-compose.e2e.yml`, ARCHITECTURE §45)
* [ ] Production images and settings (real certificate, email service, R2, no Testing environment), with deployment

Do not introduce Docker complexity that is unnecessary for local development.

---

# 22. Phase 18 — CI/CD

## GitHub Actions

* [x] Add backend build workflow (`.github/workflows/ci.yml`, 2026-10-27; first GitHub run passed 2026-10-27, run 40464368936)
* [x] Add backend tests (same)
* [x] Add frontend build (same)
* [x] Add frontend tests/linting (Prettier check, e2e type check, unit tests, theme check; same)
* [x] Add migration validation where appropriate (every e2e run applies all migrations to an empty database; same)
* [ ] Add CI status checks (branch protection on GitHub, the developer's setting)
* [-] Add dependency vulnerability scanning and secret scanning (SECURITY §20; `security` job added 2026-10-27, first GitHub run pending)
* [ ] Update `@angular/build` when a release moves past `vite` 8.3.0 (high advisory, development server only)
* [ ] Consider GitHub's own secret scanning with push protection and Dependabot alerts (repository settings, the developer's choice)

## Quality Gates

CI should catch:

* Build failures
* Test failures
* Formatting/lint failures where configured
* Type errors
* Other agreed quality issues

Do not make CI unnecessarily slow.

---

# 23. Phase 19 — AWS Deployment

## Infrastructure

* [ ] Decide initial AWS deployment architecture
* [ ] Configure required AWS resources
* [ ] Configure application environment
* [ ] Configure secrets safely
* [ ] Configure PostgreSQL production database
* [ ] Configure object storage if required
* [ ] Configure HTTPS
* [ ] Configure domain if desired

## Deployment

* [ ] Deploy backend
* [ ] Deploy frontend
* [ ] Configure production database
* [ ] Configure S3/object storage
* [ ] Configure AI provider secrets
* [ ] Verify health endpoint
* [ ] Verify core user flows

## Production Safety

* [ ] Verify no secrets are committed
* [ ] Verify logging
* [ ] Verify error handling
* [ ] Verify rate limiting
* [ ] Verify database backups where applicable
* [ ] Verify cost controls

---

# 24. Phase 20 — Performance

## Images

* [ ] Generate/serve appropriate thumbnails
* [ ] Use responsive image sizes
* [ ] Use WebP/AVIF where appropriate
* [ ] Implement lazy loading
* [ ] Review image payload sizes

## Frontend

* [ ] Review initial bundle size
* [ ] Review route loading
* [ ] Review unnecessary rendering
* [ ] Review layout shifts
* [ ] Review animation performance

## Backend

* [ ] Review slow queries
* [ ] Add appropriate indexes
* [ ] Review N+1 query risks
* [ ] Add caching where justified
* [ ] Review external API latency

Do not optimize blindly.

Measure meaningful bottlenecks before adding complex solutions.

---

# 25. Phase 21 — Security Review

* [ ] Review authentication
* [ ] Review authorization
* [ ] Review uploaded image validation
* [ ] Review API input validation
* [ ] Review rate limiting
* [ ] Review CORS
* [ ] Review secrets management
* [ ] Review logging for sensitive information
* [ ] Review database permissions
* [ ] Review external API credentials
* [ ] Review anonymous AI abuse risk

---

# 26. Phase 22 — UX and Visual Polish

Only after the core product works reliably:

## Visual

* [x] Refine typography (Nunito with Gowun Dodum for Korean)
* [x] Replace emoji with outlined brown icons
* [x] Light and dark themes with a switch, Martian Mono titles (D-074, 2026-10-27)
* [-] Check both themes and the new motion in a browser on a phone and desktop (automated browser QA, 2026-10-27: both themes on 13 pages, phone and desktop widths, axe clean; the feel of the motion still needs a person)
  - Priority: High
* [x] Full QA pass in a real browser across all journeys, both themes, desktop and phone (automated browser QA, 2026-10-27; see CURRENT_STATE for what was not covered)
  - Priority: High
* [ ] Refine spacing
* [ ] Refine image presentation
* [-] Refine cards (outlined-leaf placeholder, fade-in, hover zoom; masonry and hover information pending)
* [ ] Refine hero transition
* [ ] Refine responsive layouts

## Botanical Interaction

* [~] Add subtle desktop cursor effect if still valuable (not pursued: D-074)
* [-] Add occasional leaf effect if still valuable (leaf placeholders sway once on hover; no falling leaves)
* [x] Add subtle hover interactions (sidebar labels, card images)
* [ ] Add bookmark animation
* [-] Add scroll-driven hero transition (hero ease-in and scroll drift on desktop; card-to-hero transition needs the detail data before the page changes)

## Motion Review

* [ ] Verify navigation never waits for animation (by design; check in the browser)
* [x] Verify reduced-motion behavior (card fade-ins covered by tests; titles checked in the browser, automated browser QA, 2026-10-27)
* [ ] Verify mobile performance
* [ ] Measure with Lighthouse before and after the visual refresh
  - Priority: High (before treating Phase 22 motion as done)
* [ ] Remove effects that do not add meaningful value

---

# 27. Phase 23 — Portfolio Readiness

## Engineering

* [ ] Review architecture
* [ ] Review database design
* [ ] Review API design
* [ ] Review error handling
* [ ] Review tests
* [ ] Review security
* [ ] Review performance
* [ ] Review CI/CD
* [ ] Review deployment

## Documentation

* [ ] Update README
* [ ] Document local setup
* [ ] Document architecture
* [ ] Document major technical decisions
* [ ] Document API usage
* [ ] Document deployment
* [ ] Document AI identification design
* [ ] Document data provenance

## Demonstration

* [ ] Create screenshots
* [ ] Create short demo video/GIF if useful
* [ ] Prepare portfolio project description
* [ ] Prepare resume bullet points
* [ ] Prepare interview explanation
* [ ] Prepare architecture explanation
* [ ] Prepare trade-off explanations

---

# 28. Definition of Done

A feature is not complete merely because the UI exists.

For a meaningful feature, the definition of done is generally:

```text id="ps9h3q"
Requirement understood
        ↓
Implementation completed
        ↓
API/data contracts verified
        ↓
Loading/empty/error states handled
        ↓
Accessibility considered
        ↓
Tests added where appropriate
        ↓
Manual verification completed
        ↓
Documentation updated
        ↓
Code reviewed
```

The exact steps may vary depending on feature size.

---

# 29. Current Highest-Priority Tasks

The next practical tasks should be:

1. [ ] Confirm repository structure
2. [x] Create ASP.NET Core solution
3. [x] Establish backend layers
4. [x] Establish PostgreSQL + EF Core
5. [x] Create initial Plant domain model
6. [x] Create initial migration
7. [x] Add small development dataset
8. [x] Implement `GET /api/plants/{plantId}`
9. [x] Create Angular application foundation
10. [x] Connect Angular to the Plant Detail API
11. [x] Build the first Plant Detail vertical slice
12. [x] Validate the architecture against the real implementation

Do not move directly to AI identification before this foundation is stable.

---

# 30. Deferred Work

The following should remain deferred until their prerequisites are ready:

* [~] Advanced recommendation ML
* [~] Complex distributed architecture
* [~] Kubernetes
* [~] Microservices
* [~] Advanced event-driven infrastructure
* [~] Full growing manuals
* [~] Live retailer inventory without reliable data
* [~] iOS application
* [~] Favorites folders
* [~] Advanced visual plant similarity
* [~] Excessive decorative animation

Deferred does not mean permanently rejected.

It means:

> Do not spend implementation time on this before the core product justifies it.

---

# 31. Blockers

Record blockers here when they prevent progress.

Current blockers:

* [ ] None currently identified at the documentation level

When a blocker occurs, record:

```text id="0jupbq"
Blocker:
Why it matters:
Affected task:
Possible solutions:
Decision required:
```

Do not silently work around an architectural or product blocker if doing so would create long-term inconsistency.

---

# 32. Discovered Work

Use this section for work discovered during implementation that is not yet part of the main roadmap.

### Template

```text id="n6x1wy"
- [ ] [Task]
  - Why: [Reason]
  - Related feature: [Feature]
  - Priority: [High / Medium / Low]
```

### Discovered during the EF Core database foundation

- [x] Plant identity / duplicate-detection key for concurrent ingestion
  - Done: unique index `ux_plants_identity` on normalized scientific + cultivar name (migration `AddPlantIdentityKey`, DATABASE_DESIGN §59)
- [x] Handle `unique_violation` on `ux_plants_identity` during ingestion by reusing the existing plant
  - Done (11b): `PlantImportStore.InsertAsync` returns the winning plant; covered by a concurrent-import test
- [x] Soft near-duplicate warning for imports (e.g. `Mr Lincoln` vs `Mr. Lincoln`, `Ophelia` vs `Ophélia`) using `search_key`
  - Done (11b): an import whose scientific name has the same `search_key` as another plant but a different identity is saved as `NeedsReview` instead of `Published`
- [x] Flexible name search (case, spacing, underscores/punctuation) without altering stored names
  - Done: `search_key()` (case, accents, punctuation) in migration `AddSearchSupport`; used by `GET /api/search`
- [x] External plant lookup when search finds nothing (API_SPEC §27)
  - Done (11a–11c): `POST /api/search/external` and the "Search Catalogue of Life" button on the Search page
- [ ] Application-level validation and canonical casing of language tags (e.g. `en-GB`, `zh-Hant`)
  - Why: The database checks only the general tag shape
  - Related feature: Plant names / ingestion
  - Priority: Medium
- [ ] Per-field care provenance
  - Why: Care provenance and verification currently cover the whole PlantCare record
  - Related feature: Plant Detail Quick Care
  - Priority: Low
- [ ] Decide whether `updated_at` must also be maintained for raw SQL / `ExecuteUpdate` writes
  - Why: Timestamps are maintained only through `SaveChanges`
  - Related feature: Data integrity
  - Priority: Low
- [x] Add a backend test project with database integration tests
  - Done: `backend/PlantEncyclopedia.Tests` (xUnit 2 + Testcontainers PostgreSQL 16), 9 tests passing
- [ ] Port the remaining one-off constraint, delete-behavior, and timestamp checks into `PlantEncyclopedia.Tests`
  - Why: Only part of the earlier scratch validation is now repeatable
  - Related feature: Testing
  - Priority: Medium
- [ ] Verify sample plant names and taxonomy against GBIF / iNaturalist
  - Why: `seed-dev-data.sql` content was written from general knowledge and is unverified (e.g. "Rosa × hybrida" is a horticultural name, not an accepted species)
  - Related feature: Ingestion / curated data
  - Priority: Low (before any sample data could be mistaken for real content)
- [x] Integration test that an exception thrown by a real endpoint goes through `GlobalExceptionHandler`
  - Done: `DatabaseFailure_Returns500SafeError` (Plant Detail endpoint with an unreachable database)
- [ ] Runtime validation for `PlantSource.RetrievedAt` (reject `default`)
  - Why: `required` cannot prevent an explicit `default`; documented by `Required_DoesNotPreventExplicitDefaultRetrievedAt`
  - Related feature: Ingestion / curation
  - Priority: Medium

Newly discovered work should not automatically interrupt the current task.

Evaluate whether it should be:

* Added to the current scope
* Added to a later phase
* Deferred
* Rejected

---

# 33. Change Management

When a TODO item reveals that an existing decision is incorrect:

Do not simply change the TODO item.

Instead:

1. Identify the affected decision.
2. Review the relevant documentation.
3. Discuss the trade-off.
4. Update `DECISIONS.md` if the decision changes.
5. Update affected specifications.
6. Update this TODO.
7. Implement the approved change.

This prevents implementation from silently redefining the project.

---

# 34. Working Principle

The project should move forward through small, validated increments.

Prefer:

```text id="6av6yn"
Small task
→ Implement
→ Test
→ Review
→ Document
→ Commit
→ Next task
```

over:

```text id="yq8xq7"
Build everything
→ Debug everything
→ Discover architectural problems late
```

The goal is not to finish the checklist as quickly as possible.

The goal is to continuously move the project from:

```text id="q5w4ik"
Idea
→ Design
→ Working software
→ Tested software
→ Deployed product
→ Portfolio-quality project
```

while preserving the product's core principle:

> Build a real, trustworthy, fast, image-first plant discovery product before adding complexity.
