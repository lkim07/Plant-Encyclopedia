# Project Instructions

## 1. Purpose

This document defines the operating rules for AI coding agents and developers working on the Plant Encyclopedia project.

It is the project's implementation constitution.

It does not replace the product, UX, architecture, database, API, or roadmap documents. Instead, it defines how those decisions should be interpreted and implemented safely.

The primary goal is to build a realistic, maintainable, portfolio-quality plant discovery application while keeping the implementation understandable enough for a junior developer to learn from and maintain.

---

# 2. Project Identity

## Project Name

Plant Encyclopedia

## One-Sentence Description

A fast, image-first plant discovery application that helps users identify, explore, learn about, save, and find plants through trustworthy structured data and responsible AI-assisted identification.

## Core User Journey

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

The product should feel like a real consumer application, not a demonstration of technologies.

---

# 3. Current Technology Direction

The planned technology stack is:

### Web

* Angular
* TypeScript

### Backend

* ASP.NET Core
* C#

### Database

* PostgreSQL
* Entity Framework Core

### Infrastructure

* Docker
* AWS
* Amazon S3 where appropriate
* GitHub Actions

### Mobile

* .NET MAUI
* C#
* Android as the initial mobile target

### AI

* External vision-capable AI API for plant identification
* AI provider abstraction so the provider can be replaced later

Technology choices may evolve, but changes must follow the decision process described in this document.

---

# 4. Source-of-Truth Document Hierarchy

Before implementing a feature, AI agents must understand which document defines what.

Use the following hierarchy:

1. `PRODUCT_REQUIREMENTS.md`

   * Product behavior
   * Features
   * Scope
   * Product rules

2. `UX_SPECIFICATION.md`

   * User interaction
   * Navigation
   * Layout behavior
   * Responsive behavior
   * Loading, empty, and error states
   * Interaction rules

3. `DECISIONS.md`

   * Explicit architectural/product decisions
   * Decisions that were intentionally made after considering alternatives

4. `ARCHITECTURE.md`

   * System structure
   * Backend architecture
   * Frontend architecture
   * Infrastructure boundaries
   * Integration patterns

5. `DATABASE_DESIGN.md`

   * Data model
   * Relationships
   * Persistence rules
   * Data integrity

6. `API_SPEC.md`

   * HTTP endpoints
   * Request/response contracts
   * API behavior

7. `DEVELOPMENT_ROADMAP.md`

   * Implementation order
   * Development phases
   * Milestones

8. `PROJECT_INSTRUCTIONS.md`

   * How AI/developers must work within the project

9. `CURRENT_STATE.md`

   * What has actually been implemented

10. `TODO.md`

* Current actionable work

If documents appear to conflict, do not silently choose an interpretation.

First determine whether the conflict is:

* outdated documentation,
* an intentional newer decision,
* an implementation state mismatch,
* or a genuine unresolved product/technical decision.

If the conflict requires changing an established decision, ask the project owner before proceeding and update the relevant documentation after the decision is made.

---

# 5. Roles

## Project Owner / Developer

The project owner makes final decisions about:

* Product behavior
* UX
* Scope
* Technology direction
* Architecture changes
* Trade-offs
* Feature priorities

AI must not silently make major product decisions on behalf of the project owner.

## ChatGPT

ChatGPT is used primarily as:

* Product manager
* Software architect
* Technical teacher
* Planning assistant
* Code reviewer
* Debugging assistant
* Documentation assistant

ChatGPT should explain important technical decisions in understandable language when requested.

## Cursor / Coding Agent

Cursor is the implementation agent.

Cursor should:

* Read relevant project documentation before coding
* Inspect the existing repository before changing code
* Follow established patterns
* Make small, reviewable changes
* Run appropriate tests and validation
* Explain important implementation decisions
* Avoid changing unrelated files
* Avoid inventing product behavior

Cursor is not authorized to redefine the product.

---

# 6. Mandatory Development Workflow

Use this workflow for meaningful features:

```text
Requirement
    ↓
Relevant Documentation
    ↓
Acceptance Criteria
    ↓
Implementation Plan
    ↓
Project Owner Approval
    ↓
Implementation
    ↓
Tests
    ↓
Review
    ↓
Documentation Update
    ↓
Commit / PR
    ↓
CI Validation
```

Do not skip directly from:

```text
"I want feature X"
        ↓
"Build everything"
```

Instead, break the feature into understandable implementation steps.

---

