# Plant Encyclopedia — Decision Log

This document records important product, UX, data, and engineering decisions for Plant Encyclopedia.

The purpose of this document is to preserve **why** decisions were made, not merely what the current product does.

Future implementation decisions should respect accepted decisions unless there is a strong reason to revisit them.

---

## 1. How to Use This Document

Each decision should answer:

* What was decided?
* Why was it decided?
* What alternatives were considered?
* What does this decision imply for future implementation?

Decision statuses:

* **Accepted** — Current project direction. Do not casually change.
* **Deferred** — Intentionally postponed until a later phase.
* **Rejected** — Considered and explicitly excluded.
* **Superseded** — Replaced by a newer decision.

When a major accepted decision changes, update this document rather than silently changing implementation requirements.

---

# 2. Product Direction Decisions

## D-001 — Image-First Plant Discovery

**Status:** Accepted

### Decision

Plant Encyclopedia will be designed as an **image-first plant discovery experience**.

Images should be a primary part of:

* Explore
* Search results
* Category browsing
* Plant groups
* Identification results
* Favorites
* Plant Detail

### Rationale

Plant discovery is inherently visual. Users often recognize a plant visually before knowing its name.

The product should therefore feel closer to a visual discovery platform than a traditional text-heavy encyclopedia.

### Alternatives Considered

* Text-first encyclopedia
* Search-engine-style interface
* Chatbot-first plant assistant

These were rejected because they do not match the primary discovery behavior of the target user.

### Implications

* Image quality is a major product concern.
* Image loading performance is important.
* Responsive images, thumbnails, lazy loading, and appropriate image formats should be considered during implementation.
* Visual hierarchy should not sacrifice usability or accessibility.

---

## D-002 — Primary User: Curious Plant Discoverer

**Status:** Accepted

### Decision

The primary user is a general person who encounters a plant and wants to:

1. Identify it
2. Learn more about it
3. Compare related plants
4. Find where to buy it
5. Save it for later
6. Discover other plants

The product is not initially optimized exclusively for professional botanists, horticulturists, or commercial growers.

### Rationale

The product should be approachable to users who may know only a common name such as "rose" but want to discover a more specific cultivar or scientific name.

### Implications

Technical and botanical information should be available without making the interface feel academically intimidating.

---

## D-003 — Core User Journey

**Status:** Accepted

### Decision

The core product journey is:

**Discover → Identify → Compare → Learn → Find → Save → Discover More**

### Rationale

The product should not stop at plant identification.

Identification is an entry point into a broader discovery experience.

### Implications

Features should connect naturally to the next useful action rather than treating identification as an isolated feature.

---

# 3. Plant Data and Trust Decisions

## D-004 — Own Database as User-Facing Source of Truth

**Status:** Accepted

### Decision

The application's own database is the source of truth for user-facing encyclopedia information.

External services such as iNaturalist and GBIF may provide source data, but their raw API responses must not be exposed directly as the application's final content model.

### Rationale

Depending directly on external API responses would make the UI and data model tightly coupled to third-party schemas.

A normalized internal model provides:

* Stable application behavior
* Consistent naming
* Data validation
* Better caching
* Better provenance tracking
* Easier provider replacement

### Implications

External data should be:

**Fetched → Normalized → Validated → Stored → Served from the application's database**

---

## D-005 — External Structured Sources: iNaturalist and GBIF

**Status:** Accepted

### Decision

iNaturalist and GBIF are the primary structured external sources considered for plant taxonomy and related biological information.

### Rationale

The application needs structured, externally verifiable biological information rather than relying solely on generative AI.

### Implications

The data layer should preserve:

* Source/provider
* External identifier
* Retrieval timestamp
* Relevant provenance information

The exact integration strategy belongs to the architecture/API phase.

---

## D-006 — Source Provenance Must Be Preserved

**Status:** Accepted

### Decision

Important plant information should retain source/provenance metadata whenever practical.

### Rationale

Plant Encyclopedia is intended to be a trustworthy reference-oriented application.

