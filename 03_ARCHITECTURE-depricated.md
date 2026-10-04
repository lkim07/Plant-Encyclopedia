# Plant Encyclopedia — Architecture

This document defines the high-level technical architecture for Plant Encyclopedia.

It translates the product and UX decisions into a maintainable software architecture.

This document should guide implementation without prematurely over-specifying details that belong in lower-level technical documentation.

---

# 1. Architecture Goals

The architecture should support the following goals:

1. **Maintainability**
2. **Clear separation of responsibilities**
3. **Reliable plant data**
4. **AI-assisted plant identification**
5. **Fast image-heavy experiences**
6. **Secure user accounts and saved data**
7. **Testability**
8. **Scalable external API integration**
9. **Cloud deployment**
10. **AI-assisted development without losing engineering quality**

The architecture should be appropriate for a portfolio project while following practices that resemble a real production application.

---

# 2. Core Architectural Principle

The system should separate:

```text
Presentation
    ↓
Application / API
    ↓
Domain Logic
    ↓
Persistence
    ↓
External Services
```

The frontend should not directly depend on external plant data providers.

The backend should act as the application's primary coordination layer.

---

# 3. High-Level System

The intended architecture is:

```text
                         ┌──────────────────────┐
                         │       User           │
                         └──────────┬───────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │                               │
             Web Application                 Mobile Application
               Angular/TS                     .NET MAUI/C#
                    │                               │
                    └───────────────┬───────────────┘
                                    │
                              HTTPS / JSON
                                    │
                           ┌────────▼────────┐
                           │  ASP.NET Core   │
                           │      API        │
                           └────────┬────────┘
                                    │
              ┌─────────────────────┼──────────────────────┐
              │                     │                      │
       ┌──────▼──────┐      ┌──────▼───────┐      ┌──────▼───────┐
       │ Application │      │ Domain Logic  │      │ Integration  │
       │ Services    │      │               │      │ Services     │
       └──────┬──────┘      └──────┬────────┘      └──────┬───────┘
              │                    │                      │
              └────────────────────┼──────────────────────┘
                                   │
                            ┌──────▼──────┐
                            │ PostgreSQL  │
                            └─────────────┘
                                   │
                         ┌─────────┴─────────┐
                         │                   │
                  External Plant APIs    AI Vision API
                  iNaturalist / GBIF
```

AWS infrastructure will support deployment, storage, networking, monitoring, and other production concerns as appropriate.

---

# 4. Frontend Architecture

## 4.1 Web

**Technology direction:**

* Angular
* TypeScript
* HTML
* CSS

### Responsibilities

The web application is responsible for:

* Rendering the user interface
* Navigation
* User interactions
* Client-side state
* Form validation
* API communication
* Loading states
* Error states
* Responsive layouts
* Accessibility
* Image presentation

The web client should not contain authoritative plant data logic.

---

## 4.2 Mobile

**Technology direction:**

* .NET MAUI
* C#
* Android first

### Responsibilities

The mobile application should consume the same backend APIs as the web application.

It should not implement a separate plant database or duplicate backend business rules.

### Initial Platform

Android is the first mobile target.

iOS support is deferred because development currently does not have access to a Mac environment.

---

# 5. Backend Architecture

## 5.1 Technology Direction

The backend currently favors:

* C#
* ASP.NET Core
* Entity Framework Core
* PostgreSQL

The backend is the primary application orchestration layer.

---

## 5.2 Backend Responsibilities

The backend is responsible for:

* API endpoints
* Authentication and authorization
* Plant data retrieval
* Plant search
* Plant ingestion
* Data normalization
* Data validation
* Recommendation logic
* Favorites
* Search history
* View history
* Identification orchestration
* External API integration
* AI integration
* Location-based retailer search
* Caching
* Rate limiting
* Error handling
* Logging
* Security
* Persistence

---

# 6. Recommended Backend Layering

The backend should maintain clear boundaries.

A conceptual structure is:

```text
API / Controllers
        ↓
Application Services
        ↓
Domain
        ↓
Infrastructure
```

## 6.1 API Layer