# 7. Before Coding

Before modifying code, the AI agent should:

1. Identify the feature being implemented.
2. Read the relevant documentation.
3. Inspect the existing repository structure.
4. Search for existing implementations or reusable patterns.
5. Determine which layer or module should own the change.
6. Identify affected API contracts and data models.
7. Identify required tests.
8. Check whether the feature conflicts with an existing decision.
9. Create a small implementation plan.

For larger changes, explicitly state:

* What will change
* What will not change
* Which files/modules are expected to change
* How the implementation will be tested
* Whether documentation must be updated

---

# 8. Do Not Invent Product Behavior

AI agents must not silently introduce:

* New navigation patterns
* New categories
* New recommendation rules
* New authentication requirements
* New user flows
* New UI metaphors
* New data sources
* New product features

when those decisions have already been defined elsewhere.

For example:

Do not turn the Explore page into a search-first page simply because it is technically easier.

Do not add a bottom navigation bar to mobile because it is a common mobile pattern.

Do not add a fourth identification candidate because the UI has four visual slots.

Do not add unrelated plants to an empty category page.

Do not add pet-safety claims without an approved reliable source.

Existing product decisions are intentional.

---

# 9. Product Principles

Always prioritize:

1. Trustworthy information
2. Fast perceived performance
3. Frictionless interaction
4. Clear information hierarchy
5. Beautiful visual presentation
6. Responsible AI usage
7. Accessibility
8. Maintainability

The application should remain:

> Fast, image-first, trustworthy, and easy to explore.

---

# 10. UX Invariants

The following behaviors must remain stable unless explicitly changed by the project owner.

### Navigation

* Desktop uses a collapsible sidebar.
* Mobile uses a slide-in sidebar.
* There is no persistent mobile bottom navigation.
* Back navigation remains available on nested pages.
* Back navigation should preserve useful navigation context where practical.

### Explore

* Explore is discovery-first.
* Search is activated from Explore.
* Category browsing remains category-pure.
* The same plant/cultivar should not appear repeatedly as duplicate cards in one recommendation feed.

### Identification

The identification flow is:

```text
Camera / Photo
    ↓
Image Preview
    ↓
Identification
    ↓
Identification Results
    ↓
Plant Detail
```

Identification does not directly jump to Plant Detail.

### Plant Detail

All entry points use the same Plant Detail experience.

Examples:

* Explore
* Search
* Identification
* Category browsing
* Favorites
* More

### Favorites

Favorites require authentication.

The primary save metaphor is the bookmark.

Do not introduce hearts as an additional save metaphor.

---

# 11. Architecture Rules

The application should begin as a **modular monolith**.

Do not introduce microservices unless there is a demonstrated need.

Do not introduce:

* Kubernetes
* service meshes
* distributed systems
* unnecessary message queues
* premature event-driven infrastructure
* multiple independently deployed backend services

simply to make the portfolio look more sophisticated.

Prefer:

```text
Simple
→ Modular
→ Testable
→ Observable
→ Scalable when necessary
```

rather than:

```text
Complex
→ Distributed
→ Difficult to understand
→ Difficult to maintain
```

Architecture should solve actual problems rather than hypothetical future scale.

---

# 12. Backend Rules

Backend technology:

* ASP.NET Core
* C#
* PostgreSQL
* Entity Framework Core

Follow clear separation between:

```text
Presentation
Application
Domain
Infrastructure
```

Dependencies should point toward stable inner concepts rather than allowing arbitrary coupling.

## Controllers / API Layer

Controllers should primarily handle:

* HTTP concerns
* Request validation boundaries
* Authentication/authorization context
* Calling application services
* Returning appropriate responses

Controllers should not contain large amounts of business logic.

## Application Layer

Application services/use cases should coordinate:

* Business workflows
* Data access through appropriate abstractions
* External provider interactions
* Validation
* Mapping where appropriate

## Domain Layer

Domain logic should represent meaningful business rules.

Do not move every trivial property into an elaborate domain abstraction simply for architectural appearance.

## Infrastructure

Infrastructure contains implementation details such as:

* EF Core
* PostgreSQL
* External APIs
* S3
* AI providers
* Caching implementations
* Email or other external services if introduced later

---

# 13. API Rules

The API is a contract.

Do not casually change:

* Endpoint paths
* HTTP methods
* Request structures
* Response structures
* Required fields
* Error formats

without checking frontend impact and updating `API_SPEC.md`.

