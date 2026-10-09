# Plant Encyclopedia — Development Roadmap

## 1. Purpose

This document defines the implementation roadmap for Plant Encyclopedia.

It translates the product requirements, UX specification, database design, and API specification into an executable development sequence.

The roadmap is designed to:

* build the product incrementally
* reduce unnecessary rework
* keep the architecture understandable for a beginner developer
* establish professional software engineering practices
* use AI-assisted development without blindly delegating engineering decisions
* produce a meaningful portfolio-ready MVP as early as practical
* leave room for advanced features after the MVP

This roadmap is a living document.

Implementation details may change when new technical information, testing results, or product decisions justify the change.

---

# 2. Current Product Direction

## 2.1 Product

Plant Encyclopedia is an image-first plant discovery and identification application.

Core journey:

```text
Discover
    ↓
Identify
    ↓
Compare
    ↓
Learn
    ↓
Find
    ↓
Save
    ↓
Discover More
```

The product should feel like a real consumer application rather than a technology demonstration.

The primary product qualities are:

1. Trustworthy information
2. Fast perceived performance
3. Frictionless interaction
4. Strong visual design
5. Responsible AI usage

---

# 3. Target Technology Direction

The current intended stack is:

### Web Frontend

* Angular
* TypeScript
* HTML
* CSS

### Backend

* ASP.NET Core
* C#

### Database

* PostgreSQL
* Entity Framework Core

### Infrastructure

* Docker
* AWS
* Amazon S3 for image/object storage where appropriate
* GitHub Actions for CI/CD

### Mobile

* .NET MAUI
* C#
* Android as the first target platform

iOS is deferred because development currently does not require a Mac.

### AI

A vision-capable AI API may be used for plant identification.

The exact provider is intentionally not hard-coded into the product architecture.

The AI provider must remain replaceable behind an application-level abstraction.

---

# 4. Development Philosophy

## 4.1 Build the Product, Not the Stack

Technology should support product requirements.

Do not build features simply because they demonstrate a framework.

For example:

Bad approach:

```text
"I need to demonstrate AWS Lambda."
```

Better approach:

```text
"The application needs reliable image storage."
        ↓
"Object storage is appropriate."
        ↓
"Amazon S3 is selected."
```

The product requirement comes first.

---

# 5. Incremental Development Strategy

The project should be developed in vertical slices whenever practical.

A vertical slice should move through enough layers to produce observable product behavior.

Example:

```text
User action
    ↓
Angular UI
    ↓
HTTP API
    ↓
ASP.NET Core service
    ↓
EF Core
    ↓
PostgreSQL
    ↓
Response
    ↓
Angular UI
```

This is preferred over building the entire frontend first, then the entire backend, then integration.

---

# 6. AI-Assisted Development Workflow

AI is a development tool, not the final decision-maker.

The project uses three roles.

## 6.1 ChatGPT

ChatGPT acts primarily as:

* product manager
* software architect
* teacher
* implementation planner
* code reviewer
* debugging assistant
* testing advisor
* documentation assistant

ChatGPT should help explain:

* why a design is chosen
* trade-offs
* implementation steps
* risks
* testing strategies
* unfamiliar C#/.NET concepts

---

## 6.2 Cursor

Cursor acts primarily as:

* repository-aware coding assistant
* implementation agent
* refactoring assistant
* test-writing assistant
* documentation assistant

Cursor may inspect and modify the actual repository.

Cursor should receive small, well-defined implementation tasks rather than being asked to build the entire project from a vague prompt.

---

## 6.3 Developer

The developer remains responsible for:

* product decisions
* architecture approval
* reviewing generated code
* understanding important implementation decisions
* approving migrations
* approving dependency changes
* verifying tests
* reviewing security-sensitive code
* deciding when a feature is actually complete

AI-generated code is not automatically considered correct.

---

# 7. Standard Development Loop

Every significant feature should follow approximately this workflow:

```text
1. Define requirement
        ↓
2. Clarify acceptance criteria
        ↓
3. Analyze architecture impact
        ↓
4. Create implementation plan
        ↓
5. Review the plan
        ↓
6. Ask Cursor to implement a small slice
        ↓
7. Run tests
        ↓
8. Manually verify behavior
        ↓
9. Ask AI to review the implementation
        ↓
10. Fix issues
        ↓
11. Commit changes
        ↓
12. CI validation
```

