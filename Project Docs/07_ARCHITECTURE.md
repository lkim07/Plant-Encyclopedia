# Plant Encyclopedia — Architecture

## 1. Purpose

This document defines the technical architecture of Plant Encyclopedia.

It translates the product requirements, UX specification, database design, and API specification into a concrete system structure.

The architecture is designed around the following principles:

* product requirements come before technology choices
* keep the MVP understandable and maintainable
* separate responsibilities between application layers
* isolate external providers from the core application
* treat the application database as the user-facing source of truth
* keep AI replaceable
* optimize for reliability and user experience before infrastructure complexity
* make the system extensible without prematurely over-engineering it
* support local development, automated testing, CI/CD, and cloud deployment

---

# 2. Architectural Principles

## 2.1 Product First

Architecture exists to support the product.

Technology should not determine product behavior simply because a technology is available.

For example:

```text id="2ud2f0"
Requirement:
Store and serve plant images reliably
        ↓
Need:
Object storage
        ↓
Possible implementation:
Amazon S3
```

The reverse should be avoided:

```text id="v0o5rj"
"I want to use S3, so I need to invent an S3 feature."
```

---

## 2.2 Modular Monolith First

The initial backend should use a **modular monolith** rather than microservices.

The application should have clear internal boundaries while remaining deployable as one backend application.

Conceptually:

```text id="w7m1so"
                    ASP.NET Core Application
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
   Encyclopedia        Identification       Discovery
        │                   │                   │
        ├──────────────┬────┼──────────────┐    │
        │              │    │              │    │
      Search        Favorites           Ingestion
        │              │                   │
        └──────────────┴─────────┬─────────┘
                                 │
                              PostgreSQL
```

This provides separation of concerns without the operational cost of multiple independently deployed services.

Microservices may be considered later only if real scale or organizational requirements justify them.

---

# 3. High-Level System Architecture

The system consists of:

1. Web frontend
2. Mobile client
3. Backend API
4. Application services
5. PostgreSQL database
6. Object/image storage
7. External plant data providers
8. AI identification provider
9. Retailer/location data providers
10. Cache
11. Authentication system
12. Observability and deployment infrastructure

High-level architecture:

```text id="9m8u0k"
                        ┌─────────────────────┐
                        │       User          │
                        └──────────┬──────────┘
                                   │
                     ┌─────────────┴─────────────┐
                     │                           │
              Angular Web                 .NET MAUI Android
                     │                           │
                     └─────────────┬─────────────┘
                                   │
                              HTTPS / JSON
                                   │
                        ┌──────────▼──────────┐
                        │   ASP.NET Core API  │
                        └──────────┬──────────┘
                                   │
            ┌──────────────────────┼──────────────────────┐
            │                      │                      │
            ▼                      ▼                      ▼
     Application Layer       Infrastructure Layer    Auth/Security
            │                      │
            │          ┌───────────┼────────────┐
            │          │           │            │
            ▼          ▼           ▼            ▼
       PostgreSQL    Object      Cache       External APIs
                     Storage                   │
                                               ├─ iNaturalist
                                               ├─ GBIF
                                               ├─ AI Provider
                                               └─ Retailer/Location
```

---

# 4. Client Architecture

The system has two intended clients:

* Angular web application
* .NET MAUI Android application

Both clients communicate with the same backend API.

They should not access PostgreSQL or external data providers directly.

---

# 5. Web Frontend

## Technology

* Angular
* TypeScript
* HTML
* CSS

## Responsibilities

The web application is responsible for:

* rendering UI
* navigation
* local UI state
* API communication
* loading states
* empty states
* error states
* responsive behavior
* accessibility
* image presentation
* user interaction

The frontend should not contain authoritative business rules that must be enforced by the backend.

For example:

Bad:

```text id="6obqzv"
Frontend decides:
"This user is allowed to modify this favorite."
```

Correct:

```text id="rv7shh"
Frontend:
"User requested modification."

Backend:
"Is this user authenticated?"
"Does this favorite belong to this user?"
"Is the operation allowed?"
```

---

# 6. Angular Structure

The exact Angular folder structure may evolve, but the application should separate:

```text id="q4g1f5"
Core
 ├── API clients
 ├── authentication
 ├── application services
 └── global infrastructure

Shared
 ├── UI components
 ├── directives
 ├── pipes
 └── reusable utilities

Features
 ├── explore
 ├── search
 ├── plant-detail
 ├── identification
 ├── favorites
 ├── categories
 └── where-to-buy

Layout
 ├── sidebar
 ├── top-bar
 └── application shell
```

Feature-specific behavior should remain close to the feature that owns it.

Avoid creating a huge global utility/service layer containing unrelated functionality.

---

# 7. Mobile Architecture

The Android client will use:

* .NET MAUI
* C#

The mobile application should consume the same REST API used by the web client.

Conceptually:

```text id="7h9j1b"
Angular Web ────────┐
                    │
                    ▼
              ASP.NET Core API
                    ▲
                    │
.NET MAUI Android ──┘
```

The mobile application should follow the same product rules as the web application while adapting interaction patterns for touch.

Examples:

* web hover → mobile tap
* persistent sidebar → mobile slide-in sidebar
* desktop layout → responsive mobile layout
* hover information → accessible/touch equivalent

The mobile client should not create a separate business model for the same encyclopedia concepts.

---

# 8. Backend Architecture

The backend uses ASP.NET Core.

The backend is responsible for:

* HTTP API
* authentication and authorization
* application business rules
* data validation
* database access
* external provider integration
* image handling
* AI orchestration
* recommendation logic
* caching
* rate limiting
* error handling
* logging
* observability

The backend is the primary trust boundary of the system.

---

# 9. Backend Layering

The backend should conceptually use the following layers:

```text id="j5m9sv"
Presentation
    ↓
Application
    ↓
Domain
    ↓
Infrastructure
```

Not every simple feature requires a separate class in every layer.

The goal is responsibility separation, not ceremony.

---

# 10. Presentation Layer

The Presentation layer handles HTTP concerns.

Responsibilities:

* controllers/endpoints
* request models
* response DTOs
* authentication metadata
* HTTP status codes
* request validation entry points
* API documentation

It should not contain large amounts of business logic.

Example:

```text id="f6v3qx"
PlantController
    ↓
PlantService
    ↓
PlantRepository / DbContext
```

Avoid:

```text id="x6gkq1"
Controller
    ├── complex EF queries
    ├── recommendation logic
    ├── external API calls
    └── ranking algorithm
```

---

# 11. Application Layer

The Application layer coordinates use cases.

Examples:

* GetPlant
* SearchPlants
* GetExploreFeed
* AddFavorite
* RemoveFavorite
* IdentifyPlant
* GetSimilarPlants
* GetRetailers

The Application layer should answer:

> "What does the system need to do?"

It should coordinate domain rules and infrastructure services without exposing infrastructure details to the client.

---

# 12. Domain Layer

The Domain layer contains important business concepts and rules.

Potential concepts include:

* Plant
* PlantGroup
* Category
* Favorite
* IdentificationCandidate
* Recommendation
* Source
* ExternalPlantIdentity

Domain logic should be kept independent from:

* HTTP
* Angular
* external API response formats
* cloud provider SDKs

This allows the core product rules to remain stable even when infrastructure changes.

---

# 13. Infrastructure Layer

The Infrastructure layer handles external technical dependencies.

Examples:

* EF Core
* PostgreSQL
* S3
* iNaturalist client
* GBIF client
* AI provider client
* retailer/location provider
* cache implementation
* authentication infrastructure

Example:

```text id="k6vvn7"
IPlantDataProvider
       │
 ┌─────┴─────────────┐
 │                   │
iNaturalist       GBIF
```

The application layer should depend on the abstraction rather than directly on provider-specific classes.

---

# 14. Dependency Direction

The preferred dependency direction is:

```text id="3z1t9b"
Presentation
     ↓
Application
     ↓
Domain

Infrastructure
     ↓
implements interfaces required by Application/Domain
```

The important principle is:

> Core application logic should not become tightly coupled to external providers.

For example, avoid making plant search logic depend directly on an iNaturalist SDK type.

---

# 15. API Communication

Clients communicate with the backend through HTTPS.

Initial API style:

* REST
* JSON
* versioned base path when appropriate
* DTO-based responses
* cursor/page-based pagination as defined by API requirements
* predictable error responses

Example:

```text id="d3f6hf"
GET /api/plants/{plantId}
```

The client should receive an application-specific response rather than a database entity.

---

# 16. DTO Boundary