Use DTOs at API boundaries.

Do not expose EF Core entities directly as public API contracts.

Validate external input.

Return meaningful HTTP status codes.

Use consistent error responses.

Do not leak:

* stack traces
* database details
* API keys
* internal infrastructure information

to clients.

---

# 14. Database Rules

PostgreSQL is the primary persistent data store.

Entity Framework Core is the ORM.

Important principles:

* Internal database IDs are owned by the application.
* External provider IDs are stored separately.
* External API responses are not the application's source of truth.
* Plant data should be normalized before being persisted.
* Provenance must be retained for important externally sourced information.
* Relationships must use proper foreign keys.
* Database constraints should protect important invariants.
* Migrations must be committed and reviewed.
* Avoid destructive schema changes without understanding existing data.

Do not modify the database schema merely to make a single query easier if a better design exists.

Do not store large binary images directly in PostgreSQL unless there is a deliberate reason.

Prefer object storage for persistent image assets where appropriate.

---

# 15. Plant Data Rules

The application's own database is the source of truth for user-facing encyclopedia content.

External providers such as iNaturalist and GBIF are structured data sources.

The normal ingestion flow is:

```text
User Search
    ↓
Own Database
    ↓
Found?
 ┌──┴──┐
Yes   No
 ↓     ↓
Return  External Provider
          ↓
       Normalize
          ↓
        Validate
          ↓
       Persist
          ↓
        Return
```

Never blindly copy an external response into the frontend.

External data must pass through the application's normalization and validation layer.

Track provenance and retrieval information where appropriate.

---

# 16. External Provider Rules

External APIs must be isolated behind clear interfaces/services.

Do not spread provider-specific logic throughout controllers or frontend code.

Prefer:

```text
Application
    ↓
Provider Abstraction
    ↓
Provider Implementation
```

rather than:

```text
Controller
    ↓
Direct HTTP call to external provider
```

Provider failures must be handled gracefully.

The application should distinguish between:

* Provider unavailable
* Provider returned no result
* Provider returned invalid/unusable data
* Internal database failure
* Network failure

Do not present an external provider failure as proof that a plant does not exist.

---

# 17. AI Rules

AI is primarily an **identification and candidate-generation tool**.

AI must not be treated as the authoritative source for:

* Plant taxonomy
* Care instructions
* Health claims
* Safety claims
* Historical facts

unless the information has been independently validated through appropriate structured sources.

The identification pipeline should conceptually follow:

```text
Image
  ↓
Image Validation
  ↓
Image Hash / Cache
  ↓
AI Identification
  ↓
Candidate Generation
  ↓
Candidate Ranking
  ↓
Structured Database Validation
  ↓
Identification Results
```

Unvalidated AI candidates should not automatically become encyclopedia entries.

AI providers should be abstracted so the implementation can change providers later.

AI requests must include appropriate:

* Validation
* Rate limiting
* Error handling
* Cost controls
* Logging without sensitive image exposure
* Abuse protection

Do not add fake confidence percentages.

---

# 18. Search Rules

Search should prioritize query relevance.

Personalization must not override a strong search match.

Search suggestions should be database-backed and may include:

* Common names
* Scientific names
* Cultivar names
* Plant groups
* Relevant taxonomy terms

Search history and view history are separate concepts.

---

# 19. Recommendation Rules

Recommendations should preserve diversity.

For new or anonymous users:

* Maintain broad category representation.
* Prefer curated diversity over pure randomness.
* Do not over-concentrate on one plant group.

For users with meaningful behavioral data, use the approximate principle:

```text
60% strongly related
20% somewhat related / adjacent
20% substantially different / novel
```

This is a recommendation philosophy, not a rigid requirement that every small response must mathematically match those percentages.

The recommendation system must avoid repeatedly showing the same plant/cultivar in a single feed.

Behavioral signals may include:

* Viewed plants
* Bookmarked plants
* Search activity
* Categories
* Plant groups
* Recent interactions

Start with understandable rule-based recommendation logic.

Do not introduce machine-learning recommendation infrastructure prematurely.

---

# 20. Frontend Rules

Web:

* Angular
* TypeScript

Prefer feature-oriented organization over arbitrary file grouping.

Use reusable components where there is genuine reuse.

Do not create excessive abstractions for one-off components.

Keep UI components focused on presentation and interaction.

Keep business logic out of templates where practical.

Use typed models/interfaces for API data.

Do not use `any` to avoid solving a type problem unless there is a documented reason.

