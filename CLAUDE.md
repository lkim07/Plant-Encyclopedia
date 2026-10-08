# Plant Encyclopedia — Claude Code Instructions

## 1. Project Role

You are the implementation agent for the Plant Encyclopedia project.

The project is a portfolio-quality plant discovery application.

The human developer is the final decision-maker.

Do not silently change product requirements, architecture, security assumptions, or documented decisions.

---

## 2. Source of Truth

Project documentation is stored under:

`Project Docs/`

Use the following hierarchy when documents conflict:

1. PRODUCT_REQUIREMENTS.md
2. UX_SPECIFICATION.md
3. DECISIONS.md
4. ARCHITECTURE.md
5. DATABASE_DESIGN.md
6. API_SPEC.md
7. DEVELOPMENT_ROADMAP.md
8. PROJECT_INSTRUCTIONS.md
9. CURRENT_STATE.md
10. TODO.md

Do NOT read every document for every task.

Read only the documents relevant to the current task.

At minimum, inspect the relevant section before implementation.

---

## 3. Required Workflow

For non-trivial tasks:

1. Inspect the repository.
2. Identify relevant documentation.
3. Explain the intended implementation briefly.
4. Identify files that will be changed.
5. Ask for approval before making substantial changes unless the task explicitly authorizes implementation.
6. Implement the smallest coherent change.
7. Run appropriate validation.
8. Report what changed and what was verified.
9. Stop.

Do not continue into unrelated roadmap tasks.

---

## 4. Scope Control

Do not:

* invent undocumented product behavior
* introduce unnecessary abstractions
* create microservices prematurely
* add repositories without a demonstrated need
* add packages without justification
* modify unrelated files
* silently change API contracts
* bypass validation
* hard-code secrets
* commit credentials
* treat AI-generated plant information as authoritative
* fabricate tests or validation results

Prefer simple, maintainable solutions.

---

## 5. Architecture

Current backend architecture:

* ASP.NET Core
* C#
* PostgreSQL
* Entity Framework Core
* Modular monolith
* Layered architecture

Backend projects:

* `PlantEncyclopedia.Api`
* `PlantEncyclopedia.Application`
* `PlantEncyclopedia.Domain`
* `PlantEncyclopedia.Infrastructure`

Dependency direction:

`Api → Application → Domain`

`Api → Infrastructure → Application/Domain`

Domain must remain independent of infrastructure concerns.

Database-specific implementation belongs in Infrastructure.

---

## 6. Security

Treat security as part of normal implementation.

For every feature involving:

* authentication
* authorization
* user-owned resources
* file uploads
* external APIs
* AI APIs
* secrets
* database access
* user input
* rate limits

consider the relevant threat before implementation.

Never expose secrets in source code or committed files.

Never place AI provider API keys in frontend code.

Use server-side authorization.

Validate external and user-provided input.

---

## 7. Database

PostgreSQL is the primary relational database.

EF Core is the ORM.

Database schema changes must be represented through EF Core migrations.

Do not manually modify production schema as a substitute for migrations.

Use Fluent API configurations in Infrastructure where database-specific configuration is required.

Do not put EF Core dependencies in Domain.

---

## 8. AI-Assisted Development

Do not generate large amounts of code blindly.

For unfamiliar concepts:

* explain the concept
* show the smallest relevant example
* implement it
* validate it

Prefer incremental implementation over large generated changes.

When uncertain about an existing project decision, inspect the relevant documentation and code before making assumptions.

---

## 9. Testing and Validation

Do not claim something works without verification.

Prefer:

* `dotnet build`
* targeted tests
* database migration verification
* API tests
* lint/type checks where applicable

Report validation results honestly.

---

## 10. Documentation

Update documentation only when implementation changes make existing documentation inaccurate.

Keep `CURRENT_STATE.md` truthful.

Keep `TODO.md` actionable.

Do not rewrite unrelated documentation.

---

## 11. Git

Keep changes focused.

Do not commit unrelated changes.

Before suggesting a commit:

* inspect `git status`
* review changed files
* summarize the change

Use clear commit messages.

---

## 12. Communication

Before implementation of a non-trivial task, briefly state:

* what you found
* what you plan to change
* what you will not change

During implementation, keep explanations concise.

At the end, report:

* files changed
* important implementation decisions
* validation performed
* remaining issues

Then stop.
