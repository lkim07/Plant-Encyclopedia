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
* [ ] Layers have clear dependency direction
* [ ] No unnecessary architectural complexity has been introduced
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
* [ ] Add initial images (deferred until image licensing/storage is decided)
* [ ] Add image provenance
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

* [ ] Implement `GET /api/categories`
* [ ] Implement category page
* [ ] Display category pills
* [ ] Implement category navigation
* [ ] Preserve category-pure behavior

## Plant Groups

* [ ] Implement `GET /api/plant-groups/{plantGroupId}`
* [ ] Implement plant-group page
* [ ] Display plant/cultivar image cards
* [ ] Implement navigation to Plant Detail

## Interaction

* [ ] Implement image-first cards
* [ ] Implement bookmark affordance
* [ ] Implement desktop hover state
* [ ] Implement mobile tap behavior
* [ ] Implement masonry/grid behavior

---

# 10. Phase 6 — Search

## Search API

* [ ] Implement `GET /api/search`
* [ ] Implement query validation
* [ ] Search common names
* [ ] Search scientific names
* [ ] Search cultivar names
* [ ] Search plant groups where appropriate
* [ ] Implement relevance ordering
* [ ] Add pagination/infinite-scroll support

## Suggestions

* [ ] Implement `GET /api/search/suggestions`
* [ ] Implement prefix/exact matching
* [ ] Return database-backed suggestions
* [ ] Avoid excessive external API requests while typing

## Search UI

* [ ] Implement Explore search activation
* [ ] Implement sticky search bar
* [ ] Implement back navigation
* [ ] Implement recent searches
* [ ] Implement search result grid
* [ ] Implement loading skeleton
* [ ] Implement no-results state
* [ ] Implement infinite scrolling

---

# 11. Phase 7 — Explore

## Explore API

* [ ] Implement `GET /api/explore`
* [ ] Establish initial feed composition
* [ ] Add category diversity
* [ ] Prevent duplicate plant/cultivar cards
* [ ] Support pagination/infinite scrolling

## Anonymous/New User Feed

* [ ] Implement broad category representation
* [ ] Implement curated/diverse discovery
* [ ] Avoid popularity-only recommendations
* [ ] Avoid repeated plants

## Behavioral Personalization

* [ ] Implement view signals
* [ ] Implement bookmark signals
* [ ] Implement search signals
* [ ] Implement plant-group signals
* [ ] Implement category signals
* [ ] Implement approximate 60/20/20 recommendation philosophy
* [ ] Add diversity safeguards

Start with transparent rule-based recommendation logic.

Do not build a machine-learning recommendation system at this stage.

---

# 12. Phase 8 — Search and View History

## Search History

* [ ] Implement search history model
* [ ] Implement `GET /api/history/searches`
* [ ] Implement deletion endpoint
* [ ] Store recent searches
* [ ] Remove recommendation influence when explicitly deleted

## View History

* [ ] Implement view history model
* [ ] Implement `POST /api/history/views`
* [ ] Implement `GET /api/history/views`
* [ ] Record meaningful Plant Detail views
* [ ] Use view history as a recommendation signal

## Anonymous State

* [ ] Decide implementation for anonymous identifiers
* [ ] Store anonymous history safely
* [ ] Define behavior when anonymous user authenticates

---

# 13. Phase 9 — Authentication

## Authentication Foundation

* [ ] Select/confirm authentication approach
* [ ] Implement authentication
* [ ] Implement user identity model
* [ ] Implement authorization
* [ ] Protect authenticated endpoints
* [ ] Add authentication tests

## UX

* [ ] Implement login entry
* [ ] Implement account state
* [ ] Handle unauthenticated actions
* [ ] Preserve intended action where appropriate

Do not make authentication a prerequisite for unrelated anonymous discovery features.

---

# 14. Phase 10 — Favorites

## Backend

* [ ] Create Favorite entity
* [ ] Implement `GET /api/favorites`
* [ ] Implement `POST /api/favorites`
* [ ] Implement `DELETE /api/favorites/{favoriteId}`
* [ ] Implement `PATCH /api/favorites/order`
* [ ] Enforce authorization
* [ ] Prevent duplicate favorites

## Frontend

* [ ] Implement Favorites page
* [ ] Implement bookmark state
* [ ] Implement save-to-favorites flow
* [ ] Implement login-required bottom sheet/modal
* [ ] Implement immediate removal
* [ ] Implement Undo snackbar
* [ ] Implement empty state
* [ ] Implement Arrange mode

## Arrange Mode

* [ ] Mobile long-press entry
* [ ] Desktop explicit Arrange/Edit action
* [ ] Delete controls
* [ ] Drag-to-reorder
* [ ] Persist logical order

---

# 15. Phase 11 — External Plant Data Ingestion

## Provider Abstraction

* [ ] Define plant data provider interface
* [ ] Implement iNaturalist provider
* [ ] Implement GBIF provider
* [ ] Isolate provider-specific response models
* [ ] Implement provider error handling

## Normalization

* [ ] Normalize names
* [ ] Normalize taxonomy
* [ ] Normalize plant groups
* [ ] Normalize images
* [ ] Store external identifiers
* [ ] Store provenance
* [ ] Store retrieval timestamps

## Validation

* [ ] Validate external records before persistence
* [ ] Detect likely duplicates
* [ ] Prevent duplicate cultivars/plants
* [ ] Handle incomplete provider data