The developer should not skip directly from:

```text
"Let's build Favorites"
```

to:

```text
"Cursor, implement Favorites."
```

---

# 8. Definition of Done

A feature is not considered complete merely because the UI works once.

A feature should normally satisfy:

* requirement implemented
* acceptance criteria satisfied
* API contract respected
* validation implemented
* expected error states handled
* appropriate tests added
* manual verification completed
* accessibility considered
* performance considered
* security implications considered
* documentation updated when necessary
* code reviewed
* CI checks passing

Not every feature requires every type of test at the same depth.

The appropriate level depends on the risk and complexity of the feature.

---

# 9. Roadmap Overview

The development stages are:

```text
Stage 0  Project Foundation
   ↓
Stage 1  Backend & Database Foundation
   ↓
Stage 2  Core Encyclopedia Data
   ↓
Stage 3  Frontend Foundation
   ↓
Stage 4  Plant Detail & Search
   ↓
Stage 5  External Data Ingestion
   ↓
Stage 6  Authentication & Favorites
   ↓
Stage 7  Explore & Recommendations
   ↓
Stage 8  Plant Identification
   ↓
Stage 9  Where to Buy
   ↓
Stage 10 Testing, QA & Accessibility
   ↓
Stage 11 Docker, CI/CD & AWS Deployment
   ↓
Stage 12 Product Polish & Portfolio Readiness
```

The stages are sequential by default, but individual tasks may overlap when dependencies allow it.

---

# 10. Stage 0 — Project Foundation

## Goal

Create a clean repository and establish the project's engineering conventions before substantial implementation begins.

## Tasks

* create Git repository
* establish repository structure
* create README
* create project documentation directory
* add `.gitignore`
* establish environment-variable strategy
* establish branch/commit conventions
* establish local development instructions
* configure initial Angular application
* configure initial ASP.NET Core application
* configure initial test projects
* establish formatting/linting conventions
* establish basic CI skeleton

## Documentation

The repository should contain:

```text
/docs
    PRODUCT_REQUIREMENTS.md
    UX_SPECIFICATION.md
    ARCHITECTURE.md
    DATABASE_DESIGN.md
    API_SPEC.md
    DEVELOPMENT_ROADMAP.md
    CURRENT_STATE.md
    DECISIONS.md
    TODO.md
```

`PROJECT_INSTRUCTIONS.md` may be placed at the repository root if it is intended to be consumed directly by AI coding tools.

## Acceptance Criteria

* repository builds locally
* frontend starts locally
* backend starts locally
* test projects execute
* documentation is accessible
* secrets are not committed
* CI can perform basic build/test validation

## Done Means

A new developer should be able to clone the repository and understand how to start the project without relying on undocumented local configuration.

---

# 11. Stage 1 — Backend & Database Foundation

## Goal

Establish the backend architecture and PostgreSQL foundation.

## Tasks

* create ASP.NET Core application structure
* establish dependency injection
* establish configuration
* establish environment-specific configuration
* configure EF Core
* configure PostgreSQL
* create initial DbContext
* create initial migrations
* establish API error handling
* establish request validation
* establish logging
* establish health endpoint
* configure API documentation/OpenAPI
* establish basic service/repository boundaries where appropriate
* establish internal ID strategy

## Database

Start with the minimum entities required for the encyclopedia.

Initial entities (implemented by the `InitialCreate` migration):

* Plant
* PlantGroup
* Category
* Taxonomy
* PlantName
* PlantImage
* PlantSource (also stores external provider identifiers)
* PlantCare

Do not implement every future entity immediately.

## Testing

Add:

* basic API tests
* database integration tests where appropriate
* migration verification

## Acceptance Criteria

The backend can:

```text
Start
  ↓
Connect to PostgreSQL
  ↓
Apply migrations
  ↓
Expose health endpoint
  ↓
Return a basic plant response
```

## Done Means

The application has a stable backend/database foundation that later features can build upon.

---

# 12. Stage 2 — Core Encyclopedia Data

## Goal

Make the application capable of serving trustworthy plant information from its own database.

## Tasks

Implement:

* categories
* plant groups
* plants
* naming
* taxonomy
* images
* image provenance
* source records
* external identifiers

Implement basic APIs:

```text
GET /api/categories
GET /api/categories/{categoryId}
GET /api/plant-groups/{plantGroupId}
GET /api/plants/{plantId}
```

