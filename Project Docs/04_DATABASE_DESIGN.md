# Plant Encyclopedia — Database Design

This document defines the conceptual and logical database design for Plant Encyclopedia.

The goal is to provide a stable data model for:

* Plant encyclopedia content
* Taxonomy and cultivar information
* External data provenance
* Images
* Plant identification
* Search
* Explore recommendations
* Favorites
* User history
* Similar plants
* Retailer information

The database should support the current MVP without unnecessarily designing for hypothetical future scale.

---

# 1. Database Goals

The database should provide:

1. A stable internal plant identity
2. Structured taxonomy
3. Cultivar support
4. Source provenance
5. Reliable plant content
6. Efficient search
7. Image metadata
8. User-specific saved data
9. Recommendation signals
10. Identification history
11. Retailer/location information
12. Future extensibility

---

# 2. Database Technology

## Primary Database

**PostgreSQL**

## ORM

**Entity Framework Core**

The exact PostgreSQL provider and EF Core package configuration will be determined during implementation.

---

# 3. Core Data Principles

## 3.1 Internal IDs Are the Stable Identity

The application should use its own internal primary keys.

External provider IDs must not become the application's primary identity.

Example:

```text
Plant
-----
Id: internal application ID
ExternalSource: iNaturalist
ExternalId: provider-specific ID
```

### Rationale

External providers may change schemas, identifiers, or integration strategies.

The application's internal identity should remain stable.

---

# 4. Conceptual Entity Model

The core model is approximately:

```text id="0d3b4z"
                         ┌──────────────┐
                         │    Plant     │
                         └──────┬───────┘
                                │
              ┌─────────────────┼─────────────────┐
              │                 │                 │
              ▼                 ▼                 ▼
         PlantImages       PlantCare         PlantSources
              │                 │
              │                 ├── Blooming
              │                 ├── Health
              │                 └── About
              │
              ▼
          Taxonomy
              │
              ▼
         PlantGroup
              │
              ▼
          Category


User ────────┬──────── Favorite ──────── Plant
             │
             ├──────── ViewHistory ───── Plant
             │
             └──────── SearchHistory

IdentificationRequest
        │
        └──── IdentificationCandidate ──── Plant

Plant ───────── SimilarPlant ───────── Plant

Plant ───────── Retailer / RetailerLocation
```

This is a conceptual model. Exact table boundaries may change during implementation.

---

# 5. Plant Entity

`Plant` is the central encyclopedia entity.

Conceptual fields:

```text id="tqf6q7"
Plant
-----
Id
DisplayName
ScientificName
CultivarName      (nullable)
Description
PlantGroupId      (nullable)
TaxonomyId        (nullable)
CreatedAt
UpdatedAt
Status
```

### Implemented Rules (InitialCreate)

* `plant_group_id` is nullable so drafts and unreviewed imports can exist without a curated group.
* A Published plant must have a group: `CHECK (status <> 'Published' OR plant_group_id IS NOT NULL)`.
* `status` is stored as text and limited to `Draft`, `Validated`, `Published`, `NeedsReview`, `Archived`.

### Responsibilities

The Plant entity represents the application's user-facing plant identity.

It should not contain every possible piece of content directly.

Related information may be stored in dedicated tables.

---

# 6. Plant Naming

A plant may have multiple names.

Potential concepts include:

* Common name
* Scientific name
* Cultivar name
* Local/common names
* Synonyms

The primary display name should be determined by application rules rather than assuming that the database's first string field is always the correct UI name.

### Implemented Model (InitialCreate)

Canonical names live on `Plant` (`DisplayName`, `ScientificName`, `CultivarName`).

Alternative searchable names live in `PlantName`:

```text
PlantName
---------
Id
PlantId
Name            (stored exactly as written)
NameType        (Common | Synonym)
LanguageCode    (nullable, BCP 47 tag such as en, ko, zh-Hant, es-419)
CreatedAt
```

Rules:

* `Common` names require a language code; `Synonym` names may have one or `NULL`.
* The database checks only the general shape of the language tag; canonical casing is application validation.
* `UNIQUE (plant_id, name_type, name, language_code) NULLS NOT DISTINCT` — two identical names with a `NULL` language are duplicates.
* Uniqueness is case-sensitive. Names keep their original capitalization; search flexibility (case, spacing, punctuation) belongs to the search implementation.