Responsible for:

* HTTP endpoints
* Request validation
* Authentication checks
* Response formatting
* HTTP status codes

The API layer should remain thin.

Business logic should not be placed directly inside controllers.

---

## 6.2 Application Layer

Responsible for application use cases.

Examples:

* SearchPlants
* GetPlantDetails
* IdentifyPlant
* GetExploreFeed
* AddFavorite
* RemoveFavorite
* ReorderFavorites
* GetSimilarPlants
* FindNearbyRetailers

Application services coordinate domain logic and infrastructure services.

---

## 6.3 Domain Layer

Contains core business concepts and rules.

Examples:

* Plant
* Cultivar
* PlantGroup
* Category
* Favorite
* SearchHistory
* ViewHistory
* IdentificationResult

Business rules such as recommendation diversity should not depend on HTTP or database implementation details.

---

## 6.4 Infrastructure Layer

Responsible for external systems.

Examples:

* PostgreSQL
* Entity Framework Core
* iNaturalist client
* GBIF client
* AI provider client
* Image storage
* Cache
* Retailer/location providers
* Email or authentication infrastructure if required

Infrastructure implementations should be replaceable where practical.

---

# 7. Database Architecture

## 7.1 Primary Database

PostgreSQL is the intended primary relational database.

It should store persistent application data.

Potential major entities include:

```text
Plant
PlantCategory
PlantGroup
Taxonomy
Cultivar
PlantImage
PlantCare
PlantBlooming
PlantHealth
PlantAbout
PlantSource
Favorite
SearchHistory
ViewHistory
IdentificationRequest
IdentificationCandidate
Retailer
RetailerLocation
SimilarPlant
User
```

The exact schema is defined in `DATABASE_DESIGN.md`.

---

## 7.2 Database as Source of Truth

The application database is the primary source for user-facing plant information.

External data should be imported into the application's normalized model.

The frontend should not depend on external provider schemas.

---

# 8. External Plant Data Integration

## 8.1 Provider Abstraction

External providers should be accessed through dedicated integration services.

Conceptually:

```text
PlantProvider
    ├── iNaturalistProvider
    └── GBIFProvider
```

The application should avoid spreading provider-specific API logic throughout the codebase.

---

## 8.2 Search and Ingestion Flow

When a user searches for a plant:

```text
User Search
    ↓
Backend Search Service
    ↓
Search Own Database
    ↓
Found?
 ┌──┴──┐
Yes   No
 │     │
 ↓     ↓
Return  Query External Providers
              ↓
          Normalize Data
              ↓
          Validate Data
              ↓
          Save to Database
              ↓
          Return Plant
```

This keeps external APIs from becoming a direct dependency of the user interface.

---

# 9. Plant Identification Architecture

Plant identification is a separate workflow from normal plant search.

## 9.1 Identification Pipeline

Conceptually:

```text
User Image
    ↓
Image Validation
    ↓
Image Hash / Cache Check
    ↓
AI Vision API
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
Identification Results
```

---

## 9.2 AI Responsibility

The AI provider is responsible for interpreting the image and generating possible plant candidates.

The application remains responsible for deciding which candidates are valid user-facing results.

---

## 9.3 Candidate Validation

A candidate should ideally be mapped to an existing or newly ingested plant entity.

Conceptually:

```text
AI Candidate
    ↓
Taxonomic / Name Matching
    ↓
Known Plant?
 ┌──┴──┐
Yes   No
 │     │
 ↓     ↓
Use    Attempt validated ingestion
Known  / reject candidate
Entity
```

The application should not display arbitrary AI-generated names as authoritative encyclopedia entries.

---

# 10. Identification Caching

Identification can be expensive because AI inference may incur API cost.

The architecture should therefore support caching.

Potential cache key:

```text
image_hash + identification_version
```

The exact strategy should be determined during implementation.

### Goals

Caching should:

* Reduce repeated AI requests
* Reduce latency
* Reduce API cost
* Improve reliability

### Important

Cache is an optimization.

It is not the authoritative plant database.

---

# 11. Image Architecture

Plant Encyclopedia is image-heavy, so image architecture is a major concern.

