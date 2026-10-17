# Recorded ChecklistBank responses

Real responses from the ChecklistBank API, recorded once so the common-name tests run offline
(`ExternalData/CommonNameTests`, `ExternalData/PlantIngestionTests`).
Tests never call ChecklistBank. Do not re-record without approval: the bodies are evidence of how the API answered.

- Recorded: 2026-10-17, 07:20 UTC. Read-only `GET` requests, made once each, approved by the developer.
- User-Agent: `PlantEncyclopedia/0.1 (+https://github.com/lkim07/Plant-Encyclopedia)`, `Accept: application/json`
- Dataset: `3LXR`, the latest Catalogue of Life eXtended Release. The taxon IDs are the COL XR IDs GBIF returned
  in `Fixtures/Gbif`.
- URL: `https://api.checklistbank.org/dataset/3LXR/taxon/<id>/vernacular`

| File | Taxon | HTTP | Names | en / fr / de / ko |
|---|---|---|---|---|
| `vernacular-rosa-canina.json` | `4TCZM` Rosa canina | 200 | 119 (about 60 languages) | 6 / 7 / 4 / 0 |
| `vernacular-mentha-x-piperita.json` | `72ZGR` Mentha × piperita | 200 | 33 | 5 / 2 / 3 / 0 |

What the recordings showed:

- The response is a plain JSON array (no paging). Each item has `name`, `language` (ISO 639-3, e.g. `eng`;
  missing for some names) and, for some names, `referenceId`. There is no "preferred" flag.
- Names are uneven by language: many for Rosa canina, none in Korean for either plant.
- Spellings vary ("Dog-rose" / "dog rose", "Eglantier" / "Églantier"), and some names may refer to a
  cultivar or form ("curly mint"). See `CommonNamePolicy` for how the English display name is chosen.
- Response headers: `Content-Type: application/json`, `Cache-Control: public, max-age=604800`.

Licence: the responses contain Catalogue of Life data, published under CC BY 4.0 unless otherwise
indicated. The exact citation required for public release has not been verified yet (see TODO, Phase 11).