---

# 7. Cultivar Model

Cultivars are important to the product because a user may know only a broad plant type but want to identify a specific cultivar.

Conceptually:

```text id="o7z6sn"
Plant
  │
  └── Cultivar information
```

A cultivar may be represented as part of the plant identity when the application considers the cultivar a distinct user-facing encyclopedia entity.

Example:

```text
Rosa 'Peace'
Rosa × hybrida
```

### Important Rule

Different cultivars should be distinguishable.

The recommendation system must be able to treat:

```text
Rosa 'Peace'
Rosa 'Mr. Lincoln'
Rosa 'Iceberg'
```

as different entities.

---

# 8. Plant Group

`PlantGroup` represents a user-facing intermediate grouping such as:

* Roses
* Tulips
* Orchids
* Hydrangeas

Conceptually:

```text id="y46t4m"
Category
   ↓
PlantGroup
   ↓
Plant
```

### Example

```text
Flowers
  ↓
Roses
  ↓
Rosa 'Peace'
```

This supports the product's Category → Plant Group → Specific Plant browsing hierarchy.

---

# 9. Category

Categories represent the major Explore categories.

Current categories:

* Flowers
* Trees
* Fruits
* Vegetables
* Herbs
* Succulents

A category should be represented as a stable database entity rather than hard-coded throughout the frontend.

---

# 10. Taxonomy

Taxonomy stores structured biological classification.

Potential fields include:

```text id="h3qvbs"
Taxonomy
--------
Id
Kingdom
Phylum
Class
TaxonOrder
Family
Genus
Species
Subspecies
Variety
```

Not every plant will necessarily have every classification level populated.

The order rank is stored as `TaxonOrder` / `taxon_order` because `order` is a reserved word in SQL.

### Principle

The database may store detailed taxonomy even when the UI displays only a small subset.

---

# 11. Plant Taxonomy Relationship

A Plant may reference a taxonomy record.

Conceptually:

```text id="x8w8p4"
Plant
  │
  └── Taxonomy
       ├── Family
       ├── Genus
       ├── Species
       └── ...
```

The application should avoid duplicating the same taxonomy information across many unrelated rows when normalization provides a clear benefit.

---

# 12. Plant Images

Images should be represented separately from the core Plant entity.

Conceptual model:

```text id="4v6o4j"
Plant
  │
  └── PlantImage
          ├── Url
          ├── ThumbnailUrl
          ├── MediumUrl
          ├── LargeUrl
          ├── Source
          ├── SourceId
          ├── AltText
          ├── Width
          ├── Height
          ├── License
          └── CreatedAt
```

### Rationale

A plant may have multiple images.

Images also require their own provenance and metadata.

---

# 13. Image Provenance

For externally sourced images, preserve relevant information such as:

* Provider
* External image ID
* Original source URL where appropriate
* License information
* Attribution requirements
* Retrieval timestamp

The application should not assume that an image is freely reusable merely because it was publicly accessible.

---

# 14. Image Storage

Large image binaries should not normally be stored directly in PostgreSQL.

The intended architecture is:

```text id="u0z8fi"
Image File
   ↓
Object Storage
   ↓
CDN / Image Delivery
```

The database stores references and metadata.

AWS S3 is the current cloud direction.

---

# 15. Plant Care

Care information should be structured enough to support concise Quick Care cards.

Conceptual model:

```text id="p5t0ca"
PlantCare
---------
PlantId
Light
Water
Temperature
Soil
```

Additional detailed care fields may be added later if supported by reliable data.

### Implemented Model (InitialCreate)

`PlantCare` is one-to-one with `Plant` (`plant_id` is both primary key and foreign key; deleted with the plant).

```text
PlantCare
---------
PlantId
LightSummary / LightDetails
WaterSummary / WaterDetails
TemperatureMinC / TemperatureMaxC   (numeric(4,1), °C)
TemperatureDetails
SoilSummary / SoilDetails
VerificationStatus                  (Unverified | Verified)
VerifiedAt
CreatedAt
UpdatedAt
```

Rules:

* Every care field is nullable. No row means no care data; a `NULL` field means that card has no data.
* Light, water, and soil use free-text summaries; standardized light values are deferred until filtering requires them. Soil pH is not stored.
* Temperatures must be between -60 and 60 °C, with min ≤ max when both are present.
* `Verified` requires `VerifiedAt`.
* Provenance and verification are separate. Care provenance is recorded by `PlantSource` rows with `SourceType = Care`. **A source record does not mean the care data is verified**; only `VerificationStatus = Verified` does. Unverified care data must not be presented as verified.
* Verification covers the whole care record. Per-field provenance is deferred.

---

# 16. Care Information Principles

The database should distinguish between structured values and explanatory text.

For example:

```text
Light
-----
Level: Full Sun
Description: Requires several hours of direct sunlight.
```

This allows the UI to display a compact summary while retaining more detailed information.

---

# 17. Blooming

Blooming information should be represented separately or as a dedicated domain component.

Potential fields:

```text id="x5rc4p"
Blooming
--------
PlantId
StartMonth
EndMonth
BloomingType
Summary
```

Examples:

* May–October
* Repeat bloomer
* Spring bloomer

Exact structure should support plants with different blooming patterns.

---

# 18. Health

Health information may include:

```text id="r8j4jk"
PlantHealth
-----------
PlantId
Summary
CommonIssues
DiseaseResistance
```

Only information supported by appropriate sources should be treated as authoritative.

---

# 19. About / Background

About information may contain:

* Origin
* Historical background
* Cultivation history
* Short contextual information

Potential model:

```text id="7s3c2q"
PlantAbout
----------
PlantId
Origin
History
Summary
```

The UI may present this information in an expandable section.

---

# 20. Plant Sources

Source provenance should be represented explicitly.

Conceptual model:

```text id="3pr9mm"
PlantSource
-----------
Id
PlantId
Provider
ExternalId
SourceType
SourceUrl
RetrievedAt
LastVerifiedAt
```

Possible providers include:

* iNaturalist
* GBIF
* Other approved sources

### Implemented Rules (InitialCreate)

* `ExternalId` is nullable so curated sources without a provider ID can be recorded.
* Every source must be identifiable: `CHECK (external_id IS NOT NULL OR source_url IS NOT NULL)`.
* Uniqueness uses two partial unique indexes:

```sql
UNIQUE (plant_id, provider, external_id, source_type) WHERE external_id IS NOT NULL
UNIQUE (plant_id, provider, source_url,  source_type) WHERE external_id IS NULL
```

* These prevent duplicate provenance for the same plant and source type. Several cultivars may cite the same species-level external taxon.
* They do **not** prevent duplicate `Plant` rows created by concurrent ingestion; that requires a plant identity key (see §69).
* Deleting a plant that has source rows is restricted, so provenance is never removed silently.

---

# 21. Source Types

Potential source types include:

```text id="6s1c9f"
Taxonomy
Description
Care
Image
Blooming
Health
About
```

This allows different pieces of information to have different provenance.

---

# 22. External Identity

A plant may have multiple external identifiers.

Therefore, external identity should not be represented as one fixed column such as:

```text
Plant.iNaturalistId
```

Instead, a source mapping model should allow:

```text id="v8j9c2"
Plant
  │
  ├── iNaturalist → ExternalId
  ├── GBIF        → ExternalId
  └── FutureProvider → ExternalId
```

This keeps provider integrations extensible.

---

# 23. Data Ingestion

The database should support the following ingestion flow:

```text id="b3v1tr"
External Provider
       ↓
Provider Adapter
       ↓
Normalized Data
       ↓
Validation
       ↓
Duplicate Detection
       ↓
Plant / Related Entities
       ↓
Database
```

The ingestion process should be idempotent where practical.

Running the same ingestion operation twice should not unnecessarily create duplicate entities.

---

# 24. Duplicate Detection

Duplicate detection should consider multiple identifiers and normalized names.

Potential signals:

* External provider ID
* Scientific name
* Taxonomic identity
* Cultivar identity
* Synonyms

The exact matching algorithm belongs to the application layer.

The database should provide appropriate unique constraints to protect known identities.

---

# 25. User Entity

Authenticated users require a persistent user record.

Conceptual fields:

```text id="h0e3pg"
User
----
Id
ExternalAuthId / AuthenticationIdentifier
CreatedAt
UpdatedAt
```

The exact authentication implementation is deferred.