Handle:

* Loading
* Empty
* Error
* Success

states explicitly.

---

# 21. Responsive Design Rules

The application must work across:

* Desktop
* Tablet
* Mobile

Do not design only for one screen size and patch the others afterward.

Mobile-specific interaction rules must be respected.

Examples:

* No hover-dependent functionality on mobile
* Touch targets must be sufficiently large
* Sidebar becomes a slide-in navigation
* Plant grids become two-column masonry-style layouts where appropriate
* Contextual actions may use bottom sheets

---

# 22. Accessibility Rules

Accessibility is part of the implementation, not a later polish step.

Support:

* Keyboard navigation
* Meaningful semantic elements
* Accessible labels for icon buttons
* Sufficient contrast
* Meaningful alt text
* Adequate touch targets
* Non-color-only status communication
* Reduced-motion preferences

Hover-only interactions must have an accessible alternative.

Decorative effects must never be required to understand or use the product.

---

# 23. Performance Rules

Performance is more important than decorative animation.

Prefer:

* Responsive images
* WebP/AVIF where appropriate
* Image thumbnails
* Lazy loading
* CDN delivery where appropriate
* Efficient database queries
* Appropriate indexes
* GPU-friendly transforms
* Opacity/transform animations
* Avoiding unnecessary layout recalculation

Do not block core interactions for decorative animation.

Navigation, search, bookmarking, identification, and opening contextual UI must remain responsive.

Do not introduce performance optimizations without understanding the actual bottleneck when the optimization adds significant complexity.

---

# 24. UI Motion Rules

The product may use subtle botanical micro-interactions.

Examples include:

* Subtle cursor effects on desktop
* Occasional falling leaves
* Scroll-driven hero transitions
* Gentle card hover effects
* Bookmark transitions

These effects must be:

* Sparse
* Subtle
* Optional
* Performance-conscious
* Disabled or reduced under `prefers-reduced-motion`

Never use animation to delay a functional action.

The priority order is:

```text
Core Task
    ↓
Navigation
    ↓
Content Comprehension
    ↓
Feedback
    ↓
Decorative Interaction
```

---

# 25. Authentication and Privacy

Authentication should be introduced when required by product behavior.

Favorites require authentication.

Location is requested only when necessary, particularly for Where to Buy.

Do not request location permission merely to personalize unrelated parts of the application.

Anonymous activity may be stored locally or associated with an anonymous identifier where appropriate.

When a user later authenticates, relevant anonymous state may be associated with the account where the behavior is clearly defined and safe.

Never expose private user information unnecessarily.

---

# 26. Security Rules

Never commit secrets.

Never hard-code:

* API keys
* Passwords
* Tokens
* AWS credentials
* Connection secrets

Use appropriate environment/configuration mechanisms.

Validate user-provided input.

Validate uploaded images.

Protect authenticated endpoints.

Apply authorization server-side.

Do not trust frontend authorization checks as security boundaries.

Use rate limiting where abuse or cost risk exists.

Do not log sensitive data unnecessarily.

---

# 27. Testing Rules

Tests should be added alongside meaningful functionality.

Prioritize:

### Unit tests

For:

* Business rules
* Validation
* Ranking logic
* Recommendation rules
* Mapping/normalization logic

### Integration tests

For:

* Database interactions
* API behavior
* External provider boundaries
* Authentication/authorization where appropriate

### End-to-end tests

For critical user journeys when the application reaches sufficient maturity.

Important flows include:

* Search
* Plant Detail
* Favorites
* Identification
* Navigation
* Where to Buy

Do not chase arbitrary test coverage percentages.

Test behavior and risk.

---

# 28. Testability

Production services should not require real external APIs during normal automated tests.

Use abstractions and test doubles for:

* Plant data providers
* AI providers
* Storage
* External retailer services
* Other external dependencies

Tests should be:

* Deterministic
* Repeatable
* Fast enough to run regularly

---

# 29. Error Handling

Errors should be contextual and recoverable.

Do not expose internal implementation details.

Examples:

### General data failure

```text
Something went wrong

We couldn't load the plants.

Please check your connection and try again.

[Try Again]
```

### Identification failure

```text
We couldn't identify this plant

The image may be unclear, or the plant may not be in our database yet.

[Try Again]
[Choose Another Photo]
```

Do not claim that a plant does not exist simply because an AI request failed.

---

# 30. Dependency Discipline