## 11.1 Image Storage

Images should not normally be stored directly inside PostgreSQL as large binary objects.

Instead:

```text
Application
    ↓
Object Storage
    ↓
CDN / Optimized Delivery
```

AWS object storage such as S3 is the intended direction.

The database should store metadata and references to images.

---

## 11.2 Image Variants

Where appropriate, the system should support multiple image sizes:

```text
Original
   ↓
Large
   ↓
Medium
   ↓
Thumbnail
```

The frontend should request an appropriate size for the display context.

---

## 11.3 Image Performance

Implementation should consider:

* WebP
* AVIF
* Responsive image sizes
* Lazy loading
* Thumbnail generation
* CDN delivery
* Proper compression
* Avoiding unnecessarily large downloads

---

# 12. Caching Architecture

Caching may exist at multiple levels.

Potential layers:

```text
Browser / Mobile Cache
        ↓
CDN Cache
        ↓
Application Cache
        ↓
PostgreSQL
        ↓
External APIs
```

Not every layer needs to be implemented in the MVP.

Caching should be introduced where it provides measurable value.

---

# 13. Recommendation Architecture

## 13.1 MVP Recommendation Engine

The MVP uses a rule-based recommendation system.

Inputs may include:

* Recently viewed plants
* Favorite plants
* Search activity
* Categories viewed
* Plant groups viewed
* Recent interactions

---

## 13.2 Recommendation Pipeline

Conceptually:

```text
User Signals
    ↓
Candidate Generation
    ↓
Remove Exact / Recently Seen Items
    ↓
Apply Category Diversity
    ↓
Apply Plant/Cultivar Deduplication
    ↓
Apply Relatedness
    ↓
Apply Novelty
    ↓
Rank
    ↓
Return Feed
```

---

## 13.3 Personalized Mix

For users with sufficient behavioral data, the target conceptual mix is:

```text
60% Strongly Related
20% Adjacent / Somewhat Related
20% Novel / Different
```

This is a guideline rather than a hard mathematical requirement.

---

## 13.4 New User Strategy

For new or anonymous users:

* Maintain broad category representation.
* Favor curated discovery.
* Avoid excessive popularity bias.
* Avoid purely random results.
* Prevent duplicate plant/cultivar entities in the same feed.

---

# 14. Search Architecture

Search should primarily use the application's own database.

Potential searchable fields include:

* Common name
* Scientific name
* Cultivar name
* Plant group
* Taxonomic information
* Synonyms where available

Conceptually:

```text
User Query
    ↓
Normalize Query
    ↓
Database Search
    ↓
Rank Results
    ↓
Return Results
```

If the plant does not exist locally, the backend may trigger external ingestion.

Search relevance should take priority over recommendation personalization.

---

# 15. Favorites Architecture

Favorites are authenticated user data.

Conceptually:

```text
User
  │
  └── Favorites
          │
          ├── Plant A
          ├── Plant B
          └── Plant C
```

The system should store an explicit logical ordering value.

Example conceptual model:

```text
Favorite
---------
UserId
PlantId
Order
CreatedAt
```

Folders are deferred.

---

# 16. User History Architecture

The system distinguishes between:

### Search History

Explicit searches performed by the user.

### View History

Plants actually viewed.

These are separate concepts and should not be merged.

Both can influence recommendations.

Anonymous history may initially be stored using a browser/device identifier and can later be associated with an authenticated account where appropriate.

---

# 17. Authentication and Authorization

Authentication is required for persistent Favorites.

The system should distinguish between:

* Anonymous user
* Authenticated user

Authorization must be enforced server-side.

The frontend must not be treated as a trusted security boundary.

Examples:

```text
Anonymous
├── Explore
├── Search
├── Identify
├── Plant Detail
└── View public content

Authenticated
├── Everything above
├── Favorites
├── Persistent history
└── Account features
```

Exact authentication technology should be finalized before implementation.

---

# 18. API Architecture

The backend exposes a REST-oriented HTTP API.

The frontend applications should communicate with the backend through stable API contracts.

Conceptual endpoint groups:

```text
/api/plants
/api/search
/api/explore
/api/categories
/api/plant-groups
/api/identification
/api/favorites
/api/history
/api/retailers
/api/users
```

The exact API contract belongs in `API_SPEC.md`.

---

# 19. Error Handling

The backend should use consistent error responses.

Errors should contain enough information for the frontend to:

* Display an appropriate message
* Offer recovery
* Distinguish validation errors from server failures

The API should not expose internal implementation details, stack traces, secrets, or provider-specific failures directly to users.

---

# 20. Rate Limiting

AI identification creates a potential abuse and cost risk.

The architecture should support rate limiting, especially for:

* Anonymous identification
* Authenticated identification
* Expensive external API calls

Rate limits should be introduced before public deployment of expensive functionality.

Exact limits are deferred until usage assumptions are known.

---

# 21. Security Principles

Security should be considered from the beginning.

Important principles include:

* Never trust client input.
* Validate uploaded images.
* Validate API request bodies.
* Protect authentication tokens.
* Store secrets in environment/configuration systems rather than source control.
* Never commit API keys.
* Apply authorization server-side.
* Prevent unauthorized access to Favorites.
* Sanitize or validate external data.
* Avoid exposing internal provider errors.
* Apply rate limiting to expensive operations.
* Use HTTPS in deployed environments.
* Keep dependencies updated.

---

# 22. Configuration and Secrets

Environment-specific configuration should not be hard-coded.

Examples:

```text
Development
Staging
Production
```

Sensitive values may include:

* Database credentials
* AI API keys
* External provider credentials
* AWS credentials
* Authentication secrets

These should be managed through environment variables or appropriate cloud secret-management mechanisms.

---

# 23. Observability

The production application should eventually support:

* Structured logging
* Error tracking
* Request tracing where appropriate
* API latency monitoring
* External API failure monitoring
* AI request/error monitoring
* Database performance monitoring

Observability is important because external API failures may occur independently of application failures.

---

# 24. CI/CD Architecture

GitHub Actions is the intended CI/CD direction.

A conceptual pipeline is:

```text
Git Push / Pull Request
        ↓
Install Dependencies
        ↓
Lint / Format Checks
        ↓
Unit Tests
        ↓
Integration Tests
        ↓
Build
        ↓
Security / Quality Checks
        ↓
Deploy
```

Deployment should not occur if required quality gates fail.

The exact deployment strategy will be defined after the application architecture is implemented.

---

# 25. Environment Strategy

The project should distinguish between environments.

At minimum:

```text
Local Development
        ↓
Test / CI
        ↓
Production
```

A staging environment may be introduced when it provides sufficient value.

Local development should be possible without requiring production infrastructure.

---

# 26. Docker

Docker should be used where it improves consistency between development and deployment.

Likely candidates include:

* Backend
* PostgreSQL
* Supporting services

Docker should not be added merely for technology demonstration.

The goal is reproducible environments.

---

# 27. AWS Architecture Direction

AWS is the intended cloud platform.

Potential services may include:

* Compute/container hosting
* S3 for object storage
* CDN
* Managed PostgreSQL
* Secrets management
* Monitoring/logging
* Networking

The exact AWS services should be selected based on:

1. Simplicity
2. Cost
3. Security
4. Operational burden
5. Portfolio value
6. Actual application requirements

Do not introduce unnecessary AWS services solely to make the architecture look more complex.

---

# 28. Local Development Architecture

Developers should be able to run the core system locally.

A conceptual local environment:

```text
Angular Dev Server
        │
        ↓
ASP.NET Core API
        │
        ↓
PostgreSQL
```

External APIs can be configured through development environment variables.

Mock/fake services may be used for tests where external API calls are undesirable.

---

# 29. Testing Architecture

Testing should occur at multiple levels.

## Unit Tests

Test isolated business logic.

Examples:

* Recommendation scoring
* Category diversity
* Candidate ranking
* Plant matching
* Validation rules

## Integration Tests

Test interactions between:

* API
* Database
* External service abstractions

## End-to-End Tests

Test important user journeys.