Database entities should never be treated as the public API contract.

Example:

```text id="6xw9g4"
PostgreSQL Entity
      ↓
Application Mapping
      ↓
PlantDto
      ↓
JSON
      ↓
Angular / MAUI
```

This protects the API from database implementation details.

Benefits:

* safer schema evolution
* reduced accidental data exposure
* clearer API contracts
* easier frontend development
* easier testing

---

# 17. Database Architecture

The primary relational database is PostgreSQL.

Entity Framework Core is used as the application's ORM.

The database stores:

* plant records
* names
* taxonomy
* categories
* plant groups
* images
* sources
* external identities
* care information
* user accounts
* favorites
* search history
* view history
* identification records
* recommendation-related signals
* retailer information
* location information where appropriate

The detailed schema is defined in `DATABASE_DESIGN.md`.

---

# 18. Database Access

EF Core is the primary database access technology.

Application code should use:

* explicit queries
* appropriate projections
* indexes
* pagination
* transactions where necessary
* asynchronous database operations

Avoid retrieving entire database entities when only a small DTO is required.

Example principle:

```text id="7n6o3m"
Need:
Plant name + thumbnail

Prefer:
SELECT only required fields

Avoid:
Load complete Plant + all relationships + all images
```

---

# 19. Object Storage Architecture

Large images should not be stored directly inside PostgreSQL.

Use object storage for appropriate image assets.

The intended cloud storage is Amazon S3.

Conceptually:

```text id="2q0rj9"
Application
    │
    ├── metadata ──→ PostgreSQL
    │
    └── image ─────→ Object Storage
```

PostgreSQL stores metadata such as:

* image ID
* plant ID
* storage key
* source
* dimensions
* image type
* provenance
* timestamps

The database should not store large image binary data unless a specific requirement justifies it.

---

# 20. Image Processing

Images may pass through an application-controlled pipeline.

Conceptually:

```text id="h5j1w7"
Upload / External Image
        ↓
Validate
        ↓
Process
        ↓
Generate appropriate variants
        ↓
Store
        ↓
Persist metadata
```

Potential variants:

* original
* medium
* thumbnail

The exact variants should be determined by actual UI and performance requirements.

Do not generate unnecessary image sizes.

---

# 21. External Plant Data Architecture

External plant providers are treated as data sources, not application databases.

Primary structured sources:

* iNaturalist
* GBIF

Architecture:

```text id="4p6r4z"
Application
     ↓
Plant Data Provider Interface
     ↓
Provider Adapter
     ├── iNaturalist
     └── GBIF
```

Provider-specific response formats must stop at the infrastructure boundary.

---

# 22. Data Ingestion

The ingestion pipeline is:

```text id="h7jv9e"
External Provider
       ↓
Provider DTO
       ↓
Normalization
       ↓
Validation
       ↓
Identity / Duplicate Detection
       ↓
Application Model
       ↓
PostgreSQL
```

The application should preserve provenance.

A plant record should be able to answer:

* where did this information originate?
* which external identifier was used?
* when was it retrieved?
* what source provided the information?

---

# 23. External Provider Failure

External APIs are unreliable dependencies.

The application must handle:

* timeout
* rate limit
* invalid response
* provider outage
* malformed data
* partial results

External failure should not automatically cause the entire application to fail.

Example:

```text id="0o8qk4"
User Search
    ↓
Local DB
    ↓
No result
    ↓
External provider
    ↓
Provider unavailable
    ↓
Graceful error / fallback
```

The frontend should receive a controlled application error rather than an external provider exception.

---

# 24. Caching Architecture

Caching is used to reduce:

* external API calls
* repeated expensive operations
* latency
* AI API costs

Caching is not the application's permanent source of truth.

Principle:

```text id="y8t6gq"
Database = persistence
Cache    = temporary reuse
```

Potential cached data:

* external search results
* plant lookup results
* image metadata
* identification results
* recommendation feed components

Cache TTLs should be chosen according to data volatility.

Avoid caching everything by default.

---

# 25. Plant Identification Architecture

Plant identification is an orchestration workflow.

The application should not expose the AI provider directly to the frontend.

Architecture:

```text id="5t9nqk"
Angular / MAUI
       ↓
POST /api/identification
       ↓
ASP.NET Core
       ↓
Image Validation
       ↓
Image Hash / Cache
       ↓
Identification Service
       ↓
AI Provider Adapter
       ↓
Candidate Generation
       ↓
Plant Database Matching
       ↓
Validation
       ↓
Ranking
       ↓
Identification Result
       ↓
Client
```

---

# 26. AI Provider Abstraction

The AI provider must be replaceable.

Conceptually:

```text id="0f2d4u"
IPlantIdentificationProvider
          │
     ┌────┴────┐
     │         │
Provider A   Provider B
```

The application should depend on:

```text
IPlantIdentificationProvider
```

rather than a specific vendor implementation.

This allows:

* provider replacement
* model experimentation
* testing with fake providers
* cost optimization
* fallback strategies

---

# 27. AI Is Not the Source of Truth

The AI system generates candidate identities.

It does not define authoritative encyclopedia information.

Correct:

```text id="y6x8br"
Image
 ↓
AI candidate
 ↓
Match against known plant
 ↓
Validated plant
 ↓
Encyclopedia information
```

Incorrect:

```text id="6l2w1h"
Image
 ↓
AI generated text
 ↓
Display as authoritative plant-care information
```

Care, taxonomy, and other authoritative information should come from validated application data and trusted sources.

---

# 28. Identification Candidate Ranking

Candidate ranking may consider:

* AI candidate score internally
* database match quality
* taxonomy consistency
* available source evidence
* image/context validation
* duplicate identity rules

The internal score does not need to be exposed to users.

The UI should avoid fake precision such as:

```text
97.83% confidence
```

unless such a number has a defensible meaning and is deliberately designed into the product later.

---

# 29. Identification Caching

Image hashes can be used to detect repeated or identical image requests.

Conceptually:

```text id="n5x7k0"
Image
 ↓
Hash
 ↓
Existing Identification?
 ├── Yes → Cached Result
 └── No
       ↓
      AI
       ↓
     Result
       ↓
     Cache
```

The cache must not incorrectly share private user information.

Privacy and retention rules must be considered when storing identification requests.

---

# 30. Identification Security

Image uploads are an important security boundary.

Validate:

* MIME type
* file size
* file format
* image dimensions
* malformed files
* request frequency

Potential protections:

* rate limiting
* request quotas
* abuse detection
* timeout
* provider cost controls

The backend must not trust the file metadata supplied by the client.

---

# 31. Recommendation Architecture

Recommendations should initially be rule-based.

The recommendation system consumes application behavior signals such as:

* views
* searches
* favorites
* categories
* plant groups
* recent activity

Conceptually:

```text id="t4h7wx"
User Signals
     ↓
Candidate Generation
     ↓
Relevance Scoring
     ↓
Diversity Filtering
     ↓
Plant Deduplication
     ↓
Feed
```

---

# 32. Recommendation Diversity

Recommendation ranking must not be based purely on relevance.

The system should preserve diversity.

For data-rich users, the target guideline is:

```text id="h1f5q3"
60% strongly related
20% somewhat related / adjacent
20% novel / substantially different
```

For new or anonymous users, category representation should remain broadly balanced.

The same plant/cultivar must not appear multiple times in a recommendation feed.

---

# 33. Recommendation Architecture Evolution

The system should evolve gradually:

```text id="7t8y2u"
Phase 1
Rule-based recommendations
        ↓
Phase 2
Improved scoring + behavioral signals
        ↓
Phase 3
More sophisticated ranking
        ↓
Phase 4
Machine-learning recommendation system
```

Machine learning should only be introduced when:

* sufficient behavioral data exists
* a real recommendation problem has been demonstrated
* the additional complexity is justified

---

# 34. Search Architecture

Search should prioritize application data.

Conceptually:

```text id="6n0c5j"
Search Query
     ↓
Normalization
     ↓
PostgreSQL Search
     ↓
Relevant Results
     ↓
No Results?
     ↓
External Ingestion
     ↓
Normalize + Persist
     ↓
Return
```

Search ranking should prioritize query relevance over personalization.

Personalization should not cause a strong search query to return unrelated plants merely because they match a user's previous interests.

---

# 35. Search Suggestions

Suggestions should primarily come from the application's own data.

Potential searchable fields:

* common name
* scientific name
* cultivar
* plant group
* taxonomy-related names

Suggestions should be lightweight and fast.

The system should avoid triggering expensive external API calls for every keystroke.

---

