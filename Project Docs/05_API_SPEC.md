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

Implemented (D-070): ASP.NET Core Identity with a session cookie.

* `pe_session` — the session cookie (HttpOnly, Secure, SameSite=Lax, 14 days, sliding). Unauthenticated calls to protected endpoints get `401 UNAUTHENTICATED`, never a redirect.
* `XSRF-TOKEN` — a readable cookie with the CSRF token; the client copies it into the `X-XSRF-TOKEN` header on requests that change data. It is issued by `GET /api/me` and again after signing in or out (the token is bound to the user). Missing or wrong tokens get `403 CSRF_TOKEN_INVALID`.
* Account endpoints (`/api/auth/*`) are limited to 10 requests per minute per client (`429 RATE_LIMITED`), with a 4 KB body limit.

| Endpoint | Body | Success | Errors |
|---|---|---|---|
| `GET /api/me` | — | `200 {data: user}` | `401 UNAUTHENTICATED` when signed out |
| `POST /api/auth/register` | `{email, password, displayName}` | `201 {data: user}`, signed in; a confirmation email is sent | `400 VALIDATION_ERROR` (`email`, `password` 8–128, `displayName` 1–50); `409 EMAIL_TAKEN` |
| `POST /api/auth/login` | `{email, password}` | `200 {data: user}` | `401 INVALID_CREDENTIALS` (same for unknown email and wrong password); `423 ACCOUNT_LOCKED` after 5 failures, for 15 minutes |
| `POST /api/auth/logout` | `{}` | `204` | — |
| `POST /api/auth/forgot-password` | `{email}` | `202`, the same whether or not the address has an account | `400 VALIDATION_ERROR` |
| `POST /api/auth/reset-password` | `{email, token, password}` | `204` (also ends a lockout) | `400 INVALID_RESET_LINK`; `400 VALIDATION_ERROR` |
| `POST /api/auth/resend-confirmation` | `{}` (signed in) | `202` | `401 UNAUTHENTICATED` |
| `GET /api/auth/confirm-email?userId=&token=` | — | redirect to `/email-confirmed?status=ok` or `status=invalid` | — |
| `GET /api/auth/providers` | — | `200 {data: [{id, name}]}` — only providers with configured keys (`google`, `github`) | — |
| `GET /api/auth/external/{provider}?returnUrl=` | — (full-page navigation) | redirect to the provider; it returns to `/signin-{provider}`, then `/api/auth/external/callback` | `404 PROVIDER_NOT_FOUND` |
| `GET /api/auth/external/callback?returnUrl=` | — | signed in; redirect to `returnUrl` (paths inside the app only, else `/explore`) | redirect to `/login?error=email-in-use`, `email-required`, `locked` or `external-failed` |

A user is `{ "id", "displayName", "email", "emailConfirmed" }`. Email links (confirmation, reset) are built from `Accounts:PublicBaseUrl` and expire after 3 hours. Registration necessarily reveals that an address already has an account (`409`); sign-in and password reset do not.

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

Implemented limits (currently used by `GET /api/plant-groups/{plantGroupId}`):

* `page` — whole number from 1 to 10000; default 1.
* `pageSize` — whole number from 1 to 50; default 20.
* Any other value returns `400 VALIDATION_ERROR` with a `details` entry for each invalid parameter:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields are invalid.",
    "details": {
      "pageSize": "Page size must be a whole number between 1 and 50."
    }
  }
}
```

* `pagination.hasNext` is `true` when at least one more item exists after this page.

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
      "q": "Search text is required."
    }
  }
}
```

Rate-limited requests return `429 Too Many Requests` with a `Retry-After` header (seconds) and:

```json
{
  "error": {
    "code": "RATE_LIMITED",
    "message": "Too many requests. Please wait a moment and try again."
  }
}
```

Requests that exceed their time limit return `503 Service Unavailable` with:

```json
{
  "error": {
    "code": "REQUEST_TIMEOUT",
    "message": "The request took too long. Please try again."
  }
}
```

When an external provider cannot answer (§27, §53), the endpoint returns `503 Service Unavailable` with:

```json
{
  "error": {
    "code": "EXTERNAL_LOOKUP_UNAVAILABLE",
    "message": "External plant lookup is unavailable right now. Please try again later."
  }
}
```

A request body larger than an endpoint accepts returns `413 Payload Too Large` with:

```json
{
  "error": {
    "code": "PAYLOAD_TOO_LARGE",
    "message": "The request body is too large."
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
    "plantGroupId": "roses",
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
    },
    "attributions": []
  }
}
```

Field rules:

* `id` — the plant's UUID.
* `name` — the most understandable plant/cultivar name (UX §28).
* `plantGroup` — the plant group name.
* `plantGroupId` — the plant group's slug, usable with `GET /api/plant-groups/{plantGroupId}` (§19). `plantGroup` and `plantGroupId` are both `null` when the plant has no group.
* `heroImage` — the plant's photo, or its botanical illustration when it has no photo; `null` when it has no image. An illustration stored next to a photo is not returned. Shape:
  ```json
  {
    "url": "/media/plants/…/…-large.webp",
    "altText": "Dog rose (Rosa canina)",
    "kind": "photo",
    "credit": {
      "author": "Douglas Goldman",
      "license": "CC BY-SA",
      "licenseUrl": null,
      "source": "iNaturalist",
      "sourceUrl": null
    }
  }
  ```
  * `url` is the 1600-pixel WebP rendition: an absolute Cloudflare R2 URL when image storage is R2 (Development since Phase 11d-3), or a relative `/media/…` URL served by this API with local storage.
  * `kind` is `photo` or `illustration`.
  * `credit` is what the image's licence requires to be shown. `licenseUrl` is set only when the licence version is known; `sourceUrl` is the image's page at the source, when known. `credit` is `null` only for sources without reviewed credit text.
* `quickCare` — `null` unless the plant's care information is **Verified**. Unverified care is never returned. Inside it, any card without a value is `null`.
* `temperature` — numeric degrees Celsius (`minC`, `maxC`, either may be `null`); clients format the display text (e.g. "15–25°C").
* `attributions` — credits for external sources, shown on Plant Detail. Each item is `{ "type", "text" }`, for example `{ "type": "taxonomy", "text": "Catalogue of Life, via GBIF" }` for a plant imported through §27. The array is empty for curated plants.
  * Sources from providers without reviewed attribution text are not listed.
  * The exact Catalogue of Life citation must be verified before public release.
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