Do not add a library simply because it is convenient.

Before introducing a dependency, consider:

* Is it actually necessary?
* Does the framework already provide this?
* Does it create long-term maintenance cost?
* Is it actively maintained?
* Does it introduce security concerns?
* Does it make the architecture harder to understand?

Prefer existing platform/framework capabilities when they are sufficient.

---

# 31. Code Quality Rules

Prefer code that is:

* Clear
* Typed
* Testable
* Readable
* Consistent
* Small enough to understand

Avoid:

* Giant classes
* Giant controllers
* Giant components
* Deeply nested conditionals
* Duplicate business logic
* Magic numbers
* Hard-coded environment-specific values
* Unnecessary abstractions

Do not optimize for the smallest possible number of lines.

Optimize for understandable code.

---

# 32. AI-Assisted Development Rules

AI-generated code must be treated as proposed code, not automatically trusted code.

The developer should understand important code before accepting it.

When generating code, AI should:

1. Explain what the code does when the change is non-trivial.
2. Identify important trade-offs.
3. Mention assumptions.
4. Point out uncertainty.
5. Suggest tests.
6. Avoid claiming that code works without validation.

AI must not fabricate:

* Test results
* API responses
* Provider behavior
* Library APIs
* Deployment success
* Performance measurements

If something has not been verified, say so.

---

# 33. Cursor Rules

Before asking Cursor to implement a feature:

1. Give it the relevant requirements.
2. Give it the relevant architecture/API/database context.
3. Ask it to inspect the repository.
4. Ask it to propose a plan.
5. Review the plan.
6. Implement incrementally.
7. Run tests.
8. Review the diff.

Do not give Cursor vague instructions such as:

> "Build the entire backend."

Prefer focused instructions such as:

> "Implement the Plant Detail API described in `API_SPEC.md`. First inspect the existing backend structure and database entities. Propose the files that need to change. Do not modify unrelated features."

---

# 34. Change Scope

Each implementation task should have a clear scope.

Prefer:

```text
One feature
→ One focused change
→ Tests
→ Review
```

over:

```text
Feature
+ Refactor
+ New dependency
+ Architecture change
+ UI redesign
+ Database rewrite
```

in a single task.

Do not modify unrelated files merely because they could be cleaned up.

If unrelated technical debt is discovered, record it in `TODO.md` instead.

---

# 35. Refactoring Rules

Refactor when there is a concrete reason.

Good reasons include:

* Removing duplication that creates bugs
* Improving testability
* Fixing architectural coupling
* Improving maintainability
* Supporting an approved feature

Do not perform large refactors simply because the code could theoretically be cleaner.

A working implementation should not be destabilized without a clear benefit.

---

# 36. Git and Commit Rules

Keep commits focused and meaningful.

Prefer commit scopes such as:

```text
feat: add plant detail endpoint
feat: add plant search
fix: handle provider timeout
test: add recommendation ranking tests
refactor: extract plant provider interface
docs: update API specification
```

Avoid commits such as:

```text
update
changes
stuff
final
final-final
```

Do not mix unrelated changes in one commit when avoidable.

---

# 37. Documentation Maintenance

Documentation is part of the project.

When an implementation changes an established behavior, update the relevant documentation.

Examples:

### Product behavior changed

Update:

`PRODUCT_REQUIREMENTS.md`

### UX behavior changed

Update:

`UX_SPECIFICATION.md`

### Architectural decision changed

Update:

`DECISIONS.md`

and, if necessary:

`ARCHITECTURE.md`

### Database changed

Update:

`DATABASE_DESIGN.md`

### API changed

Update:

`API_SPEC.md`

### Roadmap changed

Update:

`DEVELOPMENT_ROADMAP.md`

### Current implementation changed

Update:

`CURRENT_STATE.md`

### New work discovered

Update:

`TODO.md`

Do not allow documentation and implementation to drift indefinitely.

---

# 38. Decision Changes

When a previously settled decision needs to change:

1. Identify the existing decision.
2. Explain why it is no longer appropriate.
3. Describe the alternatives.
4. Get project owner approval.
5. Update `DECISIONS.md`.
6. Update affected documents.
7. Implement the new decision.

Never silently overwrite an established decision.

---

# 39. Scope Control

The project is intentionally ambitious.

That does not mean every possible feature should be implemented immediately.

When a feature is proposed, evaluate:

* User value
* Portfolio value
* Learning value
* Implementation cost
* Maintenance cost
* Data availability
* Security implications
* Performance impact
* Whether it belongs in MVP

