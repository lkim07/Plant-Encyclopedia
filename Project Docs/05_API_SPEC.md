# Plant Encyclopedia — API Specification

This document defines the API contract for Plant Encyclopedia.

The API is the shared backend interface for:

* Angular Web
* .NET MAUI Mobile
* Future clients

The API should expose application-level resources and behavior rather than directly exposing database entities or third-party provider responses.

---

# 1. API Goals

The API should provide:

1. Stable contracts for frontend clients
2. Clear separation from database implementation
3. Consistent authentication behavior
4. Predictable error responses
5. Efficient pagination
6. Safe external API integration
7. Support for anonymous discovery
8. Authenticated user features
9. AI-assisted plant identification
10. Future extensibility

---

# 2. API Style

The initial API follows a REST-oriented HTTP architecture.

Base path:

```text id="6t2v2g"
/api
```

Example:

```text id="j3c9x8"
GET /api/plants/{plantId}
```

The API should use JSON for normal request and response bodies.

---

# 3. Versioning

The API should be designed so that versioning can be introduced without unnecessarily breaking existing clients.

The initial implementation may use:

```text id="4v6n8a"
/api/...
```

If a breaking public API change becomes necessary, versioning may be introduced, for example:

```text id="q4x1vb"
/api/v1/...
```

Do not introduce versioning complexity before it is needed.

---

# 4. HTTP Methods

Use HTTP methods according to their intended semantics.

```text id="6j2q0v"
GET     Retrieve
POST    Create / initiate an operation
PUT     Replace a resource where appropriate
PATCH   Partially update a resource
DELETE  Remove a resource
```

Examples:

```text id="6k1t9n"
GET    /api/plants/123
POST   /api/favorites
DELETE /api/favorites/123
PATCH  /api/favorites/order
POST   /api/identification
```

---

# 5. Content Type

Requests and responses should normally use:

```text
application/json
```

Image upload endpoints may use:

```text
multipart/form-data
```

The identification upload endpoint is the primary expected multipart endpoint.

---

# 6. Authentication

The API distinguishes between:

## Anonymous Requests

Can access public discovery functionality.

Examples:

* Explore
* Search
* Plant Detail
* Category browsing
* Plant groups
* Identification, subject to rate limits

## Authenticated Requests

Required for persistent personal features.

Examples:

* Favorites
* Persistent user history
* Account-specific functionality

The exact authentication provider and token mechanism will be finalized during implementation.

---

# 7. Authorization Principle

The backend is the security boundary.

The frontend must not be trusted to enforce authorization.

For example:

```text id="7u3g4m"
DELETE /api/favorites/{favoriteId}
```

The backend must verify that the authenticated user owns the Favorite.

A user must never be able to modify another user's Favorites by changing an ID in the request.

---

# 8. Common Headers

Typical requests may include:

```http
Accept: application/json
Content-Type: application/json
Authorization: Bearer <token>
```

The `Authorization` header is required only for endpoints requiring authentication.

---

# 9. Common Response Structure

Successful responses should be consistent and application-oriented.

For simple resources:

```json
{
  "data": {
    "id": "123",
    "name": "Rose"
  }
}
```

For collections:

```json
{
  "data": [],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "hasNext": true
  }
}
```

The exact envelope strategy may be adjusted during implementation, but consistency should be maintained across endpoints.

---

# 10. Pagination

Collection endpoints should support pagination.

Initial conceptual parameters:

```text id="m5x1wr"
?page=1&pageSize=20
```

Example:

```text id="f9w7pk"
GET /api/plants?page=1&pageSize=20
```

The API should enforce reasonable maximum page sizes.

Clients should not be able to request unlimited records.

---

# 11. Cursor Pagination

Cursor-based pagination may be introduced later if the scale or recommendation feed requires it.

The MVP may use page-based pagination if it is sufficient.

The architecture should avoid making the frontend depend unnecessarily on one pagination implementation.

---

# 12. Error Response

Errors should use a consistent structure.

Conceptual response:

```json
{
  "error": {
    "code": "PLANT_NOT_FOUND",
    "message": "The requested plant could not be found."
  }
}
```