When information is obtained from an external source, the application should be able to determine where it came from and when it was retrieved.

### Implications

Future database and ingestion designs should support provenance rather than treating imported data as anonymous content.

---

## D-007 — AI Is Primarily an Identification Engine

**Status:** Accepted

### Decision

AI is primarily responsible for:

* Image interpretation
* Candidate generation
* Identification assistance
* Candidate ranking

AI should not be treated as the authoritative source for encyclopedia content.

### Rationale

Generative AI can produce plausible but incorrect botanical information.

The application should not allow AI-generated care instructions or biological facts to become authoritative simply because they sound convincing.

### Implications

AI-generated identification candidates must pass through application-side validation before being displayed as encyclopedia results.

---

## D-008 — AI Must Not Generate Authoritative Care Information

**Status:** Accepted

### Decision

Core plant care information should be source-backed rather than generated freely by AI.

### Rationale

Care instructions can directly affect how users treat living plants.

Hallucinated information is therefore undesirable.

### Implications

AI may assist with interpretation or candidate generation, but authoritative care information should come from structured and trusted data sources or curated content.

---

## D-009 — No Google Images as a Product Data Source

**Status:** Rejected

### Decision

Google Images is not part of the core plant data architecture.

### Rationale

Using arbitrary image-search results would create uncertainty around:

* Image licensing
* Image provenance
* Data consistency
* Quality control
* Duplicate content
* Long-term reproducibility

The product should instead use controlled image sources and its own normalized data model.

### Implications

The UI should not depend on dynamically embedding arbitrary Google Image results for core encyclopedia content.

---

## D-010 — Cache Is Not the Source of Truth

**Status:** Accepted

### Decision

Caching is used for performance and temporary reuse.

The application's database remains the persistent source of truth.

### Rationale

Cache and persistence have different responsibilities.

### Example

A plant not currently in the database may trigger external lookup and ingestion.

After ingestion:

**External Source → Normalize → Validate → Database → Cache/API response**

### Implications

Cache invalidation and expiration policies should be designed separately from database persistence.

---

# 4. Identification Decisions

## D-011 — Identification Results Must Be Validated Before Display

**Status:** Accepted

### Decision

AI-generated candidates that cannot be matched or validated against the application's plant data should not be presented as normal encyclopedia results.

### Rationale

Showing unvalidated AI guesses as if they were verified plants could reduce user trust.

### Implications

The identification pipeline should distinguish between:

* AI candidate
* Validated candidate
* User-facing encyclopedia entity

---

## D-012 — Identification Results Show Three Candidates Plus More

**Status:** Accepted

### Decision

The identification result screen displays:

* #1 primary candidate
* #2 secondary candidate
* #3 secondary candidate
* More

"More" is an action to explore additional contextual results, not a fourth candidate.

### Rationale

The interface should communicate a small set of useful possibilities without overwhelming the user.

### Implications

The data model and API should support ranked candidates beyond the first three.

---

## D-013 — No Confidence Percentages

**Status:** Accepted

### Decision

The product will not display numerical confidence percentages for plant identification in the MVP.

### Rationale

A percentage such as "87% confident" may imply a level of scientific certainty that the system cannot reliably communicate to ordinary users.

The product should communicate ranking without creating false precision.

### Implications

Identification results should focus on:

* Candidate ranking
* Visual evidence
* Plant names
* Navigation to verified plant information

---

## D-014 — Identification Does Not Navigate Directly to Detail

**Status:** Accepted

### Decision

After image identification, users first see Identification Results.

Selecting a candidate then opens Plant Detail.

### Rationale

Users may want to compare multiple candidates before deciding which result is relevant.

### Navigation

**Identify → Identification Results → Candidate → Plant Detail**

The Back action should return to Identification Results.

---

# 5. Explore and Recommendation Decisions

## D-015 — Explore Is Discovery, Not Search-First

**Status:** Accepted

### Decision

Explore is primarily a visual discovery experience.

Search is available through a search icon rather than dominating the initial Explore screen.

### Rationale

The product should encourage browsing and discovery while still providing fast search when the user knows what they want.