# 36. Favorites Architecture

Favorites are authenticated user data.

Architecture:

```text id="7n1b6d"
Client
 ↓
Authentication
 ↓
Favorites API
 ↓
Authorization
 ↓
PostgreSQL
```

The backend must verify ownership for every modification.

The frontend may optimistically update UI where appropriate, but the backend remains authoritative.

---

# 37. Search and View History

Two separate concepts are maintained:

### Search History

Explicit queries entered or executed by the user.

### View History

Plants the user viewed.

They may influence recommendations differently.

Anonymous activity may be associated with a browser/device identifier where appropriate.

Authenticated activity can be associated with the user's account.

The implementation must minimize unnecessary personal data collection.

---

# 38. Authentication Architecture

Authentication should be handled through a secure, established authentication mechanism rather than a custom password system.

The architecture should support:

* authenticated user identity
* session/token management
* authorization
* secure logout
* protected endpoints

The exact identity provider can be finalized during implementation.

The rest of the application should depend on an application-level authenticated user identity rather than a specific authentication vendor.

---

# 39. Authorization

Authorization must be enforced on the server.

Examples:

```text id="q0f1v7"
User A
  ↓
GET own favorites
  ✓

User A
  ↓
DELETE User B's favorite
  ✗
```

Frontend controls are not security controls.

---

# 40. Where to Buy Architecture

Where to Buy combines plant context with location-aware business information.

Conceptually:

```text id="v3k7n1"
Plant
 ↓
Retailer Search
 ↓
Location
 ↓
Nearby Businesses
 ↓
Sort / Filter
 ↓
Retailer Results
```

Potential sources may include:

* business/location APIs
* curated retailer data
* online retailer data

The application should not claim live inventory unless the provider supports reliable inventory information.

---

# 41. Location Privacy

Location access should be requested only when necessary.

The rest of the application should function without precise location.

Conceptually:

```text id="j9p0m2"
Plant Encyclopedia
      │
      ├── Explore
      ├── Search
      ├── Detail
      └── Favorites
             ↓
       No location required

Where to Buy
      ↓
Location permission
      ↓
Nearby results
```

If location access is denied, the user should still be able to manually select a location.

---

# 42. API Rate Limiting

Rate limiting is particularly important for:

* identification
* external data ingestion
* search endpoints that can trigger external calls
* authentication endpoints

The goal is to protect:

* application infrastructure
* external providers
* AI API budget
* database resources

Rate limits should be introduced according to actual endpoint risk.

---

# 43. Configuration & Secrets

Configuration should be environment-based.

Examples:

```text id="3r4x1j"
DATABASE_CONNECTION_STRING
AI_PROVIDER_API_KEY
STORAGE_BUCKET
AUTH_CONFIGURATION
EXTERNAL_PROVIDER_CONFIGURATION
CACHE_CONFIGURATION
```

Secrets must never be committed to Git.

Development secrets should use an appropriate local secret mechanism.

Production secrets should use appropriate cloud secret/configuration management.

---

# 44. Environment Architecture

At minimum:

```text id="4s0l8e"
Development
Test
Production
```

Each environment should have independent configuration.

Production data must never be casually used as a development database.

---

# 45. Docker Architecture

Docker should provide reproducible development and deployment environments.

Local development may include:

```text id="1j4b7p"
Angular
ASP.NET Core
PostgreSQL
```

The exact containerization strategy may evolve.

Docker should not be introduced merely for resume keywords.

Its purpose is:

* reproducibility
* environment consistency
* deployment portability
* easier onboarding

---

# 46. CI/CD Architecture

GitHub Actions is the intended CI/CD platform.

Basic pipeline:

```text id="n2x5k8"
Git Push / Pull Request
        ↓
Install Dependencies
        ↓
Build
        ↓
Lint / Format
        ↓
Unit Tests
        ↓
Integration Tests
        ↓
Package
        ↓
Deploy
```

Deployment should only occur after required quality gates pass.

---

# 47. Deployment Architecture

The production architecture should remain as simple as practical.

Conceptually:

```text id="6b9p3m"
User
 ↓
HTTPS
 ↓
Frontend Hosting / CDN
 ↓
ASP.NET Core API
 ├── PostgreSQL
 ├── Object Storage
 ├── Cache
 └── External Providers
```

The exact AWS services should be selected after evaluating:

* cost
* operational complexity
* scalability
* deployment experience
* project requirements

Avoid selecting AWS services solely because they appear on a job description.

---

# 48. Health Checks

The backend should expose a health endpoint:

```text
GET /health
```

Health checks may eventually verify:

* application availability
* database connectivity
* critical infrastructure dependencies

External AI/provider availability should not necessarily make the entire application unhealthy.

A temporary third-party outage is different from the backend being unavailable.

---

# 49. Logging

Logs should help answer:

* what happened?
* when did it happen?
* which request was involved?
* which component failed?
* how long did it take?

Use structured logging where practical.

Potential useful metadata:

* request/correlation ID
* endpoint
* duration
* status code
* provider
* operation type

Do not log:

* passwords
* API keys
* authentication tokens
* unnecessary private images
* unnecessary personal information

---

# 50. Correlation IDs

Requests should eventually have a correlation/request identifier.

Conceptually:

```text id="x4t7s9"
Frontend
  Request ID: ABC123
       ↓
ASP.NET Core
  ABC123
       ↓
Database / Provider
  ABC123
```

This makes production debugging easier.

---

# 51. Error Architecture

Errors should be handled at appropriate boundaries.

Example:

```text id="f9x2m7"
External Provider
      ↓
Provider Exception
      ↓
Infrastructure Error
      ↓
Application Error
      ↓
API Error Response
      ↓
Frontend Error State
```

The user should receive a useful message.

The user should not receive:

```text
NullReferenceException
```

or an external provider stack trace.

---

# 52. Transaction Boundaries

Database transactions should be used when multiple related changes must succeed or fail together.

Examples:

* creating a plant and required relational records
* processing certain ingestion operations
* changing favorite ordering
* updating related identification records

Do not wrap every request in a large transaction unnecessarily.

---

# 53. Concurrency

Concurrency should be considered for:

* favorite ordering
* simultaneous ingestion
* duplicate plant creation
* identification caching
* user actions from multiple devices

Database constraints should provide important protection against duplicate or invalid data.

Application-level checks alone are not sufficient for race-condition-sensitive uniqueness.

---

# 54. Background Processing

The initial application should avoid unnecessary background infrastructure.

Background processing may eventually be appropriate for:

* large-scale ingestion
* image processing
* recommendation generation
* data refresh
* cleanup jobs
* asynchronous identification

For the MVP, synchronous operations should be preferred when latency and provider behavior make them practical.

Introduce queues/workers only when there is a demonstrated need.

---

# 55. Scalability Strategy

The architecture should scale incrementally.

Initial strategy:

```text id="b2m6n4"
Vertical simplicity
+
Database indexes
+
Caching
+
Efficient queries
+
Image optimization
```

Later:

```text id="k9p1w3"
Background workers
+
Queues
+
CDN
+
Horizontal scaling
+
Dedicated services
```

Do not build the second architecture before the first architecture has a demonstrated scaling problem.

---

# 56. Performance Boundaries

Performance priorities are:

1. user interaction
2. navigation
3. API response
4. image loading
5. scrolling
6. decorative animation

Architecture should support:

* pagination
* lazy loading
* efficient projections
* caching
* optimized images
* CDN usage where appropriate
* database indexing

Performance budgets should be defined and measured during the testing/performance stage rather than invented without measurement.

---

# 57. Accessibility Architecture

Accessibility is primarily implemented at the UI layer but should influence component architecture.

Reusable components should support:

* semantic markup
* keyboard navigation
* focus management
* accessible labels
* appropriate ARIA only where necessary
* reduced motion
* non-hover alternatives

Accessibility should not be added as a final visual patch.

---

# 58. Security Boundaries

Important trust boundaries are:

```text id="w0s8q2"
User Device
     ↓
HTTPS
     ↓
API
     ↓
Application
     ↓
Database / Storage / External APIs
```

Never trust:

* client-side authorization
* client-provided IDs
* uploaded filenames
* MIME types without validation
* external API data without validation
* AI-generated claims
* location data without appropriate handling

---

# 59. Data Source Trust Model

The application uses different levels of trust.

Conceptually:

```text id="8v5j0d"
Trusted / validated application data
            ↑
       Normalized source data
            ↑
      External providers
            ↑
       AI candidates
```

AI candidates are therefore not equivalent to verified encyclopedia information.