Examples:

```text
Explore → Plant Detail
Search → Plant Detail
Identify → Results → Detail
Login → Favorite
Where to Buy → Retailer
```

Not every UI interaction needs an end-to-end test.

Testing should focus on important product behavior.

---

# 30. External Service Abstraction

External services should be isolated behind interfaces or equivalent abstractions.

Conceptually:

```text
IPlantProvider
    ├── INaturalistProvider
    └── GBIFProvider

IPlantIdentificationProvider
    └── AIIdentificationProvider

IRetailerProvider
    └── RetailerProviderImplementation
```

This makes it easier to:

* Replace providers
* Mock services
* Test failure scenarios
* Avoid vendor lock-in

---

# 31. Failure Handling

External services are not guaranteed to be available.

The application should handle:

* Timeouts
* Rate limits
* Invalid responses
* Temporary provider failures
* Partial data
* AI failures
* Database failures

A failure in one external provider should not unnecessarily crash the entire application.

Where possible, the application should degrade gracefully.

---

# 32. Data Ingestion Strategy

Plant ingestion should be controlled rather than unrestricted.

Conceptually:

```text
External Data
    ↓
Provider Adapter
    ↓
Normalization
    ↓
Validation
    ↓
Duplicate Detection
    ↓
Persistence
    ↓
Application Data
```

The system should avoid creating duplicate plant entities when multiple providers refer to the same biological entity.

---

# 33. Data Consistency

The system should distinguish between:

* External identity
* Internal plant identity
* Display names
* Taxonomic names
* Cultivar identity

A provider's identifier should not automatically become the application's primary identifier.

The application's internal identifiers should remain stable even if external providers change.

---

# 34. Performance Architecture

The system should prioritize performance for the image-heavy user experience.

Important areas include:

### Frontend

* Lazy loading
* Responsive images
* Efficient rendering
* Minimal unnecessary API requests
* Appropriate caching

### Backend

* Database indexes
* Pagination
* Efficient queries
* Avoiding N+1 queries
* Caching expensive operations

### Infrastructure

* CDN where justified
* Object storage
* Compression
* Connection management

---

# 35. Pagination and Infinite Scroll

Explore and Search should support progressive loading.

The frontend should not request the entire plant catalog at once.

Conceptually:

```text
Initial Request
    ↓
12–20 Cards
    ↓
User Scrolls
    ↓
Next Page
    ↓
Next Page
    ↓
...
```

The exact pagination strategy should be determined during API design.

---

# 36. API and Database Boundary

The frontend should receive application-specific DTOs rather than raw database entities.

Conceptually:

```text
Database Entity
      ↓
Application Mapping
      ↓
Response DTO
      ↓
Frontend
```

This prevents the database schema from becoming the public API contract.

---

# 37. DTO and Domain Separation

The system should avoid exposing internal domain/database objects directly through API endpoints.

Benefits include:

* Stable API contracts
* Better security
* Controlled response size
* Easier schema evolution
* Separation of concerns

---

# 38. Mobile and Web API Reuse

Web and mobile should consume the same backend API wherever practical.

```text
                    ┌───────────────┐
                    │ ASP.NET Core  │
                    │     API       │
                    └───────┬───────┘
                            │
              ┌─────────────┴─────────────┐
              │                           │
        Angular Web                 .NET MAUI
```

### Rationale

Duplicating backend logic between web and mobile would increase maintenance cost and create inconsistent behavior.

---

# 39. Architecture Boundaries

The following boundaries should remain clear:

### Frontend

Presentation and user interaction.

### Backend API

Application orchestration and security boundary.

### Domain

Business rules.

### Database

Persistent application state.

### External Providers

Third-party information and capabilities.

### AI

Identification assistance.

### Infrastructure

Deployment, storage, networking, observability, and operational services.

---

# 40. What the Architecture Should Avoid

The project should avoid:

* Frontend directly calling multiple plant APIs
* Raw third-party API responses exposed to users
* Business logic embedded in UI components
* Large controllers containing all business logic
* Database entities exposed directly as public API responses
* AI-generated authoritative care content
* Hard-coded API keys
* Unnecessary microservices
* Premature distributed systems
* Excessive AWS services
* Complex ML recommendation systems before basic recommendations are validated
* Overengineering for hypothetical scale