### Implications

Initial Explore should not be dominated by a large persistent search box.

---

## D-016 — Explore and Search Have Different Responsibilities

**Status:** Accepted

### Decision

Explore answers:

> "What interesting plants can I discover?"

Search answers:

> "Which plant am I looking for?"

### Rationale

Combining both behaviors into one interface can make both experiences less focused.

### Implications

Search relevance should dominate search results.

Explore personalization should not override a strong explicit search query.

---

## D-017 — Category Browsing Is Category-Pure

**Status:** Accepted

### Decision

Category pages remain focused on their selected category.

For example:

* Flowers → flower plant groups
* Trees → tree plant groups
* Vegetables → vegetable plant groups

A Flowers page should not inject unrelated tree or vegetable recommendations.

### Rationale

Category browsing has a different purpose from personalized Explore.

The user has already expressed an explicit category interest.

### Implications

Cross-category discovery belongs primarily to Explore.

---

## D-018 — Category → Plant Group → Specific Plant

**Status:** Accepted

### Decision

Category browsing follows:

**Explore → Category → Plant Group → Specific Plant/Cultivar → Detail**

For example:

**Flowers → Roses → Rosa 'Peace' → Plant Detail**

### Rationale

This structure provides a natural hierarchy from broad discovery to specific plant identity.

### Implications

Category and plant-group data should support hierarchical browsing.

---

## D-019 — New Users Receive Diverse Discovery

**Status:** Accepted

### Decision

New or anonymous users should receive approximately balanced category representation and diverse curated discovery.

The feed should not be dominated by popularity alone or generated as pure randomness.

### Rationale

With little behavioral data, aggressive personalization has insufficient evidence.

Pure popularity can also make the experience repetitive.

### Implications

The MVP should use deterministic or rule-based diversity mechanisms before introducing more sophisticated recommendation models.

---

## D-020 — Personalized Explore Uses a 60/20/20 Diversity Guideline

**Status:** Accepted

### Decision

For users with meaningful behavioral data, Explore should approximately follow:

* **60%** strongly related to user interests
* **20%** somewhat related or adjacent
* **20%** substantially different or novel

This is a product guideline, not a requirement that every small batch mathematically equal these percentages.

### Rationale

Personalization should make Explore feel relevant without trapping users in a narrow content bubble.

### Implications

Recommendation logic should explicitly include diversity and novelty.

---

## D-021 — A Plant/Cultivar Appears at Most Once in a Feed

**Status:** Accepted

### Decision

The same plant or cultivar should not appear multiple times in the same recommendation feed.

Different cultivars within the same plant group may appear separately.

### Example

Valid:

* Rosa 'Peace'
* Rosa 'Mr. Lincoln'
* Rosa 'Iceberg'

Invalid:

* Rosa 'Peace' with multiple cards representing the same entity

### Rationale

Duplicate entities make a recommendation feed feel repetitive and artificially reduce discovery breadth.

### Implications

Recommendation queries should deduplicate by plant/cultivar identity, not merely by image.

---

## D-022 — Personalization Is Behavioral, Not Identity-Based

**Status:** Accepted

### Decision

Recommendation personalization should primarily use behavioral signals such as:

* Viewed plants
* Saved plants
* Search activity
* Categories explored
* Plant groups interacted with
* Recent interactions

### Rationale

These signals directly represent user interest in the product.

### Implications

The recommendation system should avoid unnecessarily collecting unrelated personal information.

---

## D-023 — MVP Recommendation System Is Rule-Based

**Status:** Accepted

### Decision

The first recommendation system will use understandable rule-based logic rather than a machine-learning recommendation model.

### Rationale

The project should first establish:

* Useful behavioral signals
* Data collection
* Diversity rules
* Evaluation criteria

before introducing a more complex recommendation model.

### Implications

Recommendation weights should be easy to tune.

A future ML-based recommender may replace or augment this system.

---

# 6. Plant Detail Decisions

## D-024 — One Shared Plant Detail Experience

**Status:** Accepted

### Decision