Sensitive authentication information should not be unnecessarily duplicated in the application's own database.

---

# 26. Favorites

Favorites represent a user's saved plants.

Conceptual model:

```text id="7k0g9p"
Favorite
--------
Id
UserId
PlantId
Order
CreatedAt
UpdatedAt
```

### Constraints

A user should not be able to favorite the same Plant more than once.

Conceptually:

```text
UNIQUE(UserId, PlantId)
```

---

# 27. Favorite Ordering

The MVP stores a logical ordering value.

Example:

```text
Plant A → Order 1
Plant B → Order 2
Plant C → Order 3
```

The exact ordering implementation may use integers or another suitable strategy.

Pixel coordinates must not be stored.

---

# 28. Search History

Search history represents explicit user queries.

Conceptual model:

```text id="k0gq66"
SearchHistory
-------------
Id
UserId / AnonymousIdentifier
Query
CreatedAt
```

The exact anonymous identity strategy will be determined during implementation.

---

# 29. Search History Rules

Search history is different from plant view history.

Deleting a search history entry should not delete:

* The plant
* The plant's database record
* Favorites
* Other users' data

---

# 30. View History

View history represents plants the user has actually opened.

Conceptual model:

```text id="6ntj1s"
ViewHistory
-----------
Id
UserId / AnonymousIdentifier
PlantId
ViewedAt
```

Multiple views may be useful as behavioral signals.

The application may later aggregate or deduplicate these events for recommendation purposes.

---

# 31. Anonymous User Behavior

The product should support useful discovery without requiring immediate login.

Anonymous users may have local/device-associated:

* Search history
* View history
* Temporary recommendation signals

Persistent cross-device Favorites require authentication.

The exact anonymous identity mechanism should prioritize privacy and simplicity.

---

# 32. Identification Request

Identification requests represent image identification attempts.

Conceptual model:

```text id="8n0m3k"
IdentificationRequest
---------------------
Id
UserId / AnonymousIdentifier
ImageHash
Status
CreatedAt
CompletedAt
```

Potential statuses:

* Pending
* Processing
* Completed
* Failed

---

# 33. Identification Candidate

Each identification request may produce multiple candidates.

Conceptual model:

```text id="h7a2bn"
IdentificationRequest
        │
        └── IdentificationCandidate
                ├── PlantId
                ├── Rank
                └── Source
```

Potential fields:

```text
IdentificationCandidate
-----------------------
Id
IdentificationRequestId
PlantId
Rank
Provider
CreatedAt
```

The database does not need to expose AI confidence percentages to users.

Internal provider scores may be stored if useful for ranking/evaluation, but this is optional and should not automatically become a user-facing value.

---

# 34. Identification Candidate Validation

A candidate should reference a validated `Plant` whenever it becomes a normal user-facing result.

An unvalidated AI string should not automatically become a Plant record.

This prevents AI hallucinations from silently polluting the encyclopedia database.

---

# 35. Identification Result Retention

Identification history may be useful for:

* Debugging
* Evaluation
* Model/provider comparison
* Abuse monitoring
* Product improvement

However, image retention should be limited according to privacy, storage, and product requirements.

The system should avoid retaining raw user images indefinitely without a clear purpose.

---

# 36. Similar Plants

Similarity is directional only if the product requires it.

Conceptual model:

```text id="v9p4ch"
SimilarPlant
------------
PlantId
SimilarPlantId
Rank
Reason
```

This allows:

```text
Plant A → Plant B
Plant A → Plant C
Plant A → Plant D
```

### MVP

Similarity can be generated using:

* Taxonomy
* Plant group
* Curated relationships

Visual similarity is deferred.

---

# 37. Similarity Constraints

A plant should not be considered similar to itself.

Conceptually:

```text
PlantId != SimilarPlantId
```

Duplicate relationships should also be prevented where appropriate.

---

# 38. Retailer

Retailers represent businesses where users may potentially find a plant.

Conceptual fields:

```text id="9j0v6h"
Retailer
--------
Id
Name
Type
Website
Rating
CreatedAt
UpdatedAt
```

Possible types:

* Nursery
* Garden Centre
* Flower Shop
* Online Retailer

---

# 39. Retailer Location

A retailer may have one or more physical locations.

Conceptual model:

```text id="j4z2xq"
Retailer
   │
   └── RetailerLocation
          ├── Address
          ├── Latitude
          ├── Longitude
          ├── City
          └── Region
```

This supports distance-based sorting.

---

# 40. Retailer Availability

The database must distinguish between:

* Retailer relevance
* Known availability
* Confirmed live inventory

The application must not imply confirmed inventory unless a reliable inventory source exists.

A simple retailer relationship should therefore not automatically mean:

> "This plant is currently in stock."

---

# 41. Retailer–Plant Relationship

If the application stores known relationships between plants and retailers, this should be represented explicitly.

Conceptual model:

```text id="l5q8z7"
RetailerPlant
-------------
RetailerLocationId
PlantId
Source
LastVerifiedAt
Price
Currency
AvailabilityStatus
```

This structure should only be implemented if reliable data is available.

---

# 42. Price Data

If approximate pricing is stored, include:

* Amount
* Currency
* Source
* Retrieved/observed date
* Product format where relevant

Examples of product format:

* Potted plant
* Cut flowers
* Bouquet
* Bare root
* Seeds

Prices should not be treated as permanently accurate.

---

# 43. Search Indexing

PostgreSQL should support the initial search implementation.

Potential indexed fields include:

* Common/display name
* Scientific name
* Cultivar name
* Plant group
* Taxonomic names

The exact indexing strategy should be determined based on actual query patterns.

A dedicated search engine is not required for the initial MVP.

---

# 44. Database Indexing Principles

Indexes should support real application queries.

Likely candidates include:

```text
Plant.display_name
Plant.scientific_name
PlantGroup.category_id
Plant.taxonomy_id
Favorite.user_id
Favorite.plant_id
ViewHistory.user_id
ViewHistory.plant_id
ViewHistory.viewed_at
SearchHistory.user_id
SearchHistory.created_at
IdentificationRequest.created_at
IdentificationCandidate.identification_request_id
RetailerLocation.latitude / longitude
```

Exact indexes should be validated through query plans and real usage.

Do not create indexes indiscriminately.

PostgreSQL does not index foreign-key columns automatically. Foreign-key indexes are declared explicitly in the EF Core configuration unless an existing non-partial index already starts with the foreign-key column.

---

# 45. Foreign Key Strategy

Relationships should use foreign keys where appropriate.

Examples:

```text id="w4x5pk"
PlantGroup.CategoryId → Category.Id

Plant.PlantGroupId → PlantGroup.Id

Plant.TaxonomyId → Taxonomy.Id

Favorite.UserId → User.Id

Favorite.PlantId → Plant.Id

ViewHistory.PlantId → Plant.Id

IdentificationCandidate.PlantId → Plant.Id
```

Foreign keys should protect database integrity.

---

# 46. Delete Behavior

Delete behavior should be chosen intentionally for each relationship.

Examples:

* Deleting a Favorite should not delete the Plant.
* Deleting a Plant should not silently delete shared source data without consideration.
* Deleting a User may require deletion/anonymization of user-specific records.
* Deleting a PlantImage should not automatically delete the Plant.

The exact EF Core cascade configuration should be explicitly reviewed during implementation.

---

# 47. Soft Delete

Soft delete should not be added to every table by default.

It should be used only where there is a real need for:

* Auditability
* Recovery
* Regulatory requirements
* Moderation
* Historical references

For simple user actions such as removing a Favorite, normal deletion is sufficient.

---

# 48. Audit Timestamps

Persistent entities should generally support:

```text
CreatedAt
UpdatedAt
```

where useful.

Event/history entities may instead primarily use:

```text
OccurredAt
```

The database should avoid meaningless timestamps on entities where they provide no useful information.

---

# 49. Data Quality Status

Plant records may benefit from a data quality/status field.

Potential states:

```text id="q8s7w2"
Draft
Validated
Published
NeedsReview
Archived
```

The exact workflow is deferred.

### Rationale

Not every externally ingested record should necessarily become immediately visible to users.

---

# 50. Published vs Internal Data

The database may contain information that is not yet ready for public display.

The application should distinguish between:

```text
Imported
Validated
Published
```

where necessary.

This prevents raw or incomplete ingestion from accidentally becoming public content.

---

# 51. Normalization vs Convenience

The database should be normalized where it improves:

* Data integrity
* Reuse
* Maintainability
* Consistency

