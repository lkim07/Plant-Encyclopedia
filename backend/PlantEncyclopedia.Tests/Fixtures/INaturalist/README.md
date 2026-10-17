# Recorded iNaturalist responses

Real responses from the iNaturalist API, recorded once so the image tests run offline
(`ExternalData/PlantImageTests`). Tests never call iNaturalist. Do not re-record without approval: the bodies
are evidence of how the API answered.

- Recorded: 2026-10-17, 09:43 UTC. Read-only `GET` requests, made once each, approved by the developer.
- User-Agent: `PlantEncyclopedia/0.1 (+https://github.com/lkim07/Plant-Encyclopedia)`, `Accept: application/json`
- API documentation (swagger): stay at or under 60 requests per minute and 10,000 per day; the API is for
  application development, not scraping; use is subject to the iNaturalist Terms of Service.

| File | Request | HTTP | Result |
|---|---|---|---|
| `taxa-search-rosa-canina.json` | `GET /v1/taxa?q=Rosa%20canina&is_active=true&per_page=10` | 200 | 12 results; `55884` Rosa canina (species, Plantae), plus a section, a subsection, a mollusc ("Ciprea rosa canina") and hybrids |
| `taxa-search-mentha-x-piperita.json` | `GET /v1/taxa?q=Mentha%20%C3%97%20piperita&is_active=true&per_page=10` | 200 | 5 results; `181525` Mentha × piperita (rank **hybrid**) and four forms |
| `taxon-55884-rosa-canina.json` | `GET /v1/taxa/55884` | 200 | 11 taxon photos |
| `taxon-181525-mentha-x-piperita.json` | `GET /v1/taxa/181525` | 200 | 6 taxon photos |

What the recordings showed:

- Search results only contain the name: an exact, active, Plantae match must be picked (rank species,
  hybrid, subspecies or variety; peppermint is a hybrid).
- `taxon_photos` (details only) are in curator order, each with `license_code`, `attribution`,
  `attribution_name`, `original_url` and `original_dimensions`. The default photo is the first one.
- Rosa canina photo licences, in order: CC BY-NC-ND, CC BY-NC, **CC BY-SA** (first usable), CC BY, CC BY-NC,
  all rights reserved, CC0, CC BY, CC BY-NC-SA, all rights reserved, CC BY-NC.
- Mentha × piperita: CC BY-NC, CC BY-NC-SA, CC BY-NC, CC BY-NC, CC BY-NC-ND, CC BY-NC: none usable commercially.
- Openly licensed photos are on `inaturalist-open-data.s3.amazonaws.com`; "all rights reserved" ones
  (`license_code: null`) on `static.inaturalist.org`. No photo page URL is given (`native_page_url: null`).
- Licence codes carry no version (`cc-by-sa`).