## Data Principles

The application database is the user-facing source of truth.

External provider data must be normalized before becoming application data.

Raw provider responses should not become the public API contract.

## Acceptance Criteria

A known plant can be:

1. stored in PostgreSQL
2. retrieved through the API
3. returned through a stable DTO
4. displayed with its image and core information

## Done Means

The product has its first trustworthy encyclopedia slice.

---

# 13. Stage 3 — Frontend Foundation

## Goal

Build the visual and navigation foundation of the application.

## Tasks

* establish Angular application architecture
* create application shell
* implement responsive sidebar
* implement mobile slide-in navigation
* implement top bar
* implement back navigation
* establish typography
* establish spacing system
* establish layout primitives
* establish image/card components
* establish skeleton components
* establish buttons and icon-button patterns
* establish bottom-sheet pattern
* establish accessible interaction patterns

## Primary Screens

Start with:

```text
Home / Identify
Explore
Category
Plant Group
Plant Detail
```

Do not implement every secondary feature yet.

## Acceptance Criteria

* navigation works
* responsive layout works
* desktop/mobile structures follow UX specification
* loading skeletons exist
* keyboard navigation works for primary controls
* basic visual language is consistent

## Done Means

The application looks and behaves like a coherent product rather than a collection of separate pages.

---

# 14. Stage 4 — Plant Detail & Search

## Goal

Create the first complete user-facing encyclopedia experience.

## Plant Detail

Implement:

* hero
* plant naming hierarchy
* bookmark UI placeholder if authentication is not ready
* quick care
* blooming
* health
* about
* similar plants
* responsive layout
* hero scroll transition

The Growing section remains excluded from MVP.

## Search

Implement:

```text
GET /api/search?q={query}
GET /api/search/suggestions?q={query}
```

Frontend:

* search mode
* recent searches UI
* suggestions
* result grid
* infinite scroll
* no-results state
* back navigation

## Acceptance Criteria

A user can:

```text
Explore
  ↓
Search
  ↓
Find a plant
  ↓
Open Detail
  ↓
Read trustworthy information
```

## Portfolio Milestone

At the end of this stage, the project should already be demonstrable as a real plant encyclopedia.

This is an important milestone.

---

# 15. Stage 5 — External Data Ingestion

## Goal

Allow the encyclopedia to expand beyond manually seeded data.

## Primary Sources

The intended structured sources are:

* iNaturalist
* GBIF

The exact provider implementation must remain isolated from the rest of the application.

## Pipeline

```text
User Search
    ↓
Own Database
    ↓
Found?
 ┌──Yes──→ Return
 │
 No
 ↓
External Provider
 ↓
Normalize
 ↓
Validate
 ↓
Duplicate Detection
 ↓
Persist
 ↓
Return
```

## Tasks

* provider clients
* provider abstractions
* DTO mapping
* normalization
* validation
* external identity tracking
* duplicate detection
* source provenance
* retrieval timestamps
* ingestion error handling
* retry strategy where appropriate
* caching

## Important Rule

Do not expose raw external API responses directly to the frontend.

## Acceptance Criteria

A plant missing from the local database can be discovered through an approved external source and safely added to the application's normalized data model.

---

# 16. Stage 6 — Authentication & Favorites

## Goal

Introduce user accounts and persistent personal collections.

## Tasks

* authentication
* authorization
* user identity
* Favorites
* favorite ordering
* anonymous bookmark intent preservation where practical
* login-required bottom sheet
* empty Favorites state
* remove/undo interaction
* arrange mode

## APIs

```text
GET    /api/favorites
POST   /api/favorites
DELETE /api/favorites/{favoriteId}
PATCH  /api/favorites/order
```

## Acceptance Criteria

Authenticated users can:

* save a plant
* remove a plant
* see saved plants
* reorder favorites
* recover from accidental removal
* use Favorites on another device/session

## Security

Verify:

* users cannot modify another user's favorites
* authentication state is correctly enforced
* authorization is server-side
* sensitive data is not exposed through APIs

---

# 17. Stage 7 — Explore & Recommendation MVP

## Goal

Turn the encyclopedia into a discovery product rather than a search-only database.

## Initial Users

For new or anonymous users:

* maintain category diversity
* avoid popularity-only feeds
* avoid pure randomness
* ensure broad representation

## Data-Rich Users

Use the intended guideline:

```text
60% strongly related
20% somewhat related / adjacent
20% substantially different / novel
```

This is a recommendation guideline, not a requirement for exact mathematical distribution in every small batch.

## Diversity Rules

A recommendation feed must not show the same plant/cultivar multiple times.

Different cultivars may appear separately.

Example:

```text
Valid:
Rosa 'Peace'
Rosa 'Mr. Lincoln'
Rosa 'Iceberg'

Invalid:
Rosa 'Peace'
Rosa 'Peace'
Rosa 'Peace'
```

## MVP Recommendation Signals

Use:

* viewed plants
* bookmarks
* search activity
* categories
* plant groups
* recent interactions

## Initial Algorithm

Use a rule-based system.

Do not introduce machine-learning recommendations prematurely.

## Acceptance Criteria

Explore:

* feels diverse
* respects user interests
* does not trap the user in one category
* does not repeat the same plant
* preserves feed state during navigation where practical
* supports infinite scrolling

---

# 18. Stage 8 — Plant Identification

## Goal

Introduce image-based plant identification as the application's major differentiating feature.

## User Flow

```text
Home
 ↓
Take Photo / Choose from Photos
 ↓
Preview
 ↓
Identify
 ↓
Identification Loading
 ↓
Identification Results
 ↓
Candidate
 ↓
Plant Detail
```

## Backend Pipeline

Conceptually:

```text
Image Upload
    ↓
Image Validation
    ↓
Image Hash
    ↓
Cache Lookup
    ↓
AI Vision Provider
    ↓
Candidate Generation
    ↓
Candidate Normalization
    ↓
Database Matching
    ↓
Validation
    ↓
Candidate Ranking
    ↓
Cache Result
    ↓
Response
```

## Important Product Rule

AI-generated candidates must not automatically become authoritative encyclopedia records.

Only validated database-backed candidates should be presented as encyclopedia results.

## Identification Results

Show:

* #1 main candidate
* #2 candidate
* #3 candidate
* More

Do not show:

* fake confidence percentages
* misleading certainty
* unvalidated encyclopedia entries

## Security & Cost Controls

Implement:

* image size limits
* file type validation
* request limits
* rate limiting
* abuse protection
* API timeout handling
* provider failure handling
* logging without storing unnecessary private image data

## Acceptance Criteria

A user can upload a reasonable plant photo and receive validated candidate results.

Failures must produce recoverable UX rather than false certainty.

---

# 19. Stage 9 — Where to Buy

## Goal

Connect plant discovery with practical purchasing information.

## MVP Scope

Focus initially on Vancouver / British Columbia.

Potential business types:

* nurseries
* garden centres
* flower shops
* online retailers

## Location Flow

Request location permission only when the user enters Where to Buy.

If location is unavailable:

```text
Vancouver, BC
```

may be used as the default MVP location with manual location adjustment.

## Sorting

Default:

```text
Distance
```

Alternatives:

```text
Rating
Relevance
```

## Important Limitation

Do not claim real-time inventory unless a reliable inventory source exists.

Prices should be presented as approximate when necessary and should include source/date context when available.

## Acceptance Criteria

A user can open a plant's Where to Buy page and discover relevant nearby sellers without requiring location access for the rest of the application.

---

# 20. Stage 10 — Testing, QA & Accessibility

## Goal

Raise the project from "works on my machine" to a reliable portfolio application.

## Testing Layers

### Unit Tests

Use for:

* business rules
* recommendation calculations
* validation
* normalization
* ranking
* utility logic

### Integration Tests

Use for:

* API + database behavior
* EF Core queries
* authentication/authorization
* ingestion
* persistence rules

### End-to-End Tests

Use selectively for important user journeys:

```text
Search → Detail
Explore → Detail
Favorite → Favorites
Identify → Result → Detail
Where to Buy
```

## Manual QA

Test:

* desktop
* tablet
* mobile
* slow network
* empty data
* failed API
* invalid image
* AI failure
* expired session
* unauthorized request
* long plant names
* missing images
* large result sets

## Accessibility

Verify:

* keyboard navigation
* focus states
* semantic HTML
* accessible labels
* image alt text
* sufficient contrast
* touch target size
* reduced motion
* non-hover alternatives

## Acceptance Criteria

Critical user flows can be completed without obvious functional, accessibility, or responsive failures.

---

# 21. Stage 11 — Docker, CI/CD & AWS Deployment