Validation errors may include field-specific details:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields are invalid.",
    "details": {
      "query": "Search query is required."
    }
  }
}
```

Unexpected server errors return `500 Internal Server Error` with a fixed body:

```json
{
  "error": {
    "code": "INTERNAL_SERVER_ERROR",
    "message": "Something went wrong. Please try again."
  }
}
```

The same body is returned in every environment. Exception details are written only to server-side logs.

The API must not expose:

* Stack traces
* Internal implementation details
* Secrets
* Database credentials
* Raw provider errors

---

# 13. HTTP Status Codes

Use HTTP status codes consistently.

Common codes:

```text id="9l8z1m"
200 OK
201 Created
204 No Content
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
413 Payload Too Large
422 Unprocessable Entity
429 Too Many Requests
500 Internal Server Error
502 Bad Gateway
503 Service Unavailable
```

Not every endpoint needs every status code.

---

# 14. Plant Endpoints

## 14.1 Get Plant

```http
GET /api/plants/{plantId}
```

### Authentication

Not required.

### Purpose

Retrieve the Plant Detail data for a specific plant.

### Response — `200 OK` (implemented)

```json
{
  "data": {
    "id": "d5e00000-0000-4000-8000-000000000001",
    "name": "Rosa 'Peace'",
    "plantGroup": "Roses",
    "scientificName": "Rosa × hybrida",
    "description": "Development sample data — not verified.",
    "heroImage": null,
    "quickCare": {
      "light": {
        "value": "Full Sun",
        "description": "Requires several hours of direct sunlight."
      },
      "water": null,
      "temperature": {
        "minC": 15.0,
        "maxC": 25.0,
        "description": null
      },
      "soil": {
        "value": "Well-drained",
        "description": null
      }
    }
  }
}
```

Field rules:

* `id` — the plant's UUID.
* `name` — the most understandable plant/cultivar name (UX §28).
* `plantGroup` — the plant group name.
* `heroImage` — always `null` until the hero-image approach is decided. When set, it will be `{ "url", "altText" }`.
* `quickCare` — `null` unless the plant's care information is **Verified**. Unverified care is never returned. Inside it, any card without a value is `null`.
* `temperature` — numeric degrees Celsius (`minC`, `maxC`, either may be `null`); clients format the display text (e.g. "15–25°C").
* `blooming`, `health`, and `about` are not returned yet; they will be added when their data is stored.

Only `Published` plants are returned. Unknown IDs and plants in any other status return the same response:

`404 Not Found`

```json
{
  "error": {
    "code": "PLANT_NOT_FOUND",
    "message": "The requested plant could not be found."
  }
}
```

A `plantId` that is not a valid UUID returns `400 Bad Request`:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields are invalid.",
    "details": {
      "plantId": "Plant ID must be a valid UUID."
    }
  }
}
```

---

# 15. Plant Detail Response Principles

The endpoint should return data required by the Plant Detail UX.

The API should not return unnecessary raw database fields.

The response should be designed around the application experience.

---

# 16. Plant Images

Plant Detail may return multiple image references.

Example:

```json
{
  "images": [
    {
      "id": "img-1",
      "url": "https://example.com/large.webp",
      "thumbnailUrl": "https://example.com/thumb.webp",
      "altText": "Pink rose flower"
    }
  ]
}
```

The frontend should choose the appropriate image size based on the display context.

---

# 17. Category Endpoints

## Get Categories

```http
GET /api/categories
```

### Authentication

Not required.

### Response

```json
{
  "data": [
    {
      "id": "flowers",
      "name": "Flowers",
      "imageUrl": "https://example.com/flowers.webp"
    },
    {
      "id": "trees",
      "name": "Trees",
      "imageUrl": "https://example.com/trees.webp"
    }
  ]
}
```

---

# 18. Category Detail

```http
GET /api/categories/{categoryId}
```

Returns plant groups belonging to the category.

Example:

```json
{
  "data": {
    "id": "flowers",
    "name": "Flowers",
    "plantGroups": [
      {
        "id": "roses",
        "name": "Roses",
        "imageUrl": "https://example.com/roses.webp"
      },
      {
        "id": "tulips",
        "name": "Tulips",
        "imageUrl": "https://example.com/tulips.webp"
      }
    ]
  }
}
```