This distinction is fundamental to the product.

---

# 60. External Provider Isolation

All external services should be isolated behind interfaces/adapters where practical.

Examples:

```text id="x2h7q9"
IPlantDataProvider
IPlantIdentificationProvider
IImageStorage
IRetailerProvider
ICache
```

Benefits:

* easier testing
* easier provider replacement
* reduced vendor lock-in
* cleaner application code
* easier mocking/faking

---

# 61. Testing Architecture

Testing should mirror architectural boundaries.

```text id="m6n8v1"
Domain
  ↓
Unit tests

Application
  ↓
Unit + integration tests

Infrastructure
  ↓
Integration tests

API
  ↓
API integration tests

Frontend
  ↓
Component + integration + E2E tests

Critical user journeys
  ↓
E2E tests
```

Tests should focus on behavior rather than implementation details.

---

# 62. Test Doubles for External Providers

External APIs should not be required for every test.

For example:

```text id="v8q3j1"
Identification Service
       ↓
Fake IPlantIdentificationProvider
       ↓
Deterministic test candidates
```

This makes tests:

* faster
* cheaper
* deterministic
* independent of provider outages

Real provider integration tests should be limited and controlled.

---

# 63. Architecture Evolution

The architecture is intentionally designed to evolve.

Expected evolution:

```text id="0s9c4m"
MVP
Modular Monolith
     ↓
Better caching
     ↓
Background jobs
     ↓
Improved recommendation system
     ↓
More advanced infrastructure
```

Potential future separation into services should only occur when there is a clear reason.

Possible future candidates:

* identification worker
* ingestion worker
* recommendation service
* image processing worker

These are future options, not MVP requirements.

---

# 64. Architecture Decision Guidelines

When choosing between two technical approaches, prefer the option that:

1. solves the current product requirement
2. is understandable
3. is testable
4. has a clear failure mode
5. is reasonably performant
6. does not create unnecessary operational burden
7. can be replaced later if necessary

Complexity must have a reason.

---

# 65. What the Architecture Intentionally Avoids

The MVP architecture does not require:

* microservices
* Kubernetes
* event-driven architecture everywhere
* machine-learning recommendation models
* custom authentication infrastructure
* real-time WebSockets
* distributed databases
* complex message queues
* multi-region deployment
* elaborate service meshes

These may be valid technologies in other systems.

They are not automatically appropriate for this project.

---

# 66. Recommended Initial Backend Structure

A possible implementation structure is:

```text id="7r2q6m"
backend/
├── PlantEncyclopedia.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Configuration/
│   └── Program.cs
│
├── PlantEncyclopedia.Application/
│   ├── Plants/
│   ├── Search/
│   ├── Explore/
│   ├── Favorites/
│   ├── Identification/
│   ├── Retailers/
│   └── Common/
│
├── PlantEncyclopedia.Domain/
│   ├── Plants/
│   ├── Users/
│   ├── Identification/
│   └── Common/
│
├── PlantEncyclopedia.Infrastructure/
│   ├── Persistence/
│   ├── ExternalProviders/
│   ├── Storage/
│   ├── Caching/
│   └── Authentication/
│
└── PlantEncyclopedia.Tests/
```

This is a starting point, not a rigid requirement.

The actual structure should be adjusted if implementation reveals a simpler and clearer organization.

---

# 67. Recommended Frontend Structure

A possible structure is:

```text id="q7w4p1"
frontend/
└── src/
    └── app/
        ├── core/
        │   ├── api/
        │   ├── auth/
        │   └── services/
        │
        ├── layout/
        │   ├── sidebar/
        │   ├── top-bar/
        │   └── shell/
        │
        ├── shared/
        │   ├── components/
        │   ├── directives/
        │   └── utilities/
        │
        └── features/
            ├── explore/
            ├── search/
            ├── categories/
            ├── plant-detail/
            ├── identification/
            ├── favorites/
            └── where-to-buy/
```

The exact Angular architecture should follow the framework version and implementation needs at project initialization.

---

# 68. Recommended Request Flow

Example: opening a Plant Detail page.

```text id="e4s7y2"
User clicks plant
        ↓
Angular Router
        ↓
Plant Detail Component
        ↓
Plant API Client
        ↓
GET /api/plants/{plantId}
        ↓
ASP.NET Core Endpoint
        ↓
Application Service
        ↓
EF Core Query
        ↓
PostgreSQL
        ↓
Plant DTO
        ↓
JSON Response
        ↓
Angular
        ↓
Plant Detail UI
```