However, normalization should not be taken to an extreme that makes simple queries unnecessarily complicated.

The goal is a practical relational model.

---

# 52. Denormalization

Denormalization may be introduced later for measured performance problems.

Examples:

* Cached search fields
* Recommendation aggregates
* Precomputed counts
* Materialized views

Denormalization should be evidence-driven rather than speculative.

---

# 53. Recommendation Data

The database should store the behavioral information required to build recommendations.

Initial signals:

* Views
* Favorites
* Searches
* Category interactions
* Plant-group interactions

A dedicated recommendation database is not required for the MVP.

---

# 54. Recommendation Deduplication

Recommendation queries must be able to identify a plant uniquely.

The system should prevent the same plant/cultivar entity from appearing multiple times in a recommendation batch.

This should operate at the entity level, not merely at the image level.

---

# 55. Anonymous vs Authenticated Data

The database design should avoid unnecessarily tying every public interaction to an authenticated user.

Conceptually:

```text id="t8x0k9"
Public Content
    ↓
No login required

Personalized / Persistent Data
    ↓
User identity where required
```

This supports low-friction discovery.

---

# 56. Privacy Principles

User behavioral data should be collected only when it provides meaningful product value.

Important principles:

* Minimize collected data.
* Avoid unnecessary personal information.
* Do not store raw images indefinitely without purpose.
* Protect user-specific history.
* Do not expose one user's behavioral data to another user.
* Define retention policies for analytics/history data later.

---

# 57. Transaction Boundaries

Operations that modify multiple related records should use database transactions where consistency requires it.

Examples:

### Favorite Reordering

If multiple ordering values change, the operation should maintain a valid ordering state.

### Plant Ingestion

If a plant and required related records are created together, partial ingestion should not leave invalid state.

### Identification Persistence

If identification request and candidates are persisted together, consistency should be preserved.

---

# 58. Concurrency

The database design should account for concurrent user actions.

Examples:

* Two favorite requests arriving simultaneously
* Reordering Favorites from multiple devices
* Simultaneous ingestion of the same plant
* Multiple users triggering ingestion of the same external plant

Unique constraints and transactions should protect data integrity.

---

# 59. Plant Ingestion Race Condition

A possible race condition is:

```text
User A searches Plant X
User B searches Plant X
        ↓
Both find no local record
        ↓
Both request external data
        ↓
Both attempt to insert Plant X
```

The database should use appropriate uniqueness constraints and/or transaction strategies to prevent duplicate entities.

---

# 60. Migration Strategy

Database schema changes should be managed through EF Core migrations.

Do not manually modify production schema without recording the change in the migration history.

Conceptual workflow:

```text id="6k4y3n"
Model Change
    ↓
Migration
    ↓
Review
    ↓
Test
    ↓
Apply
```

---

# 61. Seed Data

The application may use seed data for:

* Initial categories
* Development plant records
* Test users
* Test relationships

Production seed data should be treated carefully.

Reference data such as categories may be appropriate for deterministic initialization.

---

# 62. Development Database

Local development should use a local PostgreSQL instance, preferably through Docker where practical.

Developers should be able to:

```text id="7u4v3r"
Start PostgreSQL
    ↓
Apply Migrations
    ↓
Seed Development Data
    ↓
Run API
```

---

# 63. Test Database

Automated tests should not depend on the developer's personal database state.

Tests should use isolated test data.

Depending on test type, this may involve:

* Temporary PostgreSQL
* Dedicated test database
* Transaction rollback
* Containers
* Test fixtures

The exact strategy belongs to the testing implementation.

---

# 64. Production Database

Production PostgreSQL should be managed separately from local development.

The production database should have:

* Secure credentials
* Restricted network access
* Backups
* Monitoring
* Migration procedures
* Recovery procedures

The exact AWS service is deferred to infrastructure design.

---

# 65. Database Security

Important principles:

* Database credentials must never be committed to Git.
* Production database access should be restricted.
* Application users should not receive direct database access.
* Sensitive user data should be protected.
* SQL injection must be prevented through parameterized queries/ORM usage.
* Database backups must be protected.

---

# 66. Initial MVP Entity Priority

The MVP does not need every conceptual entity immediately.

Recommended implementation priority:

### Phase 1 — Core Encyclopedia

