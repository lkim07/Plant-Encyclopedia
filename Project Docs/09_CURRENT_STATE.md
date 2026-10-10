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

* `GET /api/plants/{plantId}` — Plant Detail for Published plants only (API_SPEC §14): `id`, `name`, `plantGroup`, `scientificName`, `description`, `heroImage` (always `null` for now), `quickCare` (Verified care only, numeric temperature). `404 PLANT_NOT_FOUND` for unknown or unpublished plants, `400 VALIDATION_ERROR` for a malformed ID. Minimal API endpoint → `IPlantQueries` (Application) → EF Core query (Infrastructure).
* The backend was started locally and `GET /health` returned `healthy` against `plant_encyclopedia_dev` (manual check).

Verified by automated tests (23 passing in total; 7 for the Plant Detail endpoint, including an end-to-end check that a database failure returns the safe 500 body): healthy and unhealthy `/health` responses (exact JSON and status codes, no password in logs), the safe 500 error body, and that the API refuses to start without a connection string.

Not yet implemented: other feature endpoints (categories, plant groups, search, …), request correlation IDs, structured logging, authentication.

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
* Routes: `/` → `/identify`; heading-only placeholders for `/identify`, `/explore`, `/favorites`; a not-found page for unknown URLs.
* Design tokens (warm neutral / dark-brown palette, spacing, radii) as CSS variables in `src/styles.css`; reduced-motion respected.
* Typed API models and `PlantApi` client for `GET /api/plants/{plantId}` (`src/app/core/api/`).
* Plant Detail page at `/plants/:plantId`: Back button (in-app history, otherwise `/identify`), hero placeholder, naming hierarchy (name, plant group, italic scientific name), description, Quick Care cards (verified care only; expandable when a description exists; temperature shown as `15–25°C` / `From 15°C` / `Up to 25°C`) or "Care information isn't available yet.". Loading skeleton, "Plant not found" (404/400), and "Something went wrong" with Try Again. Blooming, Health, About, Where to Buy, Similar Plants, and Bookmark are not shown yet.
* Dev-server proxy (`proxy.conf.json`) forwards `/api` and `/health` to `http://localhost:5034`; no backend CORS configuration.
* `npm run build` succeeds without warnings; 17 unit tests pass (Vitest + jsdom, HTTP mocked).
* Manually verified end to end (browser → dev proxy → API → `plant_encyclopedia_dev`): a Published sample plant renders; the Draft plant and a malformed ID show "Plant not found"; stopping the API shows the error state and Try Again recovers; Back and the mobile layout work.

Not yet implemented: links into Plant Detail from other pages, real Identify/Explore/Favorites screens, images, animations, authentication.

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

**UX and recommendation behavior defined; implementation pending**

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

**Design defined; implementation pending**

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

**UX and architecture defined; implementation pending**

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

**Rules defined; implementation pending**

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

**Architecture defined; implementation pending**

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

**Behavior defined; implementation pending**

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

**Behavior defined; implementation pending**

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

**Concept defined; implementation pending**

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
* [ ] Search API
* [ ] Search UI
* [ ] Category browsing
* [ ] Plant-group browsing
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