## Goal

Deploy the application using reproducible engineering practices.

## Docker

Containerize appropriate services.

Potential structure:

```text
Frontend
Backend
PostgreSQL
```

Local development may use Docker Compose where appropriate.

## CI

GitHub Actions should validate at minimum:

* frontend build
* backend build
* tests
* formatting/linting where configured
* migration consistency where practical

## CD

Deployment should be automated only after CI gates pass.

Potential AWS components may include:

* compute/service hosting
* PostgreSQL hosting
* S3
* CDN
* secrets/configuration management
* monitoring/logging

Exact AWS services should be selected based on the final architecture and cost constraints.

## Environment Separation

Maintain clear separation between:

```text
Development
Test
Production
```

Never commit:

* API keys
* passwords
* database credentials
* cloud secrets
* private tokens

## Acceptance Criteria

A production environment can be deployed reproducibly from the repository.

The application has:

* HTTPS
* environment configuration
* database migration strategy
* logging
* health checks
* basic monitoring
* rollback/recovery plan

---

# 22. Stage 12 — Product Polish & Portfolio Readiness

## Goal

Transform the working application into a polished portfolio project.

## Visual Polish

Refine:

* typography
* spacing
* imagery
* responsive behavior
* card interactions
* hero transitions
* bookmark animation
* sidebar transitions
* empty states
* skeleton states

## Botanical Micro-Interactions

Possible final additions:

* subtle botanical cursor
* occasional falling leaf
* cursor-following effect
* subtle image hover transitions
* gentle bookmark transition

These are optional.

Performance and usability always take priority.

## Motion Rule

Never make core actions wait for decorative animation.

---

# 23. Portfolio Readiness Checklist

Before presenting the project publicly, verify:

## Product

* clear product purpose
* coherent user journey
* meaningful data
* usable identification feature
* polished detail pages
* responsive experience

## Engineering

* clean architecture
* meaningful tests
* API documentation
* database migrations
* validation
* error handling
* authentication/authorization
* rate limiting where necessary
* logging
* CI/CD
* deployment

## Data

* source provenance
* external IDs
* normalized data
* duplicate handling
* trustworthy user-facing content

## AI

* clear AI role
* AI failure handling
* API-cost protection
* no fabricated certainty
* no unsupported care claims
* provider abstraction

## Accessibility

* keyboard navigation
* accessible labels
* contrast
* reduced motion
* touch targets
* meaningful alternatives to hover interactions

## Performance

* optimized images
* lazy loading
* responsive images
* reasonable API latency
* efficient queries
* no unnecessary animation overhead

---

# 24. MVP Boundary

The MVP should prioritize:

```text
Core Encyclopedia
+
Explore
+
Search
+
Plant Detail
+
Authentication
+
Favorites
+
Basic Identification
```

Where to Buy can be included in the MVP if the data source/integration is sufficiently reliable.

The following are intentionally lower priority:

* advanced recommendation ML
* sophisticated visual similarity
* complex social features
* folders/collections
* full iOS application
* advanced growing guides
* real-time retailer inventory
* elaborate AI chat
* excessive animation
* advanced analytics dashboards

The goal is not to maximize feature count.

The goal is to create a convincing, reliable product.

---

# 25. Resume Milestones

The project should produce value before every advanced feature is complete.

## Milestone A — Functional Encyclopedia

After Stage 4:

```text
Angular
+
ASP.NET Core
+
PostgreSQL
+
EF Core
+
Responsive UX
+
Search
+
Plant Detail
```

This is already a meaningful portfolio project.

## Milestone B — Full Consumer MVP

After Stages 6–9:

```text
Authentication
+
Favorites
+
Explore
+
Recommendations
+
Identification
+
Where to Buy
```

This becomes the primary portfolio version.

## Milestone C — Production-Ready Portfolio

After Stages 10–12:

```text
Testing
+
Accessibility
+
Performance
+
Docker
+
CI/CD
+
AWS
+
Monitoring
+
Polish
```

This is the strongest portfolio presentation.

---

# 26. Development Order Within Each Feature

For each feature, prefer this order:

```text
1. Product requirement
2. UX behavior
3. Data requirements
4. API contract
5. Backend implementation
6. Frontend implementation
7. Integration
8. Tests
9. Accessibility
10. Performance
11. Review
12. Documentation
```