```text
Category
PlantGroup
Plant
Taxonomy
PlantName
PlantImage
PlantSource
PlantCare
```

Implemented by the `InitialCreate` migration. External identifiers are stored in `PlantSource`; there is no separate external-identity table.

### Phase 2 — User Features

```text
User
Favorite
SearchHistory
ViewHistory
```

### Phase 3 — Identification

```text
IdentificationRequest
IdentificationCandidate
```

### Phase 4 — Discovery

```text
SimilarPlant
Recommendation-related data
```

### Phase 5 — Where to Buy

```text
Retailer
RetailerLocation
RetailerPlant
```

The exact order may change according to API and implementation dependencies.

---

# 67. Relationship Summary

A simplified relationship model is:

```text id="1z8w0q"
Category
   │ 1
   │
   └──── * PlantGroup
               │
               │ 1
               └──── * Plant
                         │
          ┌──────────────┼───────────────┐
          │              │               │
          ▼              ▼               ▼
      PlantImage     PlantCare       PlantSource
                         │
                    ┌────┼─────┐
                    ▼    ▼     ▼
                Blooming Health About


User
 │
 ├──── * Favorite ───────── Plant
 │
 ├──── * ViewHistory ────── Plant
 │
 └──── * SearchHistory

IdentificationRequest
 │
 └──── * IdentificationCandidate ─── Plant

Plant
 │
 └──── * SimilarPlant ─────────────── Plant

Plant
 │
 └──── * RetailerPlant ────────────── RetailerLocation
```

---

# 68. Database Design Principles

Future schema decisions should follow these principles:

### 1. Stable internal identity

External IDs are mappings, not primary application identity.

### 2. Trustworthy data

Important plant information must have a defensible source.

### 3. Explicit relationships

Plant, taxonomy, cultivar, source, image, and user data should not be hidden in arbitrary JSON when relational structure provides meaningful benefits.

### 4. Practical normalization

Normalize where it improves integrity; avoid unnecessary complexity.

### 5. Integrity at the database level

Use:

* Foreign keys
* Unique constraints
* Check constraints
* Appropriate indexes

where appropriate.

### 6. Application logic belongs in the application layer

The database should enforce data integrity but should not become a replacement for domain logic.

### 7. Optimize based on evidence

Do not prematurely introduce specialized search databases, denormalized tables, or complex partitioning.

---

# 69. Open Database Questions

The following should be finalized during implementation:

* Exact Plant/Cultivar table structure
* Exact taxonomy normalization
* Search indexing strategy
* Anonymous user identifier strategy
* Authentication provider and User model
* Image processing pipeline
* Image retention policy
* Retailer data provider
* Geographic query strategy
* Recommendation event aggregation
* Exact identification history retention
* Production backup strategy
* PostgreSQL hosting choice
* Plant identity / duplicate-detection key for concurrent ingestion (deferred until ingestion is designed)
* Normalization of plant names and language tags for search and duplicate detection
* Per-field care provenance

These decisions should be documented when finalized.

### Implementation Notes

* `CreatedAt` / `UpdatedAt` are set by `PlantEncyclopediaDbContext.SaveChanges`. Raw SQL, `ExecuteUpdate`, and `ExecuteDelete` bypass this, and changing a child row does not update its parent's `UpdatedAt`. The `DEFAULT now()` column defaults apply only to inserts.
* UUID primary keys are generated client-side by Npgsql (UUIDv7); the database has no default for `id` columns.

---

# 70. Database Quality Bar

Before implementation is considered complete, the database design should answer:

* Can every plant have a stable internal identity?
* Can cultivars be represented separately?
* Can taxonomy be stored without duplicating unnecessary data?
* Can multiple external sources refer to the same plant?
* Can image provenance be preserved?
* Can care information be stored separately from AI output?
* Can Favorites be persisted and reordered?
* Can search and view history support recommendations?
* Can identification candidates reference validated plants?
* Can duplicate ingestion be prevented?
* Can retailer information be represented without falsely claiming inventory?
* Can schema changes be safely migrated?
* Can the database support automated testing?
* Can the schema evolve without breaking the public API?

---

# 71. Guiding Database Principle

> **The database should preserve a stable, trustworthy internal representation of plant knowledge and user state while remaining simple enough to evolve with the product.**