All entry points use the same Plant Detail experience.

Entry points include:

* Explore
* Search
* Identification
* More
* Favorites
* Category browsing
* Plant group browsing

### Rationale

Users should not receive different plant information or layouts depending on where they found the plant.

### Implications

Plant Detail should be implemented as a reusable product surface.

---

## D-025 — Plant Detail Content Order

**Status:** Accepted

### Decision

The primary order is:

**Hero → Name/Classification → Quick Care → Blooming → Health → About → Where to Buy → Similar Plants**

### Rationale

The sequence moves from:

1. Recognition
2. Identification
3. Immediate actionable information
4. Supporting information
5. Background
6. Purchase intent
7. Further discovery

---

## D-026 — Cultivar Name Has Priority in the Naming Hierarchy

**Status:** Accepted

### Decision

When a cultivar is available, the UI prioritizes the cultivar-friendly name.

Example:

```text
Rosa 'Peace'
Hybrid Tea Rose
Rosa × hybrida
```

Without a cultivar:

```text
Rose
Rosa
```

### Rationale

Ordinary users usually need the recognizable name first, while scientific classification remains available for reference.

### Implications

The database may store detailed taxonomy, but the UI should maintain a concise hierarchy.

---

## D-027 — Quick Care Uses Four Core Cards

**Status:** Accepted

### Decision

Quick Care initially contains:

* Light
* Water
* Temperature
* Soil

Cards can expand for additional detail.

### Rationale

These are high-value, frequently needed care dimensions and can be communicated compactly.

### Implications

The MVP should avoid turning Quick Care into a long gardening manual.

---

## D-028 — Growing Section Is Excluded From MVP

**Status:** Deferred

### Decision

A dedicated detailed "Growing" manual is excluded from the MVP.

Potential future content includes:

* Pruning
* Propagation
* Fertilization
* Winter protection
* Bouquet stem handling
* Advanced cultivation

### Rationale

These features would significantly expand the scope.

The MVP should first validate the core discovery, identification, encyclopedia, and purchasing journey.

### Implications

Do not add a large Growing section during MVP implementation unless explicitly re-approved.

---

## D-029 — Blooming, Health, and About Are Concise

**Status:** Accepted

### Decision

Blooming and Health should initially be short summaries.

About may be expandable/collapsible and can contain a somewhat longer historical or background explanation.

### Rationale

Not every user wants a long article.

The interface should provide useful information while maintaining visual clarity.

---

## D-030 — Similar Plants Is Contextual Discovery

**Status:** Accepted

### Decision

Similar Plants is based on the current plant and appears near the end of Plant Detail.

Initial behavior:

* Show approximately 3 cards
* Horizontal carousel can reveal more
* View More opens a broader contextual discovery page

### Rationale

Similar Plants extends the user's interest from the current plant rather than acting as a global recommendation feed.

### Implications

Similarity should initially use taxonomy and curated relationships.

Visual similarity can be considered later.

---

# 7. Where to Buy Decisions

## D-031 — Where to Buy Is Part of the Core Detail Journey

**Status:** Accepted

### Decision

Where to Buy appears after About and before Similar Plants.

Plant Detail includes a clear entry card:

```text
🌱 Where to Buy

Find nurseries, garden centres & flower shops near you

Explore →
```

### Rationale

After learning about a plant, purchasing is a natural next action.

It should be discoverable without overwhelming the encyclopedia content.

---

## D-032 — Nearby Retailers Are Separate From Online Retailers

**Status:** Accepted

### Decision

Where to Buy distinguishes between:

* Nearby nurseries
* Garden centres
* Flower shops
* Online retailers

### Rationale

Physical and online purchasing have different user needs and data requirements.

---

## D-033 — No Unsupported Live Inventory Claims

**Status:** Accepted

### Decision

The application must not claim that a retailer currently has a plant in stock unless reliable inventory data is available.

### Rationale

Location-based search results do not guarantee inventory.

False availability claims would significantly damage trust.

### Implications

The application may say that a retailer is a relevant place to check, but should not imply confirmed inventory without evidence.