However, vertical slicing is encouraged when it produces a usable result faster.

---

# 27. When to Use Cursor

Cursor should be used after the feature's requirements and implementation boundaries are sufficiently clear.

### Before Cursor

The developer and ChatGPT should establish:

* what the feature does
* what it does not do
* affected files/layers
* data model changes
* API contract
* acceptance criteria
* edge cases
* testing expectations

### Cursor Task

Give Cursor a narrow implementation objective.

Example:

```text
Implement GET /api/plants/{plantId}.

Requirements:
- follow API_SPEC.md
- use the existing EF Core DbContext
- return the defined Plant DTO
- handle not-found correctly
- do not expose database entities directly
- add unit/integration tests
- do not modify unrelated files
```

This is preferable to:

```text
Build the entire plant encyclopedia backend.
```

---

# 28. Code Review Workflow

After Cursor completes a task:

```text
Cursor implementation
        ↓
Run tests
        ↓
Manual review
        ↓
Ask ChatGPT for code review
        ↓
Fix issues
        ↓
Run tests again
        ↓
Commit
```

Review should specifically check:

* correctness
* maintainability
* security
* unnecessary complexity
* performance
* test quality
* API consistency
* architecture violations
* accidental scope expansion

---

# 29. Database Migration Rules

Database changes must be treated as controlled engineering changes.

Before a migration:

1. understand the schema change
2. verify affected relationships
3. consider existing data
4. review nullability
5. review indexes
6. consider backward compatibility
7. create migration
8. test migration locally
9. test application behavior
10. commit migration with the related code

Never casually delete or recreate the production database to solve a migration problem.

---

# 30. API Contract Rules

Frontend and backend should communicate through explicit contracts.

Avoid coupling Angular directly to:

* EF Core entities
* internal database structures
* external provider response formats

Use DTOs designed around application needs.

When an API contract changes:

1. update API specification
2. update backend
3. update frontend
4. update tests
5. verify backward compatibility where relevant

---

# 31. Error Handling Strategy

Every major feature should define:

```text
Success
Loading
Empty
Recoverable Error
Permanent/Unsupported State
```

The frontend should not expose raw backend exceptions to users.

The backend should return predictable error structures.

The application should provide a recovery action where possible.

Examples:

```text
Try Again
Choose Another Photo
Go Back
Search Again
```

---

# 32. Performance Strategy

Performance should be considered during implementation, not only after the product is finished.

Priorities:

1. fast initial interaction
2. fast navigation
3. fast image loading
4. efficient API/database queries
5. smooth scrolling
6. subtle animation

Use:

* responsive images
* WebP/AVIF where appropriate
* thumbnails
* lazy loading
* CDN where appropriate
* pagination/infinite scrolling
* efficient database indexes
* appropriate caching
* GPU-friendly animation

Do not optimize prematurely with complex infrastructure.

Measure before introducing complexity.

---

# 33. Security Strategy

Security-sensitive features require explicit review.

Areas include:

* authentication
* authorization
* file uploads
* image validation
* API keys
* external provider credentials
* rate limiting
* CORS
* database access
* user data
* retailer/location data
* AI API abuse

Never trust frontend validation alone.

Important validation must also occur on the server.

---

# 34. Observability

The application should eventually provide enough visibility to understand production problems.

Track appropriate technical signals such as:

* request failures
* response latency
* identification failures
* external provider failures
* ingestion failures
* database errors
* authentication failures

Avoid logging unnecessary sensitive information.

For identification requests, do not log raw private images merely for debugging convenience.

---

# 35. Documentation Maintenance

Documentation is part of development.

Update documentation when a meaningful decision changes.

Important documents include:

```text
PRODUCT_REQUIREMENTS.md
UX_SPECIFICATION.md
ARCHITECTURE.md
DATABASE_DESIGN.md
API_SPEC.md
DEVELOPMENT_ROADMAP.md
CURRENT_STATE.md
DECISIONS.md
TODO.md
```

Do not allow implementation to permanently diverge from these documents.

When implementation intentionally differs from a document:

1. identify the reason
2. decide whether the product/architecture changed
3. update the relevant document
4. record the decision when significant

---

# 36. Decision Records

Important architectural decisions should be recorded in `DECISIONS.md`.

Examples:

* why Angular was selected
* why ASP.NET Core was selected
* why PostgreSQL was selected
* why external plant data is normalized
* why AI is not the source of truth
* why category pages remain category-pure
* why Explore uses diversity rules
* why the first mobile platform is Android
* why growing guides are excluded from MVP
* why Google Images are not part of the data pipeline

The purpose is not bureaucracy.

The purpose is to prevent repeatedly reconsidering settled decisions.

---

# 37. Scope Control

New ideas should not automatically enter the current sprint.

For every proposed feature, ask:

```text
Does this improve the core user journey?
Does it support the current milestone?
Does it introduce significant infrastructure?
Does it create a new data dependency?
Does it delay deployment?
Can it be deferred without harming the product?
```

If the feature is useful but not essential, place it in `TODO.md` or a future milestone.

---

# 38. Learning Strategy

Because this project is also a learning project, implementation should explain important concepts rather than hide them.

When introducing an unfamiliar technology:

```text
Concept
  ↓
Why the project needs it
  ↓
Minimal example
  ↓
Implementation
  ↓
Test
  ↓
Review
```

Examples:

* C# dependency injection
* ASP.NET Core controllers/services
* EF Core relationships
* LINQ
* async/await
* PostgreSQL indexes
* Angular components/services
* TypeScript types
* HTTP/API contracts
* authentication
* Docker
* CI/CD
* AWS
* caching
* rate limiting

The project should teach transferable engineering concepts, not just framework syntax.

---

# 39. Avoiding AI Dependency

AI should accelerate development without replacing understanding.

For important code, the developer should be able to explain:

* what it does
* why it exists
* what assumptions it makes
* how it fails
* how it is tested
* what could go wrong

Particularly important areas:

* authentication
* authorization
* database transactions
* file uploads
* external APIs
* AI integration
* caching
* concurrency
* deployment
* security

---

# 40. Suggested Git Strategy

Prefer small, meaningful commits.

Examples:

```text
feat: add plant detail endpoint
feat: add plant detail page
test: add plant detail integration tests
fix: handle missing plant images
feat: add search suggestions
docs: update API specification
```

Avoid enormous commits such as:

```text
finish entire project
```

A commit should represent a coherent change.

---

# 41. Pull Request Strategy

Even when working alone, use PR-style thinking for significant features.

A PR should explain:

* what changed
* why it changed
* how it was tested
* screenshots for meaningful UI changes
* known limitations
* migration requirements if any

This creates useful evidence of professional development practices for a portfolio project.

---

# 42. Recommended Implementation Rhythm

Do not attempt to complete the entire project in one continuous coding session.

Prefer:

```text
Plan
 ↓
Small implementation
 ↓
Test
 ↓
Review
 ↓
Commit
 ↓
Next slice
```

This makes debugging easier and prevents AI-generated changes from becoming difficult to understand.

---

# 43. Current Priority

The immediate development priority should be:

```text
1. Finalize architecture documentation
2. Finalize project decisions
3. Finalize current-state documentation
4. Initialize repository
5. Establish backend/frontend foundations
6. Build first encyclopedia vertical slice
```

The first meaningful vertical slice should be:

```text
Plant in PostgreSQL
        ↓
ASP.NET Core API
        ↓
Angular Plant Detail
        ↓
Responsive UI
        ↓
Tests
```

Once this works end-to-end, expand the product.

---

# 44. What Not to Do

Do not:

* build every feature before testing
* generate the whole application with one AI prompt
* expose database entities directly
* expose raw external API responses
* let AI generate authoritative plant-care facts without validation
* add complex recommendation ML before enough behavioral data exists
* add AWS infrastructure before the local architecture is stable
* add decorative animation before core UX works
* sacrifice performance for visual effects
* add features simply to increase resume keyword count
* skip tests because the code was generated by AI
* treat passing tests as proof that the product is fully correct
* allow documentation and implementation to drift indefinitely

---

# 45. Final Roadmap Principle

Plant Encyclopedia should grow in this order:

```text
Trustworthy Data
      ↓
Reliable Backend
      ↓
Coherent UX
      ↓
Useful Encyclopedia
      ↓
Discovery
      ↓
Personalization
      ↓
Identification
      ↓
Commerce Discovery
      ↓
Reliability
      ↓
Deployment
      ↓
Polish
```

The project does not need to be impressive because it contains many technologies.

It should be impressive because the technologies work together to solve a believable user problem.

The guiding principle is:

> **Build a real product one reliable slice at a time.**