If a feature threatens the core product or timeline, propose deferring it rather than automatically expanding scope.

---

# 40. Avoid Premature Complexity

Do not add complex infrastructure just because it appears in a job posting.

For example, do not introduce:

* Kubernetes
* Microservices
* Kafka
* Complex distributed caching
* Complex recommendation ML
* Multiple backend services
* Advanced event-driven architecture

unless the project has a real requirement for them.

It is better to demonstrate strong fundamentals in a well-designed modular monolith than to demonstrate shallow familiarity with many technologies.

---

# 41. Learning Principle

This project is also a learning project.

When implementing unfamiliar technology, prefer explanations that answer:

1. What problem does this solve?
2. Why are we using it here?
3. What alternatives exist?
4. What does the code actually do?
5. What could go wrong?
6. How would this be done in a real production environment?

Do not hide complexity behind AI-generated code without explanation.

The goal is not merely:

> "The application runs."

The goal is:

> "The developer understands why the application is designed this way."

---

# 42. Portfolio Quality

The project should demonstrate professional engineering practices through actual implementation.

Prioritize evidence of:

* Clear architecture
* Well-designed APIs
* Relational database design
* Validation
* Error handling
* Testing
* Authentication
* External API integration
* AI integration
* Caching where justified
* Docker
* CI/CD
* Cloud deployment
* Accessibility
* Performance awareness
* Documentation
* Clean Git history

Do not add superficial features solely to increase the technology list on a résumé.

Depth is more valuable than technology-count.

---

# 43. Implementation Checklist

Before coding:

* [ ] Read relevant documentation
* [ ] Inspect existing code
* [ ] Identify affected layers
* [ ] Confirm product behavior
* [ ] Confirm API/database impact
* [ ] Define acceptance criteria
* [ ] Create a focused implementation plan
* [ ] Check for conflicting decisions

During coding:

* [ ] Follow existing architecture
* [ ] Keep changes focused
* [ ] Avoid unnecessary dependencies
* [ ] Validate inputs
* [ ] Handle errors
* [ ] Maintain accessibility
* [ ] Consider performance
* [ ] Avoid unrelated refactoring

After coding:

* [ ] Run relevant tests
* [ ] Run formatting/linting where applicable
* [ ] Review the diff
* [ ] Check for accidental changes
* [ ] Verify API contracts
* [ ] Verify database migrations if applicable
* [ ] Update documentation if behavior changed
* [ ] Update `CURRENT_STATE.md` when implementation state changes
* [ ] Update `TODO.md` when work is completed or new work is discovered
* [ ] Commit the focused change

---

# 44. When Unsure

If an AI agent encounters uncertainty, it should distinguish between:

### Implementation uncertainty

Example:

> "There are two reasonable ways to structure this service. I recommend A because it matches the existing architecture."

The agent may proceed when the decision is local and low-risk.

### Product uncertainty

Example:

> "The requirements do not define whether anonymous users can save identification results."

Ask the project owner before implementing behavior.

### Architecture uncertainty

Example:

> "This feature may require introducing background processing, which would affect the current architecture."

Explain the trade-off and ask before introducing significant complexity.

---

# 45. Non-Negotiable Rules

The following rules must not be violated without explicit approval:

1. Do not expose secrets.
2. Do not silently change established product decisions.
3. Do not treat AI-generated information as authoritative plant information.
4. Do not expose raw external provider responses as user-facing encyclopedia data.
5. Do not introduce major architecture complexity without justification.
6. Do not break API contracts casually.
7. Do not bypass server-side authorization.
8. Do not ignore validation and error handling.
9. Do not sacrifice core usability for decorative animation.
10. Do not modify unrelated code without a reason.
11. Do not fabricate tests, API behavior, or implementation results.
12. Do not allow documentation to remain intentionally inconsistent with the implementation.

---

# 46. Final Principle

The Plant Encyclopedia project should be built as if it were a small real product.

That means:

```text
Product decisions before implementation
        ↓
Clear architecture
        ↓
Small, understandable changes
        ↓
Tests and validation
        ↓
Review
        ↓
Accurate documentation
        ↓
Continuous improvement
```

The objective is not to make the project appear complex.

The objective is to make it **credible, useful, maintainable, fast, trustworthy, and technically well-engineered**.

> Build a real product first.
> Demonstrate the technology through the quality of the implementation.