## Search Integration

* [ ] Query local DB first
* [ ] Query external providers only when appropriate
* [ ] Persist validated results
* [ ] Return application-owned data

---

# 16. Phase 12 — Plant Identification

## Image Upload

* [ ] Implement image selection
* [ ] Implement camera/photo selection flow
* [ ] Implement image preview
* [ ] Implement Retake
* [ ] Implement Choose Another Photo
* [ ] Validate image type
* [ ] Validate image size
* [ ] Validate image dimensions where appropriate

## Identification API

* [ ] Implement `POST /api/identification`
* [ ] Define identification request model
* [ ] Define identification candidate model
* [ ] Implement validation
* [ ] Implement error handling
* [ ] Implement rate limiting

## AI Provider

* [ ] Define AI provider abstraction
* [ ] Implement selected vision API provider
* [ ] Secure API credentials
* [ ] Handle provider failures
* [ ] Prevent excessive usage/cost

## Candidate Pipeline

* [ ] Generate candidates
* [ ] Normalize candidate names
* [ ] Match candidates against own database
* [ ] Rank validated candidates
* [ ] Return maximum three actual candidates
* [ ] Keep More separate from candidates

## Result UI

* [ ] Implement identification loading state
* [ ] Keep selected image visible while processing
* [ ] Implement #1 candidate
* [ ] Implement #2 candidate
* [ ] Implement #3 candidate
* [ ] Implement More
* [ ] Implement failure state
* [ ] Navigate candidates to Plant Detail

---

# 17. Phase 13 — Similar Plants and More

## Similar Plants

* [ ] Implement similarity relationship/model
* [ ] Implement `GET /api/plants/{plantId}/similar`
* [ ] Implement initial taxonomy/curated matching
* [ ] Implement carousel
* [ ] Implement View More

## More

* [ ] Implement contextual identification results
* [ ] Implement `GET /api/identification/{requestId}/more` if required
* [ ] Preserve identification context
* [ ] Prevent duplicate candidates
* [ ] Keep More distinct from Explore

---

# 18. Phase 14 — Where to Buy

## Retailer Data

* [ ] Define retailer entity
* [ ] Define location entity
* [ ] Define retailer type
* [ ] Define source/provenance
* [ ] Define approximate pricing model
* [ ] Define availability model if reliable data becomes available

## API

* [ ] Implement `GET /api/plants/{plantId}/retailers`
* [ ] Implement distance sorting
* [ ] Implement rating sorting where supported
* [ ] Implement relevance sorting

## Location

* [ ] Implement location permission flow
* [ ] Handle permission denial
* [ ] Implement Vancouver, BC fallback
* [ ] Implement manual location change
* [ ] Avoid unnecessary location collection

## UX

* [ ] Implement Where to Buy entry card
* [ ] Implement dedicated Where to Buy page
* [ ] Separate nearby and online retailers
* [ ] Avoid unsupported live inventory claims
* [ ] Display approximate pricing with appropriate source/date information

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
* [ ] Favorites API
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

* [ ] Explore → Plant Detail
* [ ] Search → Plant Detail
* [ ] Identify → Results → Plant Detail
* [ ] Plant Detail → Favorites
* [ ] Favorites → Plant Detail
* [ ] Plant Detail → Where to Buy

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

* [ ] Create backend Dockerfile
* [ ] Test local backend container
* [ ] Configure environment variables

## Frontend

* [ ] Create production frontend build
* [ ] Configure frontend container if appropriate
* [ ] Verify production build

## Database

* [ ] Create development PostgreSQL container configuration
* [ ] Verify migrations from clean environment
* [ ] Verify local multi-container setup

## Compose

* [ ] Create Docker Compose configuration where useful
* [ ] Verify full local environment startup
* [ ] Document local development commands

Do not introduce Docker complexity that is unnecessary for local development.

---

# 22. Phase 18 — CI/CD

## GitHub Actions

* [ ] Add backend build workflow
* [ ] Add backend tests
* [ ] Add frontend build
* [ ] Add frontend tests/linting
* [ ] Add migration validation where appropriate
* [ ] Add CI status checks

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

* [ ] Refine typography
* [ ] Refine spacing
* [ ] Refine image presentation
* [ ] Refine cards
* [ ] Refine hero transition
* [ ] Refine responsive layouts

## Botanical Interaction

* [ ] Add subtle desktop cursor effect if still valuable
* [ ] Add occasional leaf effect if still valuable
* [ ] Add subtle hover interactions
* [ ] Add bookmark animation
* [ ] Add scroll-driven hero transition

## Motion Review

* [ ] Verify navigation never waits for animation
* [ ] Verify reduced-motion behavior
* [ ] Verify mobile performance
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
12. [ ] Validate the architecture against the real implementation

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

- [ ] Plant identity / duplicate-detection key for concurrent ingestion
  - Why: `plant_sources` uniqueness prevents duplicate provenance, not duplicate Plant rows (DATABASE_DESIGN §59)
  - Related feature: External plant data ingestion
  - Priority: High (before ingestion)
- [ ] Flexible name search (case, spacing, underscores/punctuation) without altering stored names
  - Why: Names are stored exactly as written; uniqueness is case-sensitive
  - Related feature: Search
  - Priority: Medium
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