---

## D-034 — Approximate Prices Must Be Sourced

**Status:** Accepted

### Decision

If approximate prices are displayed, they should include source/date context where practical.

### Rationale

Plant and bouquet prices change frequently.

A displayed price without context can become misleading.

---

## D-035 — Location Permission Is Only Requested When Needed

**Status:** Accepted

### Decision

The application should request location permission when the user enters Where to Buy or performs an action that actually requires location.

### Rationale

Requesting location permission on first launch creates unnecessary friction.

### Fallback

If location permission is unavailable or denied:

* Default to Vancouver, BC for the MVP
* Allow manual location changes

The rest of the application remains usable without location permission.

---

# 8. Navigation Decisions

## D-036 — Sidebar Is Primary Destination Navigation

**Status:** Accepted

### Decision

The desktop application uses a ChatGPT-like expandable/collapsible sidebar.

Primary destinations:

* Identify
* Explore
* Favorites
* Login/account

### Rationale

The sidebar provides a persistent, scalable navigation structure without introducing a crowded top navigation bar.

---

## D-037 — Back Navigation Is Separate From Sidebar Navigation

**Status:** Accepted

### Decision

The Back button represents navigation through the user's current stack.

The sidebar represents direct navigation to major destinations.

### Example

```text
Explore
  ↓
Search
  ↓
Plant A
  ↓
Plant B
  ↓
Back → Plant A
```

### Rationale

Replacing Back with sidebar navigation would destroy navigation context.

---

## D-038 — No Persistent Mobile Bottom Navigation

**Status:** Accepted

### Decision

The mobile application will not use a persistent bottom navigation bar.

### Rationale

The primary product actions are better represented by:

* Top app bar
* Slide-in sidebar
* Contextual bottom sheets

A bottom navigation bar would add another competing navigation system.

---

## D-039 — Mobile Uses a Slide-In Sidebar

**Status:** Accepted

### Decision

Mobile navigation uses a slide-in sidebar.

The top bar contains:

* Menu button on the left
* "Plant Encyclopedia" centered
* Login/account button on the right

### Rationale

This preserves the desktop navigation model while adapting it to smaller screens.

---

# 9. Favorites Decisions

## D-040 — Bookmark Is the Only Save Metaphor

**Status:** Accepted

### Decision

The bookmark icon is the primary save/favorite interaction.

Hearts are not used for saving plants.

### Rationale

Bookmarking more directly communicates "save for later" and avoids mixing social-media-style like/favorite semantics.

---

## D-041 — Favorites Require Login

**Status:** Accepted

### Decision

Saving a plant to persistent Favorites requires authentication.

### Rationale

Favorites need to persist across sessions and potentially across devices.

### UX

When an unauthenticated user attempts to save:

```text
Save this plant

Sign in to save plants to your Favorites.

[Sign in] [Cancel]
```

The original save intent should be preserved after login where practical.

---

## D-042 — MVP Uses One Favorites Collection

**Status:** Accepted

### Decision

The MVP has one collection:

**All Favorites**

Folders or custom collections are deferred.

### Rationale

Collections add complexity without validating the core save behavior.

---

## D-043 — Favorites Supports Arrange Mode

**Status:** Accepted

### Decision

Users can explicitly enter an arrange/edit mode.

On mobile:

* Long press enters arrange mode
* Cards jiggle/wobble
* Delete/minus controls appear
* Dragging changes order

On desktop:

* Explicit Arrange/Edit action or contextual control is used
* Hover alone does not activate editing

### Rationale

Normal browsing and editing should not interfere with each other.

---

## D-044 — Favorites Store Logical Order, Not Pixel Coordinates

**Status:** Accepted

### Decision

The MVP stores an explicit logical ordering value.

It does not store arbitrary pixel positions.

### Rationale

Logical ordering is easier to persist, synchronize, and migrate to future layouts.

---

# 10. Loading, Empty, and Error State Decisions

## D-045 — Skeleton UI Is the Default Loading Pattern

**Status:** Accepted