Category results must remain category-pure.

---

# 19. Plant Group Endpoints

## Get Plant Group

```http
GET /api/plant-groups/{plantGroupId}
```

Returns plants/cultivars belonging to the group.

Example:

```json
{
  "data": {
    "id": "roses",
    "name": "Roses",
    "plants": []
  },
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "hasNext": true
  }
}
```

---

# 20. Explore Endpoint

```http
GET /api/explore
```

### Authentication

Optional.

### Purpose

Return the user's discovery feed.

The backend should determine the recommendation strategy based on available behavioral signals.

---

# 21. Explore Query Parameters

Potential parameters:

```text
page
pageSize
categoryId
```

Example:

```http
GET /api/explore?page=1&pageSize=20
```

The API should not expose internal recommendation weights to clients.

---

# 22. Explore Recommendation Behavior

For new or anonymous users:

* Maintain category diversity.
* Favor curated discovery.
* Avoid excessive popularity bias.
* Avoid pure randomness.

For users with sufficient behavioral data:

```text id="5t3j1w"
60% strongly related
20% adjacent
20% novel
```

This is a recommendation guideline rather than a strict API response requirement.

---

# 23. Explore Deduplication

The API must ensure that the same plant/cultivar does not appear multiple times in the same feed.

Deduplication occurs at the plant entity level, not image level.

---

# 24. Search Endpoint

```http
GET /api/search?q={query}
```

Example:

```http
GET /api/search?q=peace%20rose&page=1&pageSize=20
```

### Authentication

Not required.

### Purpose

Search the application's plant database.

---

# 25. Search Response

Example:

```json
{
  "data": [
    {
      "id": "123",
      "name": "Rosa 'Peace'",
      "plantGroup": "Hybrid Tea Rose",
      "scientificName": "Rosa × hybrida",
      "imageUrl": "https://example.com/peace.webp"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "hasNext": false
  }
}
```

---

# 26. Search Suggestions

```http
GET /api/search/suggestions?q={query}
```

Suggestions should be database-backed.

Potential suggestion types:

* Common name
* Scientific name
* Cultivar
* Plant group
* Taxonomic term

Example:

```json
{
  "data": [
    {
      "text": "Rosa 'Peace'",
      "type": "cultivar",
      "plantId": "123"
    },
    {
      "text": "Peace Rose",
      "type": "commonName",
      "plantId": "123"
    }
  ]
}
```

---

# 27. Search External Ingestion

If a search does not find a plant locally, the backend may attempt external structured-data lookup.

Conceptually:

```text id="4m1h6r"
Search
  ↓
Own DB
  ↓
No Result
  ↓
External Providers
  ↓
Normalize
  ↓
Validate
  ↓
Persist
  ↓
Return
```

The frontend should not call iNaturalist or GBIF directly.

---

# 28. Recent Searches

## Get Recent Searches

```http
GET /api/history/searches
```

Authentication may be optional depending on the anonymous-history implementation.

## Delete Search

```http
DELETE /api/history/searches/{searchId}
```

Deleting search history does not delete plant data.

---

# 29. View History

## Record Plant View

```http
POST /api/history/views
```

Example request:

```json
{
  "plantId": "123"
}
```

The backend may choose to record views asynchronously if appropriate.

---

# 30. View History Retrieval

```http
GET /api/history/views
```

Authentication is optional depending on the identity model.

The endpoint should not expose another user's history.

---

# 31. Favorites

Favorites require authentication.

## Get Favorites

```http
GET /api/favorites
```

Example:

```json
{
  "data": [
    {
      "favoriteId": "f-123",
      "plant": {
        "id": "123",
        "name": "Rosa 'Peace'",
        "imageUrl": "https://example.com/peace.webp"
      },
      "order": 1
    }
  ]
}
```

---

# 32. Add Favorite

```http
POST /api/favorites
```

Request:

```json
{
  "plantId": "123"
}
```

### Possible Responses

```text
201 Created
```

If the plant is already favorited:

```text
409 Conflict
```

or the API may implement idempotent behavior.

The final behavior should be chosen during implementation and documented consistently.

---

# 33. Remove Favorite

```http
DELETE /api/favorites/{favoriteId}
```

Authentication required.