---

# 69. Recommended Identification Flow

```text id="b8f2m5"
User selects image
        ↓
Angular / MAUI
        ↓
POST /api/identification
        ↓
API Validation
        ↓
Identification Application Service
        ↓
Image Validation
        ↓
Image Hash
        ↓
Cache
        ↓
AI Provider Adapter
        ↓
AI Provider
        ↓
Candidate Names
        ↓
Plant Database Matching
        ↓
Validation + Ranking
        ↓
Identification Result
        ↓
Client
```

This flow keeps AI behind the application boundary.

---

# 70. Recommended Search Flow

```text id="p6w3n8"
User enters query
        ↓
Angular
        ↓
GET /api/search
        ↓
Search Application Service
        ↓
PostgreSQL
        ↓
Results
        ↓
Found?
 ├── Yes → Return
 │
 └── No
      ↓
 External Provider
      ↓
 Normalize
      ↓
 Validate
      ↓
 Persist
      ↓
 Return
```

The application should avoid calling external providers unnecessarily.

---

# 71. Recommended Explore Flow

```text id="c5r8y1"
User opens Explore
        ↓
GET /api/explore
        ↓
Recommendation / Discovery Service
        ↓
Candidate Generation
        ↓
Relevance
        ↓
Diversity
        ↓
Deduplication
        ↓
Pagination
        ↓
Response
        ↓
Angular Grid
```

Feed composition should remain fast enough for normal browsing.

---

# 72. Recommended Favorites Flow

```text id="n8q2k5"
User clicks Bookmark
        ↓
Angular
        ↓
POST /api/favorites
        ↓
Authentication
        ↓
Authorization
        ↓
Favorite validation
        ↓
PostgreSQL
        ↓
Success
        ↓
UI update
```

The server remains authoritative.

---

# 73. Architecture and Documentation Relationship

The project documents have distinct responsibilities:

```text id="4k7v2n"
PRODUCT_REQUIREMENTS.md
"What are we building?"

        ↓

UX_SPECIFICATION.md
"How should users experience it?"

        ↓

DECISIONS.md
"Why did we choose this behavior/approach?"

        ↓

ARCHITECTURE.md
"How is the system structured?"

        ↓

DATABASE_DESIGN.md
"How is information stored?"

        ↓

API_SPEC.md
"How do clients communicate with the backend?"

        ↓

DEVELOPMENT_ROADMAP.md
"In what order do we build it?"
```

No single document should become a replacement for all others.

---

# 74. Architecture Quality Bar

Before calling the architecture stable enough for implementation, verify:

* frontend and backend responsibilities are clear
* backend business logic is not concentrated in controllers
* database entities are not public API contracts
* external providers are isolated
* AI is isolated behind an abstraction
* PostgreSQL is the application persistence layer
* images are separated from relational metadata
* authentication and authorization boundaries are clear
* recommendation logic is independent from UI
* external failures are handled
* rate limiting can be introduced
* testing boundaries are defined
* deployment can evolve without rewriting the application
* MVP complexity remains manageable

---

# 75. Final Architecture Principle

Plant Encyclopedia should begin as a **well-structured modular monolith with clear boundaries**.

The core architecture is:

```text id="h0p4x6"
                   ┌──────────────────────┐
                   │    Web / Android     │
                   └──────────┬───────────┘
                              │
                         HTTPS / JSON
                              │
                   ┌──────────▼───────────┐
                   │    ASP.NET Core API  │
                   └──────────┬───────────┘
                              │
              ┌───────────────┼────────────────┐
              │               │                │
              ▼               ▼                ▼
        Application        Domain       Infrastructure
              │                                │
              │                ┌───────────────┼──────────────┐
              │                │               │              │
              ▼                ▼               ▼              ▼
        Business Rules    PostgreSQL      Object Storage   External APIs
                                              │              │
                                              │        ┌───────┼────────┐
                                              │        │       │        │
                                              │     iNaturalist GBIF   AI
                                              │
                                              └──── Images
```

The architecture should remain simple until complexity is justified by a real requirement.

The guiding principle is:

> **Start simple, enforce boundaries, and add complexity only when the product proves that it is necessary.**