### Decision

General data loading uses skeleton UI.

Plant Detail uses structured skeletons for:

* Hero
* Name
* Quick Care
* Content sections

### Rationale

Skeletons preserve the expected page structure and reduce perceived waiting time.

---

## D-046 — Identification Loading Keeps the Selected Image Visible

**Status:** Accepted

### Decision

During image identification, the selected image remains visible.

The UI provides a concise message such as:

> Identifying your plant…

### Rationale

Users should remain visually connected to the image they submitted.

The application should not expose unnecessary internal AI processing details.

---

## D-047 — No Fake Progress Percentages

**Status:** Accepted

### Decision

The application will not display artificial AI progress percentages or fake processing stages.

### Rationale

Fake progress communicates false information.

The UI should communicate actual state rather than invented precision.

---

## D-048 — Empty States Should Be Contextual

**Status:** Accepted

### Decision

Empty states should explain what is empty and provide a relevant next action.

Examples:

* Search → no matching plants
* Category → no plants available yet
* Favorites → Explore Plants

### Rationale

An empty state should help the user recover rather than simply report the absence of content.

---

## D-049 — Errors Must Be Recoverable

**Status:** Accepted

### Decision

Network, data, and identification errors should provide:

* Clear explanation
* Relevant recovery action
* Retry when appropriate

The application should avoid misleading claims about why an operation failed.

### Example

Identification failure should not automatically claim:

> "This plant does not exist."

Instead, it should communicate that the image may be unclear or the plant may not yet be in the database.

---

# 11. Accessibility and Performance Decisions

## D-050 — Accessibility Is a Product Requirement

**Status:** Accepted

### Decision

Accessibility is part of the product quality bar, not a later cleanup task.

The web experience should support:

* Keyboard navigation
* Accessible icon labels
* Sufficient contrast
* Meaningful alt text
* Adequate touch targets
* Non-color-only status communication
* Reduced-motion preferences
* Accessible alternatives to hover interactions

### Rationale

Interactive visual design should remain usable by people who cannot rely on a mouse, color, or animation.

---

## D-051 — Performance Has Priority Over Decoration

**Status:** Accepted

### Decision

Performance takes priority over decorative interaction.

Priority order:

1. Core task completion
2. Navigation
3. Content comprehension
4. Feedback
5. Decorative interaction

### Rationale

The project is intended to feel premium, but animation must never make the product feel slow.

---

## D-052 — Use Performance-Friendly Image and Motion Techniques

**Status:** Accepted

### Decision

The implementation should prefer:

* WebP/AVIF where appropriate
* Responsive images
* Thumbnails
* Lazy loading
* CDN delivery where appropriate
* GPU-friendly transform/opacity
* Minimal layout recalculation

### Rationale

Plant Encyclopedia is image-heavy, making image delivery a major performance factor.

---

# 12. Interaction and Visual Design Decisions

## D-053 — Subtle Botanical Micro-Interactions

**Status:** Accepted

### Decision

The product may use subtle botanical interactions such as:

* Branch/stem-inspired desktop cursor
* Occasional falling leaves
* Cursor-following interaction in selected areas
* Scroll-driven hero transitions
* Gentle bookmark transitions
* Subtle image hover effects

### Rationale

These interactions provide a distinctive identity inspired by high-end interactive websites while keeping the product focused.

### Constraint

Decorative interaction must remain sparse and optional.

---

## D-054 — No Constant Decorative Animation

**Status:** Accepted

### Decision

Decorative effects such as falling leaves should not run continuously without reason.

Event-based effects and cooldowns should be preferred.

### Rationale

Constant animation creates visual noise, can hurt performance, and may become annoying during normal use.

---

## D-055 — Reduced Motion Must Preserve Functionality

**Status:** Accepted

### Decision

When `prefers-reduced-motion` is enabled, decorative motion should be reduced or disabled without removing functionality.

### Rationale

Motion should enhance the experience, not become a functional dependency.

---

## D-056 — Mobile Does Not Depend on Hover

**Status:** Accepted

### Decision

