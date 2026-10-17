# Recorded GBIF responses

Real responses from the GBIF API, recorded once so the provider tests run offline
(`ExternalData/GbifPlantDataProviderTests`, `ExternalData/PlantIngestionTests`).
Tests never call GBIF. Do not re-record without approval: the bodies are evidence of how the API answered.

- Recorded: 2026-10-16, 10:17 UTC. Read-only `GET` requests, made once each.
- User-Agent: `PlantEncyclopedia/0.1 (+https://github.com/lkim07/Plant-Encyclopedia)`
- Checklist: Catalogue of Life eXtended Release (COL XR), `checklistKey=7ddf754f-d193-4cc9-b351-99906754a03b`
- URL: `https://api.gbif.org/v2/species/match?scientificName=<name>&checklistKey=7ddf754f-d193-4cc9-b351-99906754a03b[&kingdom=Plantae]`

| File | `scientificName` | `kingdom` | HTTP | `matchType` | Usage (status, rank) |
|---|---|---|---|---|---|
| `match-rosa-canina.json` | `Rosa canina` | `Plantae` | 200 | EXACT | `4TCZM` Rosa canina (ACCEPTED, SPECIES) |
| `match-rosa-caninna-fuzzy.json` | `Rosa caninna` | `Plantae` | 200 | VARIANT | `4TCZM` Rosa canina (ACCEPTED, SPECIES) |
| `match-rosa-genus.json` | `Rosa` | `Plantae` | 200 | EXACT | `63S6D` Rosa (ACCEPTED, GENUS) |
| `match-homo-sapiens-plantae.json` | `Homo sapiens` | `Plantae` | 200 | HIGHERRANK | `P` Plantae (ACCEPTED, KINGDOM) |
| `match-homo-sapiens.json` | `Homo sapiens` | — | 200 | EXACT | `6MB3T` Homo sapiens (ACCEPTED, SPECIES, kingdom Animalia) |
| `match-lycopersicon-esculentum-synonym.json` | `Lycopersicon esculentum` | `Plantae` | 200 | EXACT | `3WLP6` (SYNONYM of `4Y369` Solanum lycopersicum) |
| `match-mentha-x-piperita-hybrid.json` | `Mentha × piperita` | `Plantae` | 200 | EXACT | `72ZGR` Mentha × piperita (ACCEPTED, SPECIES) |
| `match-brassica-oleracea-var-capitata.json` | `Brassica oleracea var. capitata` | `Plantae` | 200 | EXACT | `5MN3K` (ACCEPTED, VARIETY) |
| `taxon-info-rosa-canina.404.html` | `GET /v2/taxon/7ddf754f-…/4TCZM/info` | — | 404 | — | "API v2 is only for maps and taxa" |

What the recordings showed:

- `kingdom=Plantae` is only a hint: `Homo sapiens` with the hint falls back to the kingdom (HIGHERRANK),
  and without it matches an animal. The adapter therefore always checks the returned classification.
- `canonicalName` drops the hybrid sign and rank markers (`Mentha piperita`, `Brassica oleracea capitata`);
  `name` minus `authorship` keeps them, so that is what the adapter stores.
- The GBIF taxon details endpoint (documented as returning vernacular names) returned 404 at the path tried, so common
  names are fetched from ChecklistBank instead (see `Fixtures/ChecklistBank`).

Licence: the responses contain Catalogue of Life data, published under CC BY 4.0 unless otherwise
indicated. The exact citation required for public release has not been verified yet (see TODO, Phase 11).
