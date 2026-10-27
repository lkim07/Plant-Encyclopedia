# Plant-Encyclopedia

**Snap a photo of a plant, find out what it is, and discover more plants like it.**

I often walk around my neighborhood admiring front yards and wondering what those plants are —
and where I could buy them. Plant Encyclopedia is my answer: a photo-first plant discovery app with
exploring photos of plants like Pinterest for plants, built on verified botanical data rather than AI guesses.

 ---

## What it does

- **Identify a plant from a photo.** Take a photo or choose one, preview it, and get up to three
  ranked matches. Each match is a real plant from the encyclopedia, never an unverified AI guess,
  and the app shows no fake confidence percentages.
- **Explore.** An image-first feed that keeps categories and plant groups varied. Once you're
  signed in, it adapts to your interests: roughly 60% closely related plants, 20% nearby ones and
  20% something new, so you don't get stuck in a bubble.
- **Search.** Common, scientific and cultivar names, with accent- and typo-tolerant matching and
  suggestions.
- **Favorites.** Save plants with a bookmark, undo a removal, and rearrange them by drag-and-drop
  (with keyboard-accessible move buttons too).
- **Accounts.** Email and password, or sign in with Google or GitHub.
- **Plant detail.** Names, taxonomy, care information from sources, and photos with credits.


**Planned:** Where to Buy (nearby nurseries and online shops) and a "More like this" page after
identification.


## Design principles

- **AI suggests, data decides.** Identification candidates are only shown if they match a plant
  already in the encyclopedia. AI output never becomes plant facts or care advice.
- **Trustworthy sources.** Taxonomy comes from Catalogue of Life (through GBIF). Images come from
  iNaturalist and Wikimedia, and only openly licensed ones are used, with credits shown.
- **Privacy by default.** Uploaded photos are processed in memory, re-encoded without location
  metadata, and never stored.


## Tech stack

| Layer    | Technology |
|----------|------------|
| Frontend | Angular 22, TypeScript, Angular CDK (drag-and-drop), GSAP |
| Backend  | ASP.NET Core (.NET 10), C#, a layered modular monolith (Api → Application → Domain, Infrastructure) |
| Database | PostgreSQL 16, Entity Framework Core 10 (migrations, trigram and unaccent search) |
| Storage  | Cloudflare R2 for plant images (WebP in three sizes) |
| External | Pl@ntNet (identification), GBIF / Catalogue of Life, ChecklistBank, iNaturalist, Wikimedia |
| Testing  | xUnit + Testcontainers (real PostgreSQL), Vitest |

## Engineering highlights
- **Security:** ASP.NET Core Identity with an HttpOnly session cookie, CSRF protection, account
  lockout, per-user ownership checks, and rate limits on expensive or abusable endpoints.
- **Cost control for paid and external APIs:** app-wide call budgets, caching, timeouts, and
  graceful "try again" states instead of crashes.
- **Safe image uploads:** the file type is checked from its bytes, the size from its header, and
  large photos are decoded at reduced scale.
- **Rule-based recommendations:** a transparent scoring model with recency weighting, and stable
  paging, so infinite scroll never repeats or skips plants.
- **Accessibility:** keyboard alternatives to drag-and-drop, screen-reader announcements and
  reduced-motion support.
- **Testing:** about 500 backend tests and 125 frontend tests. Tests never call outside services:
  they use fake providers and saved or constructed sample responses.

## How I built it
I built this project with **Claude Code** as an AI pair programmer. I set the product
requirements, made the architecture decisions (recorded in `Project Docs/`), reviewed each change,
and worked in small steps: plan → approve → implement → test → document. A big part of the
learning was understanding and questioning the generated code, not just accepting it.





## Running locally

```bash
# Backend (needs Docker for PostgreSQL)
cd backend
dotnet run --project PlantEncyclopedia.Api --launch-profile https
# Frontend
cd frontend
npx ng serve    # then open http://localhost:4200

Secrets (the database connection, OAuth keys, the Pl@ntNet key) go in .NET User Secrets, never in
the repository.

Status and roadmap
- [x] Encyclopedia, search, Explore, accounts, Favorites, history, personalized feed
- [x] Photo identification (backend and screens)
- [ ] "More like this" after identification
- [ ] Where to Buy
- [ ] Deployment, CI/CD and a mobile app (.NET MAUI)

## Future features

- **Manual image review.** Plant photos and illustrations are chosen automatically from openly licensed sources
  (iNaturalist, Wikipedia, Wikimedia Commons). The automatic choice checks licences, credits, file type and size,
  but cannot see what a picture shows, so it sometimes picks seeds, a microscope slide or a product photo instead
  of the plant. A review screen would list a plant's acceptable candidates with thumbnails and let an authorized
  user choose the one to show. Today a wrong image can only be rejected, in development, so that the next
  candidate is chosen automatically. Tracked in `Project Docs/10_TODO.md` (Phase 11).