Important information and actions must not depend exclusively on hover.

Desktop hover interactions may provide additional visual feedback.

Mobile uses tap-based interaction instead.

### Rationale

Touch devices do not provide reliable hover behavior.

---

# 13. Scope Decisions

## D-057 — MVP Focuses on the Core Discovery Loop

**Status:** Accepted

### Decision

The MVP prioritizes:

* Plant discovery
* Image identification
* Plant encyclopedia data
* Search
* Category browsing
* Favorites
* Where to Buy
* Similar Plants
* Responsive UX

### Rationale

These features form a coherent consumer product rather than a collection of disconnected technical demonstrations.

---

## D-058 — Advanced Features Are Intentionally Deferred

**Status:** Deferred

The following may be implemented after the MVP:

* Advanced recommendation ML
* Visual similarity search
* Detailed Growing guides
* Favorites folders
* Advanced personalization
* More sophisticated plant comparison
* Advanced inventory integration
* Additional mobile platforms
* More advanced AI capabilities

### Rationale

The project should first validate the core product experience.

---

# 14. Engineering Process Decisions

## D-059 — Product Decisions Come Before Architecture

**Status:** Accepted

### Decision

Implementation should follow this general order:

**Product → UX → Decisions → Architecture → Database → API → Roadmap → Implementation**

### Rationale

Starting implementation before product behavior is defined creates unnecessary rework.

---

## D-060 — AI-Assisted Development Uses Human Review

**Status:** Accepted

### Decision

AI coding tools may implement substantial portions of the application, but the user remains the final decision-maker and reviewer.

The intended workflow is:

```text
Requirement
    ↓
AI Analysis
    ↓
Implementation Proposal
    ↓
Human Review
    ↓
AI Implementation
    ↓
Tests
    ↓
AI Code Review
    ↓
Human Review
    ↓
Commit / Pull Request
    ↓
CI/CD
```

### Rationale

AI can accelerate implementation but can also introduce:

* Incorrect assumptions
* Overengineering
* Security problems
* Hidden bugs
* Unnecessary dependencies
* Code that the developer does not understand

The goal is AI-assisted engineering, not blind code generation.

---

## D-061 — Implementation Should Be Incremental

**Status:** Accepted

### Decision

The project should be developed in small, verifiable slices rather than asking an AI coding agent to build the entire application at once.

### Rationale

Small changes are easier to:

* Review
* Test
* Debug
* Revert
* Understand
* Discuss in interviews

### Implications

Implementation work should generally be organized around small feature slices or PR-sized units.

---

## D-062 — Tests Are Part of Feature Completion

**Status:** Accepted

### Decision

A feature is not considered complete simply because its code works manually.

Feature completion should include appropriate:

* Unit tests
* Integration tests
* API tests
* End-to-end tests where appropriate
* Error-path testing

### Rationale

The project is intended to demonstrate real software engineering practices, not only UI implementation.

---

# 15. Documentation Decisions

## D-063 — Project Documentation Is Written in English

**Status:** Accepted

### Decision

The project's source-of-truth Markdown documentation is written in English.

### Rationale

The project is portfolio-facing and may be reviewed by:

* Recruiters
* Hiring managers
* Engineers
* Open-source-style collaborators
* AI coding tools

English documentation also makes it easier for tools such as Cursor to consume the project specifications consistently.

### Implications

Conversation and learning explanations may remain in Korean, but project artifacts should normally remain in English.

---

## D-064 — Documentation Is the Project Memory

**Status:** Accepted

### Decision

Important product and engineering decisions must be captured in project Markdown documents rather than relying solely on conversation history.

### Rationale

Long ChatGPT conversations are not a reliable engineering source of truth.

The project should remain understandable even when a new AI session or developer opens the repository.

### Core Documents