---

# 41. Monolith vs Microservices

**Decision:** Use a modular monolith for the initial application.

### Rationale

The application does not currently justify independent deployable services.

A modular backend provides:

* Simpler development
* Easier local setup
* Easier debugging
* Lower infrastructure cost
* Clear architectural boundaries

The system can later extract services if real requirements justify doing so.

---

# 42. Scalability Philosophy

The architecture should be capable of growing without being designed for hypothetical massive scale from day one.

The first scaling priorities should be:

1. Efficient database queries
2. Proper indexes
3. Pagination
4. Caching
5. Image optimization
6. CDN
7. Rate limiting
8. Background processing where justified
9. Horizontal application scaling if necessary

Only after these are insufficient should significantly more complex architecture be considered.

---

# 43. Background Processing

Some operations may eventually be moved to asynchronous/background processing.

Potential examples:

* Large-scale plant ingestion
* Image processing
* Image variant generation
* Recommendation refresh
* Data synchronization
* Expensive external API operations

The MVP should introduce background processing only when synchronous processing becomes a real problem.

---

# 44. Async User Experience

Long-running operations should not block the user's core navigation unnecessarily.

For example:

```text
User submits image
        ↓
Identification request
        ↓
Loading state
        ↓
Result
```

If a future operation becomes too expensive for synchronous handling, the API may evolve toward asynchronous job processing.

The UX should remain understandable regardless of implementation strategy.

---

# 45. Architecture Evolution

This architecture is intentionally designed to evolve.

Potential future changes include:

* More plant data providers
* More AI providers
* More sophisticated recommendation algorithms
* Advanced image similarity
* More retailer providers
* Additional mobile platforms
* More cloud infrastructure
* Background processing
* Search indexing infrastructure

Such additions should be justified by actual product requirements.

---

# 46. Implementation Priority

Architecture should be implemented in stages.

Recommended order:

```text
1. Backend foundation
2. PostgreSQL / EF Core
3. Plant domain model
4. Plant ingestion
5. Search API
6. Plant Detail API
7. Angular application foundation
8. Explore / Category / Search
9. Authentication
10. Favorites
11. Identification pipeline
12. Where to Buy
13. Similar Plants
14. Recommendation logic
15. Testing expansion
16. CI/CD
17. AWS deployment
18. Performance / observability improvements
```

The exact order may change based on implementation findings.

---

# 47. Architecture Quality Bar

Before considering the architecture implementation-ready, the project should have clear answers for:

* How does the frontend communicate with the backend?
* What is the application's source of truth?
* How are external plant providers isolated?
* How is AI identification validated?
* How are images stored and delivered?
* How are users authenticated?
* How are Favorites protected?
* How are recommendations generated?
* How are external failures handled?
* How are expensive AI operations rate-limited?
* How is the database queried efficiently?
* How are APIs tested?
* How is the application deployed?
* How are secrets managed?
* How are errors and performance monitored?

If an implementation decision cannot be answered by this document, the appropriate lower-level document should define it rather than adding unnecessary detail here.

---

# 48. Relationship to Other Project Documents

This document depends on:

* `PRODUCT_REQUIREMENTS.md`
* `UX_SPECIFICATION.md`
* `DECISIONS.md`

This document provides the foundation for:

* `DATABASE_DESIGN.md`
* `API_SPEC.md`
* `DEVELOPMENT_ROADMAP.md`
* `PROJECT_INSTRUCTIONS.md`
* `CURRENT_STATE.md`
* `TODO.md`

The architecture must not contradict accepted product or UX decisions.

---

# 49. Guiding Architecture Principle

> **Build a simple, modular, testable system that can support a real consumer product without prematurely designing for hypothetical scale.**

The architecture should make Plant Encyclopedia:

* trustworthy,
* fast,
* maintainable,
* secure,
* testable,
* deployable,
* and understandable by both a human developer and AI-assisted development tools.