Implemented (Phase 11d-2): Plant Detail returns a single `heroImage` (§14) rather than an `images` array. Card `imageUrl` values (§19, §25, §27.1) are the 800-pixel rendition of the photo, else of the illustration, or `null`.

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
      "imageUrl": null
    },
    {
      "id": "fruits",
      "name": "Fruits",
      "imageUrl": null
    }
  ]
}
```

Implemented rules:

* `id` is the category slug (lowercase letters, digits, and hyphens).
* Categories are ordered by name. A product-defined display order would require a schema change and is deferred.
* `imageUrl` is always `null` until category images exist.

---

# 18. Category Detail

```http
GET /api/categories/{categoryId}
```

`categoryId` is the category slug. Authentication: not required.

Returns plant groups belonging to the category.

Response — `200 OK`:

```json
{
  "data": {
    "id": "flowers",
    "name": "Flowers",
    "plantGroups": [
      {
        "id": "roses",
        "name": "Roses",
        "imageUrl": null
      },
      {
        "id": "tulips",
        "name": "Tulips",
        "imageUrl": null
      }
    ]
  }
}
```

Category results must remain category-pure.

Implemented rules:

* Only the category's own plant groups are returned, ordered by name.
* Plant groups are listed even if they have no Published plants yet.
* A plant group's `imageUrl` is the card image of one of its Published plants (photos before illustrations, rejected images never). The plant looks randomly chosen but stays the same for a whole day (UTC) and changes the next day; `null` when none of the group's plants has an image. The category's own `imageUrl` (§17) is still always `null`.

Development only (mapped in the `Development` and `Testing` environments, no authentication): `POST /api/dev/plants/{plantId}/hero-image/reject` rejects the image the plant shows as its hero and queues the plant for a new image; `202 Accepted`, or `404 PLANT_IMAGE_NOT_FOUND` when it has no usable image. It is a stand-in until an authorized review tool exists.

Unknown category — `404 Not Found`:

```json
{
  "error": {
    "code": "CATEGORY_NOT_FOUND",
    "message": "The requested category could not be found."
  }
}
```

---

# 19. Plant Group Endpoints

## Get Plant Group

```http
GET /api/plant-groups/{plantGroupId}
```

`plantGroupId` is the plant-group slug. Query: `page`, `pageSize` (see §10). Authentication: not required.

Returns plants/cultivars belonging to the group.

Response — `200 OK`:

```json
{
  "data": {
    "id": "roses",
    "name": "Roses",
    "category": {
      "id": "flowers",
      "name": "Flowers"
    },
    "plants": [
      {
        "id": "d5e00000-0000-4000-8000-000000000002",
        "name": "Rosa 'Iceberg'",
        "imageUrl": null
      }
    ]
  },
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "hasNext": false
  }
}
```

Implemented rules:

* Only `Published` plants are returned, ordered by name and then ID so pages are stable.
* `category` identifies the group's category (slug and name) for page headers and back navigation.
* Plant `id` values are UUIDs (use them with `GET /api/plants/{plantId}`); `imageUrl` is the card image (§16), or `null` when the plant has no image.
* A group with no Published plants returns `200` with `"plants": []`.

Unknown plant group — `404 Not Found`:

```json
{
  "error": {
    "code": "PLANT_GROUP_NOT_FOUND",
    "message": "The requested plant group could not be found."
  }
}
```

Invalid `page` / `pageSize` — `400 VALIDATION_ERROR` (see §10).

---

# 20. Explore Endpoint

```http
GET /api/explore
```

Implemented (Phase 7, feed for visitors without behavioral data):

```json
{
  "data": [
    {
      "id": "d5e00000-0000-4000-8000-000000000001",
      "name": "Dog rose",
      "plantGroup": "Roses",
      "imageUrl": "/media/plants/…/…-medium.webp"
    }
  ],
  "pagination": { "page": 1, "pageSize": 20, "hasNext": true },
  "feedAt": "2026-10-25T01:00:00.0000000Z"
}
```

* `page` / `pageSize` use the limits in §10 (`pageSize` up to 50); other values return `400 VALIDATION_ERROR`.
* Only `Published` plants with a plant group appear, each exactly once; cultivars are separate plants.
* `imageUrl` is the card image (§16), or `null`.
* The order is the same on every request (no randomness, no popularity), so pages never repeat or skip a plant; see §22.
* `categoryId` (§21) is not implemented.
* Personalized for signed-in users (Phase 7b, 2026-10-25), see §22. Visitors who are not signed in get the default order.
* `feedAt` (optional query parameter, Phase 7b): every response returns the moment the feed was composed (UTC, ISO 8601). The client sends it back with later pages; activity after it (new views, saves, searches) is then ignored, so the pages of one feed keep one order. A removed search or favorite stops counting at once. A `feedAt` in the future counts as now; one that is not a date and time returns `400 VALIDATION_ERROR` with `details.feedAt`.

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

Implemented for new or anonymous users (Phase 7): categories take turns (every category's first plant, then every category's second, …, categories in name order); inside a category the plant groups take turns the same way; inside a group, plants with an image come first, then by name.

Implemented for signed-in users (Phase 7b, rule-based, D-023):

* **Signals:** views (weight 1), saved plants (3) and searches (2, shared among the plant groups whose plants or group name contain the whole search text). Each counts half as much every 14 days.
* **When:** total weight of at least 3 (for example three plant views, or one saved plant). Below that, the default order above is used.
* **Mix:** every 10 cards follow the pattern strong, strong, adjacent, strong, strong, novel, strong, strong, adjacent, novel: 6 from plant groups with interest, 2 from other groups of the same categories, 2 from categories without interest. When one kind runs out, the next takes its turn.
* **Inside each kind:** groups take turns, the more interesting first in each round; inside a group, plants the user has not viewed or saved come first, then plants with an image, then by name. Each plant appears once.
* Weights are not exposed in the response.

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

### Rules (implemented)

* Only `Published` plants are returned.
* A plant matches when **every word** of `q` appears in at least one of: its display name, scientific name, cultivar name, any alternative name (common names in any language, synonyms), or its plant group's name. A word may appear inside a longer word ("rose" matches "Roses").
* Matching ignores letter case, accents ("cafe" matches "Café"), and punctuation and spacing ("mr lincoln" matches "Mr. Lincoln"). Full-width and other compatibility characters are normalized first (Unicode NFKC). Punctuation in `q`, including `%`, `_` and `\`, only separates words; it is never a wildcard.
* Ordering (D-016, relevance first):
  1. a display, cultivar, or scientific name, or a common name, equals the whole search;
  2. a display, cultivar, or scientific name starts with the search;
  3. every word appears in the display, cultivar, or scientific name;
  4. all other matches (found through alternative names or the plant group).

  Ties are ordered by name, then ID, so pages never overlap.
* No matches return `200 OK` with `"data": []`.
* Search itself never calls external services. When it finds nothing, the client may offer the separate Catalogue of Life lookup (§27).
* Not yet: recording recent searches on the server (§28).

### Validation and limits (implemented)

| Parameter | Rule | `details` message |
|---|---|---|
| `q` | required, not only spaces | `Search text is required.` |
| `q` | at most 100 characters after trimming and normalization | `Search text must be at most 100 characters.` |
| `q` | no control characters (for example NUL, line breaks) | `Search text must not contain control characters.` |
| `q` | valid Unicode text | `Search text contains invalid characters.` |
| `q` | at least one letter or digit | `Search text must contain a letter or digit.` |
| `q` | at most 10 words | `Search text must have at most 10 words.` |
| `page` | whole number from 1 to **50** (search stops at page 50) | `Page must be a whole number between 1 and 50.` |
| `pageSize` | as §10 (1–50, default 20) | as §10 |

All invalid parameters are reported together in one `400 VALIDATION_ERROR`.

Abuse protection:

* **Rate limit:** 30 requests per minute per client IP address (IPv6 clients are grouped by their /64 network). Excess requests return `429 RATE_LIMITED` with `Retry-After` (§12). Forwarded headers (`X-Forwarded-For`) are not trusted until a reverse proxy with a known-proxy list is configured.
* **Timeout:** a search taking longer than 5 seconds is cancelled (including its database query) and returns `503 REQUEST_TIMEOUT` (§12).
* The search text is never written to server logs.

---

# 25. Search Response

Example (implemented):

```json
{
  "data": [
    {
      "id": "d5e00000-0000-4000-8000-000000000001",
      "name": "Rosa 'Peace'",
      "plantGroup": "Roses",
      "scientificName": "Rosa × hybrida",
      "imageUrl": null
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "hasNext": false
  }
}
```

* `id` — the plant's UUID (use with §14).
* `plantGroup` — the plant group name, or `null`.
* `imageUrl` — the card image (§16), or `null` when the plant has no image.

---

# 26. Search Suggestions

```http
GET /api/search/suggestions?q={query}
```

Suggestions should be database-backed.

Implemented suggestion types (`type`):

* `plant` — a plant's display name
* `cultivar` — a cultivar name
* `scientificName` — a scientific name
* `commonName` — a common name in any language
* `synonym` — a synonym
* `plantGroup` — a plant group name
* Taxonomic terms (genus, family) are not suggested yet.

Example (implemented):

```json
{
  "data": [
    {
      "text": "Rosa 'Peace'",
      "type": "plant"
    },
    {
      "text": "Roses",
      "type": "plantGroup"
    }
  ]
}
```

### Rules (implemented)

* Sources: names of `Published` plants, and names of plant groups that contain at least one `Published` plant.
* **Word-start matching:** a name matches when it, or one of its later words, starts with `q` — ignoring case, accents and punctuation as in §24 (`pea` and `rosa pe` suggest "Rosa 'Peace'"; `eace` does not). Search (§24) still finds text inside words.
* At most **8** suggestions. Names that start with `q` come first, then names where a later word starts with it; within each, shorter names first, then alphabetical.
* Names that are identical after folding appear once. When one text has several sources, the type order above decides the shown text and `type`.
* Suggestions carry no plant ID: selecting one runs a search for `text` (UX §19). A `plantId` was removed before any client used it; it can be added back without breaking clients if a direct "open this plant" action is ever needed.
* `q` is validated exactly as in §24 (same `400 VALIDATION_ERROR` messages). Valid text shorter than **2 characters** after folding returns `"data": []`.
* No external services are called.

Abuse protection:

* **Rate limit:** 120 requests per minute per client IP (IPv6 by /64), separate from the search limit. Excess requests return `429 RATE_LIMITED` with `Retry-After` (§12).
* **Timeout:** 2 seconds, then `503 REQUEST_TIMEOUT` (§12).
* The typed text is never written to server logs.

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

## 27.1 Catalogue of Life lookup (implemented)

```http
POST /api/search/external
Content-Type: application/json

{ "q": "Rosa canina" }
```

### Authentication

Not required.

### Purpose

The user asked to look up a name that `GET /api/search` did not find. The lookup is a separate request, sent only when the user clicks "Search Catalogue of Life" (UX §43), never automatically. If the name qualifies, the backend imports the plant (ARCHITECTURE §22) and reports what the user may see. It is a `POST` because it can create a plant.

### Response — `200 OK`

| `status` | When | Body |
|---|---|---|
| `found` | The plant is Published: newly imported into a mapped plant group, or already stored | `{"data":{"status":"found","plant":{id,name,plantGroup,scientificName,imageUrl}}}`, with `plant` shaped like a §25 search result |
| `pendingReview` | The plant exists but is not Published (imported as `NeedsReview`, or stored as Draft/Validated/NeedsReview) | `{"data":{"status":"pendingReview"}}` |
| `notFound` | No exact, accepted Plantae match; the match was rejected; the plant is Archived; or `q` is not name-shaped | `{"data":{"status":"notFound"}}` |

* No plant data is returned for plants that are not Published, and rejection reasons and provider details are never returned.
* **Name-shaped** means 2–4 words of letters, starting with a letter. A word may contain `.` or `-` (`var.`, `bursa-pastoris`) or be `×`. Other text returns `notFound` without an external request.
* The name is sent in botanical casing: genus capitalized, the rest lower-case (`rosa CANINA` → `Rosa canina`).
* A plant already stored (same identity) is answered from the database without an external request.
* A newly imported plant's `name` is its English common name when one stands out in the source, e.g. `Dog rose` for Rosa canina; otherwise it is the scientific name. Its common names in en, fr, de and ko are stored too, so `GET /api/search` finds them (ARCHITECTURE §22).
* Images are fetched in the background after a plant is created, so a newly imported plant's `imageUrl` is usually `null` in this response and appears a few seconds later (ARCHITECTURE §23).

### Errors

* `400 VALIDATION_ERROR`:
  * `q` is validated as in §24, with the same `details` messages.
  * A body that is not JSON returns `details.body` = `Request body must be JSON.`.
  * Malformed JSON returns `details.body` = `Request body must be valid JSON.`.
* `413 PAYLOAD_TOO_LARGE`: the body is larger than 1 KB.
* `429 RATE_LIMITED`: more than **5 lookups per minute** per client IP (IPv6 by /64), with `Retry-After`.
* `503 EXTERNAL_LOOKUP_UNAVAILABLE`: the provider is disabled, over its app-wide limit, timed out or failed (§12).
* `503 REQUEST_TIMEOUT`: the whole request took longer than 5 seconds.

### Protection

* GBIF is called with a 3-second timeout. For a new plant, common names are then fetched from ChecklistBank with a 1.5-second timeout; if that fails, the plant is imported without them.
* App-wide, at most **30 external calls per minute and 1,000 per day** (GBIF and ChecklistBank together), whoever asks.
* Names that cannot be imported (no match or a rejected match) are remembered for **24 hours**, so repeating them does not call GBIF again. Provider failures are never remembered.
* All of this is in memory, per API instance.
* The text is never written to server logs.
* The endpoint uses no cookies or credentials. CORS is not enabled, so other sites cannot send this JSON `POST` from a browser.

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

## Implemented (Phase 7a, 2026-10-25)

Search history exists only for signed-in users (the developer's choice): every history endpoint requires sign-in (`401 UNAUTHENTICATED` otherwise) and works only on the signed-in user's own entries. `POST` and `DELETE` also require the CSRF token (`X-XSRF-TOKEN`, §6). All history endpoints share a limit of 120 requests per minute per user (`429`). Visitors who are not signed in keep their recent searches in their browser only.

* `GET /api/history/searches` returns the 10 most recent searches, newest first: `{ "data": [{ "searchId", "query", "searchedAt" }] }`.
* `POST /api/history/searches` with `{ "query": "Rose" }` records a search the user ran (the frontend sends it when a search is submitted) and returns `200` with the entry. The text is validated and normalized like `q` in §24 (`400 VALIDATION_ERROR` with `details.query`). The same text, ignoring case, is one entry: searching it again moves it to the top with the new spelling and keeps its `searchId`.
* `DELETE /api/history/searches/{searchId}` returns `204`, or `404 SEARCH_NOT_FOUND` when the user has no such entry (another user's ID answers the same).
* Only the newest 50 searches per user are kept; older ones are deleted. Search text is never written to server logs.

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

Implemented (Phase 7a, 2026-10-25): signed-in users only (`401` otherwise), CSRF token required, same rate limit as §28. Returns `204`. The plant must exist and be Published (`404 PLANT_NOT_FOUND`); a missing `plantId` returns `400 VALIDATION_ERROR`. Opening the same plant again within 30 minutes is not recorded as a new view. Only the newest 200 views per user are kept. The frontend records a view each time Plant Detail loads a plant for a signed-in user.

---

# 30. View History Retrieval

```http
GET /api/history/views
```

Authentication is optional depending on the identity model.

The endpoint should not expose another user's history.

Not implemented: view history is not shown to users in the MVP (PRD §17).

---

# 31. Favorites

Favorites require authentication.

Implemented (Phase 6b, 2026-10-23). Every favorites endpoint requires sign-in (`401 UNAUTHENTICATED` otherwise) and works only on the signed-in user's favorites. `POST`, `DELETE` and `PATCH` also require the CSRF token (`X-XSRF-TOKEN`, §6). All are limited to 60 requests per minute per user (`429`).

The implemented item shape is `{ "favoriteId", "order", "plant": { "id", "name", "plantGroup", "imageUrl" } }`. `imageUrl` is the card image (a photo when there is one, otherwise an illustration) or `null`. Only Published plants are listed; favorites of plants that stop being Published are kept but hidden, and `order` counts only the listed favorites (1…n).

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

Implemented (Phase 6b): **idempotent**, so the bookmark can work as a toggle and a double click is harmless.

* `201 Created` with `{ "data": <favorite> }` when the plant is saved.
* `200 OK` with the existing favorite when the plant was already saved.
* Optional `order` (1 or more, clamped to the end) saves the plant at that position; Undo uses it to put a removed favorite back where it was. Without it, the plant is added at the end.
* `403 EMAIL_NOT_CONFIRMED` until the user has confirmed their email address (D-041).
* `404 PLANT_NOT_FOUND` when the plant does not exist or is not Published.
* `409 FAVORITES_LIMIT` when the user already has 500 favorites.
* `400 VALIDATION_ERROR` for a missing `plantId` or an `order` below 1.

---

# 33. Remove Favorite

```http
DELETE /api/favorites/{favoriteId}
```

Authentication required.

The backend must verify ownership.

Implemented (Phase 6b): another user's favorite ID answers `404 FAVORITE_NOT_FOUND`, the same as an ID that does not exist. The remaining favorites move up to close the gap.

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

Implemented (Phase 6b): the list must contain each of the user's listed favorites exactly once, with orders 1…n; otherwise `400 VALIDATION_ERROR` and nothing changes. A valid list is saved in one transaction and answers `204 No Content`.

---

# 35. Authentication Flow

Implemented: see §6 (D-070). Sign-in with Google and GitHub: §6 (Phase 6a-2).

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

### Implemented (Phase 8a, 2026-10-25)

* Open to everyone (D-071). Requires the CSRF token (`X-XSRF-TOKEN`, §6), because a multipart POST could otherwise be sent from any site. 6 requests per minute per client IP (`429 RATE_LIMITED`); a 20-second request timeout (`503 REQUEST_TIMEOUT`).
* `image`: one file of at most 10 MB (`413 PAYLOAD_TOO_LARGE`), JPEG, PNG or WebP as judged from its bytes (the name and declared type are ignored), at least 64 pixels on each side. Anything else returns `400 VALIDATION_ERROR` with `details.image`.
* `200`: the response in §37 without `requestId` (nothing is stored, so there is nothing to refer to). Each candidate also has `plant.scientificName` and `plant.plantGroupId` (the plant group's slug, as in Plant Detail §15, or `null`; added in Phase 8b for the More action); there are at most three, and no score of any kind.
* `422 IDENTIFICATION_FAILED`: the provider answered, but none of its likely suggestions is a Published encyclopedia plant (§38). The message says the photo may be unclear or the plant may not be in the encyclopedia yet.
* `503 IDENTIFICATION_UNAVAILABLE`: identification is not configured, the app-wide budget is used up, or the provider failed or timed out.

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

Implemented synchronously (Phase 8a): a successful response has `status: "completed"`; failures are the errors in §36. `GET /api/identification/{requestId}` does not exist.

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

Implemented (Phase 8a): 6 requests per minute per client IP; app-wide at most 20 provider calls per minute and 400 per day (in memory, per API instance); an identical photo within 24 hours is answered from a cache of suggested names without a provider call.

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

Implemented (Phase 8a): uploads are processed in memory and never stored or logged. The copy sent to Pl@ntNet is a JPEG of at most 1280 pixels with no metadata (location included). The cache keeps only suggested names and scores, keyed by a hash of that copy.

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