The backend must verify ownership.

Successful response:

```text
204 No Content
```

---

# 34. Reorder Favorites

```http
PATCH /api/favorites/order
```

Example:

```json
{
  "items": [
    {
      "favoriteId": "f-1",
      "order": 1
    },
    {
      "favoriteId": "f-2",
      "order": 2
    },
    {
      "favoriteId": "f-3",
      "order": 3
    }
  ]
}
```

The operation should maintain a valid ordering state.

---

# 35. Authentication Flow

The exact authentication provider is deferred.

Conceptually:

```text id="fj7w6s"
User
 ↓
Login
 ↓
Authentication Provider
 ↓
Token / Session
 ↓
Angular or MAUI
 ↓
ASP.NET Core API
 ↓
Authenticated User
```

The API must validate authentication credentials/tokens server-side.

---

# 36. Plant Identification

## Submit Identification

```http
POST /api/identification
```

### Content Type

```text
multipart/form-data
```

### Request

```text
image=<uploaded image>
```

Optional metadata may be added later.

---

# 37. Identification Response

Example:

```json
{
  "data": {
    "requestId": "id-123",
    "status": "completed",
    "candidates": [
      {
        "rank": 1,
        "plant": {
          "id": "123",
          "name": "Rosa 'Peace'",
          "plantGroup": "Hybrid Tea Rose",
          "scientificName": "Rosa × hybrida",
          "imageUrl": "https://example.com/peace.webp"
        }
      },
      {
        "rank": 2,
        "plant": {
          "id": "456",
          "name": "Rosa 'Mr. Lincoln'",
          "plantGroup": "Hybrid Tea Rose",
          "scientificName": "Rosa × hybrida",
          "imageUrl": "https://example.com/mr-lincoln.webp"
        }
      },
      {
        "rank": 3,
        "plant": {
          "id": "789",
          "name": "Rosa 'Iceberg'",
          "plantGroup": "Floribunda Rose",
          "scientificName": "Rosa × hybrida",
          "imageUrl": "https://example.com/iceberg.webp"
        }
      }
    ]
  }
}
```

The API may internally have additional candidates, but the initial UI displays the top three.

---

# 38. Identification Failure

Example:

```json
{
  "error": {
    "code": "IDENTIFICATION_FAILED",
    "message": "The plant could not be identified."
  }
}
```

The API should not claim that the plant does not exist solely because identification failed.

---

# 39. Identification Status

If identification is implemented synchronously:

```text
completed
failed
```

If asynchronous processing becomes necessary:

```text
pending
processing
completed
failed
```

The API may later expose:

```http
GET /api/identification/{requestId}
```

This should only be added if asynchronous processing becomes necessary.

---

# 40. Identification Rate Limiting

Identification is an expensive operation.

The endpoint should be protected with rate limiting.

Potential controls include:

* Requests per anonymous client
* Requests per authenticated user
* Image size limits
* Request frequency limits
* Provider-level quotas

Exact thresholds are deferred.

---

# 41. Image Validation

The identification endpoint should validate uploaded images.

Potential validation includes:

* File type
* File size
* Image dimensions
* Decodability
* Malformed files

The backend should not blindly pass arbitrary uploaded files to the AI provider.

---

# 42. Identification Privacy

The API should not retain user-uploaded images indefinitely without a clear purpose.

If images are retained for debugging or evaluation, the retention policy should be documented separately.

---

# 43. Similar Plants

## Get Similar Plants

```http
GET /api/plants/{plantId}/similar
```

Example:

```json
{
  "data": [
    {
      "id": "456",
      "name": "Rosa 'Mr. Lincoln'",
      "imageUrl": "https://example.com/mr-lincoln.webp"
    },
    {
      "id": "789",
      "name": "Rosa 'Iceberg'",
      "imageUrl": "https://example.com/iceberg.webp"
    }
  ]
}
```

The API may return more results than the initial UI displays.

---

# 44. More / Contextual Discovery

"More" from Identification Results should be contextual to the identification request.

It should not simply redirect to the global Explore feed.

Conceptually:

```http
GET /api/identification/{requestId}/more
```

The exact endpoint may be combined with the identification response if appropriate.

---

# 45. Where to Buy

## Get Nearby Retailers