```text
PROJECT_INSTRUCTIONS.md
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

---

# 16. Deferred Architecture Decisions

The following areas should not be prematurely locked in this document.

## D-065 — Backend Architecture

**Status:** Deferred

The intended direction currently favors:

* C#
* ASP.NET Core
* PostgreSQL
* Entity Framework Core

However, detailed architecture should be finalized in `ARCHITECTURE.md`.

---

## D-066 — Frontend Architecture

**Status:** Deferred

The intended web direction currently favors:

* TypeScript
* Angular

Detailed frontend architecture, folder structure, state management, API integration, and component boundaries should be finalized in `ARCHITECTURE.md`.

---

## D-067 — Mobile Architecture

**Status:** Deferred

The intended mobile direction currently favors:

* .NET MAUI
* C#
* Android as the first target

Detailed mobile architecture should be decided after the core product and backend contracts are established.

---

## D-068 — Cloud Infrastructure

**Status:** Deferred

AWS is the intended cloud direction.

Specific services, deployment topology, networking, storage, secrets, observability, and cost controls should be documented in `ARCHITECTURE.md` and related deployment documentation.

---

## D-069 — Exact Performance Budgets

**Status:** Deferred

Performance is a priority, but exact numeric budgets should be established during implementation/testing based on realistic application behavior.

### Rationale

Premature arbitrary targets can be less useful than measuring the real application and establishing meaningful budgets.

---

# 17. Decision Principles

When future decisions are unclear, use these principles in order:

### 1. User value over technology showcase

A feature should exist because it improves the product, not merely because the technology is impressive.

### 2. Trust over AI novelty

If AI-generated information conflicts with reliable structured data, trustworthiness takes priority.

### 3. Performance over decoration

Animation should never make the application feel slower.

### 4. Simplicity before sophistication

Start with understandable systems and add complexity only when the product has evidence that it is needed.

### 5. Diversity over repetitive personalization

Personalization should help users discover relevant plants without trapping them in a narrow content bubble.

### 6. Explicit decisions over accidental behavior

If a behavior matters to the product, document it.

### 7. Human review over blind AI generation

AI tools can implement and critique, but important product and engineering decisions remain human-reviewed.

### 8. Reversible decisions should remain lightweight

Do not spend excessive effort formalizing decisions that can easily be changed later.

### 9. Irreversible or expensive decisions require stronger justification

Database structure, external data contracts, authentication architecture, cloud infrastructure, and public APIs should receive more deliberate review.

### 10. The product should feel coherent

New features should fit the established journey:

**Discover → Identify → Compare → Learn → Find → Save → Discover More**

---

# 18. Current Decision Summary

The current product can be summarized as:

> **Plant Encyclopedia is a fast, image-first plant discovery experience that combines source-backed plant information with AI-assisted image identification, while using personalization and subtle botanical interactions to create a distinctive but trustworthy consumer experience.**

The most important constraints are:

* Own DB is the user-facing source of truth.
* External data is normalized and validated.
* AI assists identification rather than becoming the authority.
* No fake confidence percentages.
* Identification shows three candidates plus More.
* Explore is discovery; Search is intent-driven.
* Category browsing remains category-pure.
* Personalized Explore preserves diversity.
* The same plant/cultivar should not appear twice in one recommendation feed.
* Plant Detail is shared across entry points.
* Where to Buy does not make unsupported inventory claims.
* Favorites require login.
* Mobile does not use persistent bottom navigation.
* Loading, error, and empty states are intentional product behavior.
* Accessibility and performance are first-class requirements.
* Decorative interactions must never interfere with core tasks.
* MVP scope must remain controlled.
* Product and engineering decisions must be documented so future AI sessions can continue the project consistently.

---

# 19. Decision Change Policy

A future change to an accepted decision should answer:

1. What changed?
2. Why is the current decision no longer appropriate?
3. What new evidence supports the change?
4. What existing product/engineering work will be affected?
5. Is the change worth the migration/rework cost?
6. Which documents must be updated?

When changing a major decision:

* Update this document.
* Update the affected product/UX/architecture document.
* Update `CURRENT_STATE.md`.
* Update `TODO.md` if implementation work changes.
* Do not leave contradictory requirements across documents.

This decision log should remain a concise record of the project's reasoning and should not become a duplicate copy of every product requirement.
