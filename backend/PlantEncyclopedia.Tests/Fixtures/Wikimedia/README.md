# Recorded Wikipedia and Wikimedia Commons responses

Real responses from the MediaWiki Action API, recorded once so the image tests run offline
(`ExternalData/PlantImageTests`). Tests never call Wikimedia. Do not re-record without approval: the bodies are
evidence of how the API answered.

- Recorded: 2026-10-17, 09:43 UTC (the two species categories on 2026-10-19). Read-only `GET` requests, made once each, approved by the developer. (The first attempt at the Rosa canina category failed with a DNS error before reaching the server and was retried once.)
- User-Agent: `PlantEncyclopedia/0.1 (+https://github.com/lkim07/Plant-Encyclopedia)`
- All requests use `action=query&format=json&formatversion=2`. File requests add
  `prop=imageinfo&iiprop=url|size|mime|extmetadata&iiurlwidth=1600`.

| File | Request | HTTP | Result |
|---|---|---|---|
| `wikipedia-pageimage-rosa-canina.json` | en.wikipedia.org `prop=pageimages&piprop=name\|original&redirects=1&titles=Rosa canina` | 200 | `Divlja_ruza_cvijet_270508.jpg` |
| `wikipedia-pageimage-mentha-x-piperita.json` | same, `titles=Mentha × piperita` | 200 | redirect to "Peppermint"; `Pfefferminze_natur_peppermint.jpg` |
| `commons-imageinfo-rosa-canina.json` | commons.wikimedia.org `titles=File:Divlja_ruza_cvijet_270508.jpg` | 200 | 1024×1365 JPEG, CC BY-SA 3.0, author "Roberta F." |
| `commons-imageinfo-mentha-x-piperita.json` | commons.wikimedia.org `titles=File:Pfefferminze_natur_peppermint.jpg` | 200 | 3264×2448 JPEG, CC BY 3.0, **no author** ("Media lacking author information") |
| `commons-illustrations-rosa-canina.json` | commons.wikimedia.org `generator=categorymembers&gcmtitle=Category:Rosa canina - botanical illustrations&gcmtype=file&gcmlimit=10` | 200 | 10 of more files (`continue` present) |
| `commons-illustrations-mentha-x-piperita.json` | same, `Category:Mentha × piperita - botanical illustrations` | 200 | 10 of more files |
| `commons-category-rosa-canina.json` | commons.wikimedia.org `generator=categorymembers&gcmtitle=Category:Rosa canina&gcmtype=file&gcmlimit=20` (2026-10-19, 22:49 UTC) | 200 | 20 of more files: photos (CC BY-SA, CC BY, CC0), videos and pronunciation audio |
| `commons-category-mentha-x-piperita.json` | same, `Category:Mentha × piperita` (2026-10-19, 22:48 UTC) | 200 | 20 of more files: photos (CC BY-SA, PD), including 5 of a grasshopper also filed as "Anacridium aegyptium" |

What the recordings showed:

- `extmetadata` holds `License` (`pd`, `cc-by-sa-3.0`, ...; missing for Flickr Commons "No restrictions"
  files), `LicenseShortName`, `AttributionRequired`, `Artist` (HTML), `Attribution`, `Restrictions`.
- Illustration licences are mostly public domain; some are CC BY 4.0 (Wellcome) or CC BY 2.0 (Biodiversity
  Heritage Library) with an empty `Artist`; some are small scans (331×400) or TIFF.
- `thumbwidth`/`thumbheight` report the requested width (1600) even when Commons serves the original
  (`utm_content=thumbnail_unscaled`, same path as `url`), including some originals wider than 1600.
- Thumbnails are served from `thumb.wikimedia.org`, originals from `upload.wikimedia.org`.
- Species categories mix photos with videos (`application/ogg`), audio and photos of other organisms filed under both names; the photo source leaves out files also filed under another two-word Latin-style name.
- The Mentha photo response contains the camera's GPS coordinates in `GPSLatitude`/`GPSLongitude`.

Licence: each file's own licence, as recorded above. These JSON responses are API metadata; no image files
were downloaded.