Conceptually:

```http
GET /api/plants/{plantId}/retailers
```

Potential parameters:

```text
latitude
longitude
radius
sort
```

Example:

```http
GET /api/plants/123/retailers?latitude=49.28&longitude=-123.12&radius=25&sort=distance
```

The exact geographic query implementation is deferred.

---

# 46. Retailer Sort Options

Potential values:

```text
distance
rating
relevance
```

Distance should be the default for nearby searches.

---

# 47. Retailer Response

Example:

```json
{
  "data": [
    {
      "id": "retailer-123",
      "name": "Example Garden Centre",
      "type": "gardenCentre",
      "location": {
        "address": "Example address",
        "city": "Vancouver",
        "region": "BC",
        "distanceKm": 3.2
      },
      "rating": 4.6,
      "website": "https://example.com",
      "availability": {
        "status": "unknown"
      }
    }
  ]
}
```

The API must not represent unknown availability as confirmed stock.

---

# 48. Retailer Availability

Possible availability states:

```text id="3q6g0v"
unknown
available
unavailable
```

Only use `available` when supported by reliable data.

Otherwise:

```text
unknown
```

is preferable.

---

# 49. Retailer Price

If pricing is available:

```json
{
  "price": {
    "amount": 39.99,
    "currency": "CAD",
    "observedAt": "2026-10-01"
  }
}
```

The API should make clear that observed pricing may become outdated.

---

# 50. Location Privacy

The API should receive only the location information necessary to perform the requested retailer search.

Location should not be stored as persistent user profile data unless there is a clear product requirement.

---

# 51. Anonymous Location

Users may deny location permission.

The frontend can provide a manually selected location.

The backend should not assume that location permission is always available.

---

# 52. External Provider Isolation

The API layer should not directly contain iNaturalist, GBIF, or AI provider-specific implementation details.

Instead:

```text id="x4c8f1"
API
 ↓
Application Service
 ↓
Provider Interface
 ↓
Provider Implementation
```

This keeps API contracts stable when external providers change.

---

# 53. External Provider Errors

External provider errors should be translated into application-level behavior.

For example:

```text id="6b9e2f"
Provider Timeout
      ↓
Backend handles timeout
      ↓
Application-level fallback/error
      ↓
Frontend receives stable API response
```

The frontend should not need to understand provider-specific error codes.

---

# 54. Idempotency

Operations that may be retried should be designed with idempotency where appropriate.

Potential examples:

* Favorite creation
* Plant ingestion
* Identification requests where duplicate submission is undesirable

The exact use of idempotency keys is deferred until implementation.

---

# 55. Caching

The backend may cache expensive or frequently requested responses.

Potential cache targets:

* Plant Detail
* Search results
* Categories
* Plant groups
* Similar plants
* External provider lookups
* Identification results

Cache behavior must not cause stale user-specific information to leak between users.

---

# 56. Cache Keys

User-specific data must include appropriate user identity in the cache key.

For example:

```text id="q3z0y4"
public:plant:123
user:456:favorites
```

A public plant response must never accidentally serve one user's private data.

---

# 57. API Security

The API must:

* Validate all input
* Authenticate protected endpoints
* Authorize resource ownership
* Limit upload sizes
* Rate-limit expensive operations
* Avoid leaking internal errors
* Protect secrets
* Use HTTPS in deployed environments

---

# 58. CORS

The API should explicitly configure allowed frontend origins.

Development and production origins should be managed separately.

Do not use unrestricted CORS in production unless there is a justified reason.

---

# 59. API Logging

The backend should log useful operational information such as:

* Request method
* Endpoint
* Response status
* Duration
* Correlation/request ID
* External provider failures

Sensitive values should not be logged.

Examples of values that should not be logged unnecessarily:

* Authentication tokens
* API keys
* Passwords
* Private user content

---

# 60. Correlation IDs

The API should support a request/correlation identifier for troubleshooting.

Conceptually:

```text id="8g4m1p"
Client Request
      ↓
Request ID
      ↓
API Logs
      ↓
External Provider Logs
```

This becomes especially useful when debugging AI or external API failures.

---

# 61. API Validation

Request validation should occur at the API boundary.

Examples:

```text id="v8z4pq"
Search query:
- Required where appropriate
- Maximum length
- Normalized

Plant ID:
- Valid format

Image:
- Valid file
- Valid size
- Supported format
```

Validation should prevent malformed input from reaching deeper layers.

---

# 62. API DTOs

The API should use dedicated DTOs.

Example:

```text id="5g6m9r"
PlantEntity
    ↓
PlantDetailDto
```

The API should not directly serialize EF Core entities.

---

# 63. DTO Design Principles

DTOs should:

* Contain only required client data
* Avoid leaking internal fields
* Avoid circular references
* Remain stable as database structure evolves
* Be optimized for the API use case

Different screens may use different DTOs.

For example:

```text
PlantCardDto
PlantDetailDto
PlantSearchResultDto
IdentificationCandidateDto
```

---

# 64. API Contract and Frontend

The Angular and MAUI applications should depend on the documented API contract rather than assumptions about the database.

If the database changes but the API contract remains compatible, frontend code should not need to change.

---

# 65. API Testing

Each API feature should have appropriate automated tests.

Examples:

### Plant

* Get existing plant
* Get nonexistent plant

### Search

* Valid search
* Empty query
* No results
* Pagination

### Favorites

* Add favorite
* Remove favorite
* Unauthorized access
* Duplicate favorite
* Reorder

### Identification

* Valid image
* Invalid file
* Provider failure
* Rate limiting
* Validated candidate filtering

### Retailers

* Valid location
* Missing location
* Invalid coordinates
* No retailer results

---

# 66. API Documentation

The backend should eventually expose interactive API documentation using an appropriate OpenAPI/Swagger implementation.

The generated documentation should match the actual API contract.

Documentation should include:

* Endpoint
* Method
* Authentication requirement
* Request schema
* Response schema
* Error responses
* Example payloads

---

# 67. API Implementation Order

Recommended implementation order:

```text id="3z8j0c"
1. Health / API foundation
2. Categories
3. Plant groups
4. Plant Detail
5. Search
6. Plant ingestion
7. Authentication
8. Favorites
9. History
10. Explore
11. Identification
12. Similar Plants
13. Where to Buy
14. Recommendation improvements
15. Performance / caching
```

This order may change based on implementation dependencies.

---

# 68. Health Check

A basic health endpoint should exist for deployment and monitoring.

Example:

```http
GET /health
```

Authentication: not required.

The check verifies that the application is running and that the PostgreSQL database is reachable.

Healthy response — `200 OK`:

```json
{
  "status": "healthy"
}
```

Unhealthy response (for example, the database is unreachable) — `503 Service Unavailable`:

```json
{
  "status": "unhealthy"
}
```

The body contains only `status`. Check names, timings, exception messages, and connection details are not returned; failure details are logged server-side.

The health endpoint should not expose sensitive infrastructure information.

More detailed readiness checks may be introduced separately.

---

# 69. API Non-Goals

The initial API should not:

* Expose raw external provider responses
* Allow arbitrary database queries
* Return unlimited collections
* Expose AI internal prompts
* Expose provider secrets
* Guarantee retailer inventory without reliable data
* Generate authoritative care information through AI
* Implement complex GraphQL infrastructure without a demonstrated need

---

# 70. Future API Extensions

Potential future capabilities:

* Advanced plant comparison
* Image similarity search
* Recommendation explanation
* Advanced plant collections
* Detailed growing guides
* User-generated content
* Notifications
* Advanced retailer inventory
* Background ingestion jobs
* Search indexing infrastructure

These should be added only when justified by product requirements.

---

# 71. API Quality Bar

Before an endpoint is considered complete, verify:

* Is the endpoint necessary for a product requirement?
* Is authentication correctly defined?
* Is authorization enforced server-side?
* Are request inputs validated?
* Are responses based on DTOs?
* Are errors predictable?
* Are pagination limits enforced?
* Are external provider details hidden?
* Are expensive operations rate-limited?
* Is sensitive data protected?
* Is the endpoint tested?
* Is the contract documented?

---

# 72. API Guiding Principle

> **The API is a stable application contract between clients and the backend, not a mirror of the database or external providers.**

The API should make the rest of the system easier to change, test, secure, and understand.
