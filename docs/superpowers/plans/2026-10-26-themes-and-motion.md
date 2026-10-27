# Light and Dark Themes, and Motion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the Angular frontend two exchangeable themes (dark "Night Greenhouse", light "Greenhouse Daylight"),
Martian Mono display titles, and calm, smooth motion, without changing the layout.

**Architecture:** Colours stay CSS custom properties in `src/styles.css`; the light set is the default and
`html[data-theme='dark']` overrides it. A tiny inline script in `index.html` picks the theme before Angular starts;
a root `Theme` service owns it afterwards and a `ThemeToggle` button switches it. Motion is CSS (under
`prefers-reduced-motion: no-preference`) plus one GSAP directive for title rises.

**Tech Stack:** Angular 22 (signals, standalone components), Vitest via `ng test`, GSAP 3.15 (SplitText included),
Lenis (existing), `@fontsource-variable/martian-mono` 5.3.0.

**Spec:** `docs/superpowers/specs/2026-10-26-themes-and-motion-design.md`

## Global Constraints

- Layout, navigation and features do not change; backend untouched.
- Token values are exactly the spec's table (light / dark): bg `#F4F1EA`/`#0E1612`, surface `#E9E5D9`/`#16211B`,
  surface-strong `#DCD7C8`/`#1F2D25`, text `#1E3328`/`#EEF2E9`, text-muted `#4F6155`/`#A3B1A6`, accent
  `#A9532E`/`#C8E86B`, on-accent `#FFFFFF`/`#0E1612`, border `#B9C4B5`/`#2E3D35`, icon `#4F6155`/`#A3B1A6`, focus
  `#A9532E`/`#C8E86B`, danger `#A4462F`/`#F2917C`.
- Normal text ≥ 4.5:1 in both themes; focus ring ≥ 3:1 against bg and surface. In the light theme, accent-coloured
  text only on `--color-bg`, never on surfaces.
- Plant photos are never recoloured; overlays on photos use fixed, theme-neutral colours.
- All motion is off under `prefers-reduced-motion: reduce` (use `motionAllowed()` from `src/app/shared/motion.ts` in
  TypeScript, `@media (prefers-reduced-motion: no-preference)` in CSS); motion uses only `transform` and `opacity`
  (colour cross-fade excepted); nothing waits on an animation before content is readable or clickable.
- No custom cursor, no scroll-jacking; Lenis stays desktop-with-fine-pointer only, as today.
- Theme storage key `plant-encyclopedia.theme`, values `light` | `dark`; anything else counts as no choice. Storage
  access is always in `try/catch`.
- Martian Mono only for `h1` and the sidebar brand name; Nunito for everything else; Korean falls back to Gowun Dodum.
  Display size about 34 px on a 390 px phone to about 64 px on desktop.
- Repo rules (CLAUDE.md): never run state-changing git commands — "Commit" steps mean: run the checks, then give the
  developer the suggested `git add` / `git commit`. Prettier only on files you created or edited. Do not edit
  CLAUDE.md or Engineering_notes.md. Docs (CURRENT_STATE, TODO, UX_SPECIFICATION, DECISIONS) only after validation.
- Each task ends with `npx ng test --watch=false` (all pass; 134 before this plan) and `npx ng build` with no
  warnings, run from `frontend/`.

## Review Focus

1. Storage blocked (private mode, disabled site data): the toggle still switches for the visit and nothing throws —
   tested in Task 1.
2. A tampered stored value (e.g. `"purple"`): treated as no choice, so the device setting applies, in both the inline
   script and the service — tested in Task 1 (service) and by the shared helper in Task 1.
3. The device switches light/dark while the app is open: followed when the visitor never chose, ignored once they
   chose — tested in Task 1.
4. A title whose text arrives or changes after it rendered (plant group, category, Where to Buy plant name): its text
   is never lost or duplicated by the title-rise split — tested in Task 5.
5. Keyboard focus visible on accent-filled buttons in both themes (focus ring colour equals the button fill) —
   handled in Task 2 by an outline offset and checked in Task 2's contrast script.

---

### Task 1: Theme tokens, early theme choice, and the `Theme` service

**Files:**
- Modify: `frontend/src/styles.css` (token blocks, `color-scheme`, shadow and easing tokens, cross-fade rule)
- Modify: `frontend/src/index.html` (inline theme script, `<meta name="theme-color">`)
- Create: `frontend/src/app/core/theme/theme.ts`
- Create: `frontend/src/app/core/theme/theme.spec.ts`
- Create: `frontend/scripts/check-theme.mjs`, and `"check:theme": "node scripts/check-theme.mjs"` in `package.json` scripts

**Interfaces:**
- Produces: `export type ThemeName = 'light' | 'dark'`; `export const THEME_STORAGE_KEY = 'plant-encyclopedia.theme'`;
  `export function parseStoredTheme(value: string | null): ThemeName | null`;
  `@Injectable({ providedIn: 'root' }) export class Theme { readonly theme: Signal<ThemeName>; toggle(): void; set(theme: ThemeName): void }`.
- Produces (CSS): new tokens `--color-accent`, `--color-on-accent`, `--color-border`, `--color-danger`,
  `--shadow-card`, `--shadow-lifted`, `--ease-out-long`; existing token names keep their meaning.

- [ ] **Step 1: Write the failing tests** in `theme.spec.ts` (stub `window.matchMedia` with a controllable
  `prefers-color-scheme: dark` list; clear `localStorage` and `document.documentElement.dataset.theme` in
  `beforeEach`):
  - `parseStoredTheme('dark') === 'dark'`, `parseStoredTheme('light') === 'light'`, `parseStoredTheme('purple') === null`, `parseStoredTheme(null) === null`.
  - "starts from the theme index.html chose": with `dataset.theme = 'dark'` before injecting, `theme()` is `'dark'`.
  - "toggle switches, remembers, and updates the page": after `toggle()` from light, `dataset.theme === 'dark'`,
    `localStorage.getItem(THEME_STORAGE_KEY) === 'dark'`, and `meta[name="theme-color"]` content is `#0E1612`
    (light: `#F4F1EA`).
  - "works when storage is blocked": with `Storage.prototype.setItem` throwing, `toggle()` still sets
    `dataset.theme` and `theme()`, and does not throw.
  - "follows the device until the visitor chooses": no stored value, firing the media query's `change` to dark sets
    `theme()` to `'dark'`; after `set('light')`, another `change` to dark leaves `'light'`.
  - "cross-fades only briefly": with fake timers and motion allowed, `toggle()` adds class `theme-transition` to
    `<html>`, removed after 350 ms; with reduced motion it is never added.
- [ ] **Step 2: Run** `npx ng test --watch=false --include src/app/core/theme/theme.spec.ts` — expected FAIL (module not found).
- [ ] **Step 3: Implement `theme.ts`.** Initial theme: `dataset.theme` if valid, else stored choice, else
  `matchMedia('(prefers-color-scheme: dark)')`. `set()` writes `dataset.theme`, the storage key (guarded), and the
  `theme-color` meta (`#F4F1EA` / `#0E1612`); adds `theme-transition` for 350 ms when `motionAllowed()`. Listen to
  the media query's `change` only while there is no stored choice.
- [ ] **Step 4: Tokens in `styles.css`.** Replace today's warm values: light set on `:root` (with
  `color-scheme: light`), dark set on `:root[data-theme='dark']` (with `color-scheme: dark`), values from Global
  Constraints. Add `--shadow-card` (light `0 18px 40px rgb(30 51 40 / 0.18)`, dark `0 18px 40px rgb(0 0 0 / 0.45)`),
  `--shadow-lifted` (light `0 10px 28px rgb(30 51 40 / 0.28)`, dark `0 10px 28px rgb(0 0 0 / 0.55)`),
  `--ease-out-long: cubic-bezier(0.16, 1, 0.3, 1)`, and, inside `prefers-reduced-motion: no-preference`,
  `html.theme-transition, html.theme-transition * { transition: background-color 300ms, color 300ms, border-color 300ms, fill 300ms, stroke 300ms !important; }`.
  Update the file's header comment (it cites UX §2.1's warm neutrals).
- [ ] **Step 5: `index.html`.** Add `<meta name="theme-color" content="#F4F1EA">` and, before `</head>`, an inline
  `<script>` that applies the same rule as `parseStoredTheme` + device fallback to `document.documentElement.dataset.theme`
  and the meta (storage read in `try/catch`). Keep it under 25 lines; comment that it must match `theme.ts`.
- [ ] **Step 6: `scripts/check-theme.mjs`.** Parse the two token blocks from `src/styles.css`; compute WCAG contrast;
  fail (exit 1) if any of these is below its floor, printing every ratio: ≥ 4.5 for text/bg, text/surface,
  text/surface-strong, text-muted/bg, text-muted/surface, text-muted/surface-strong, on-accent/accent, danger/bg,
  danger/surface, accent/bg; ≥ 3 for focus/bg, focus/surface, icon/bg. Also fail if a `.css`, `.html` or `.ts` file
  under `src/app` (specs excluded) contains a hex or `rgb(` colour outside this allowlist: `#000` in mask gradients,
  the provider logo fills in `features/account/provider-buttons.ts`, and lines carrying the comment
  `/* photo overlay: fixed */`. (Task 2 makes the allowlist check pass; until then expect it to list the files.)
- [ ] **Step 7: Run** the theme spec (PASS), `npm run check:theme` (contrast PASS; colour list printed),
  full `npx ng test --watch=false`, `npx ng build`.
- [ ] **Step 8: Commit** (suggest to the developer): `feat(theme): light and dark tokens, early theme choice and Theme service`.

### Task 2: Every component on tokens

**Files:**
- Modify: `features/account/account-page.css` (lines 59, 71 → `--color-danger`)
- Modify: `features/favorites/favorites-page.css:203`, `features/search/search-box.css:47`, `shared/snackbar/snackbar.ts:102` (shadows → `--shadow-lifted` / `--shadow-card`)
- Modify: `shared/bookmark/bookmark-button.ts:31-32` (background → `color-mix(in srgb, var(--color-bg) 90%, transparent)`, shadow → token)
- Modify: `features/plant-detail/plant-detail-page.css:14` (→ `color-mix(in srgb, var(--color-bg) 85%, transparent)`), `:94,:101` (name panel over the hero photo → fixed `rgb(14 22 18 / 0.82)` and `rgb(238 242 233 / 0.9)`, marked `/* photo overlay: fixed */`)
- Modify: `features/identify/identify-page.css:90-91` (mark `/* photo overlay: fixed */`)
- Modify: primary controls to the accent — `.action.primary` (identify), `.choice.primary`, `.sort button[aria-pressed='true']` (where-to-buy), the active sidebar item, the Favorites `.arrange-toggle` when pressed, category pills on hover/focus: `background: var(--color-accent); color: var(--color-on-accent)`; accent-filled buttons get `outline-offset: 3px` on `:focus-visible`.
- Modify: borders that use `--color-text` as a divider → `--color-border` (keep `--color-text` borders on outlined buttons).

**Interfaces:**
- Consumes: Task 1 tokens.

- [ ] **Step 1: Run** `npm run check:theme` — expected FAIL listing the hard-coded colours above.
- [ ] **Step 2: Replace** each listed colour as specified; no other visual changes.
- [ ] **Step 3: Run** `npm run check:theme` — expected PASS (no unlisted colours, all ratios above floors).
- [ ] **Step 4: Run** `npx ng test --watch=false` and `npx ng build`; then `npx prettier --check` on the edited files.
- [ ] **Step 5: Commit** (suggest): `style(theme): components use theme tokens and the accent`.

### Task 3: Theme toggle in the sidebar and top bar

**Files:**
- Create: `shared/theme-toggle/theme-toggle.ts` (inline template and styles, like `bookmark-button.ts`)
- Create: `shared/theme-toggle/theme-toggle.spec.ts`
- Modify: `shared/icons/icon.ts` (add `moon`; `sun` exists)
- Modify: `layout/sidebar/sidebar.html` (toggle inside `.account`, above Sign in / Sign out; label hidden when collapsed), `layout/top-bar/top-bar.html` (toggle before the existing trailing control)
- Modify: `layout/shell/shell.spec.ts`

**Interfaces:**
- Consumes: `Theme` from Task 1.
- Produces: `<app-theme-toggle [showLabel]="boolean" />`.

- [ ] **Step 1: Write the failing tests:**
  - `theme-toggle.spec.ts` — "names the next theme and switches": in light, the button's accessible name is
    `Switch to dark theme` and it shows the `moon` icon; clicking calls the theme to dark and the name becomes
    `Switch to light theme` with `sun`. With `showLabel` the visible text reads `Dark theme` / `Light theme`.
  - `shell.spec.ts` — "offers the theme switch in the sidebar and the top bar": both regions contain a button named
    `Switch to dark theme` (or light).
- [ ] **Step 2: Run** both specs — expected FAIL.
- [ ] **Step 3: Implement** `ThemeToggle` as a `<button type="button" class="icon-button">` with `aria-label` from
  `theme()`; place it in the sidebar and top bar.
- [ ] **Step 4: Run** both specs (PASS), full tests, build, prettier on edited files.
- [ ] **Step 5: Commit** (suggest): `feat(theme): theme switch in the sidebar and top bar`.

### Task 4: Martian Mono display type

**Files:**
- Modify: `frontend/package.json` / lockfile (`npm install @fontsource-variable/martian-mono@5.3.0`)
- Modify: `frontend/angular.json` styles (add the package's Latin-only CSS file; check the package's file list for its name, e.g. `@fontsource-variable/martian-mono/index.css` and whether a Latin-only variant exists)
- Modify: `src/styles.css` (`--font-display: 'Martian Mono Variable', 'Gowun Dodum', ui-monospace, monospace`; global `h1 { font-family: var(--font-display); font-size: clamp(2.125rem, 1.4rem + 2.6vw, 4rem); letter-spacing: -0.02em; line-height: 1.05; font-weight: 600 }`)
- Modify: page `h1` rules that set their own size so they use the global size — `features/account/account-page.css:17`, `features/explore/explore-page.css:9`, `features/favorites/favorites-page.css:13`, `features/identify/identification-results-page.css:22`, `features/identify/identify-page.css:11`, `features/where-to-buy/where-to-buy-page.css:17`; Plant Detail's hero `.names h1` (`plant-detail-page.css:113`) keeps its own size but takes the display font
- Modify: `layout/sidebar/sidebar.css` (`.brand-name` uses `--font-display`)

**Interfaces:**
- Produces: `--font-display` token.

- [ ] **Step 1: Install** the package; confirm its `LICENSE` is OFL-1.1 and note the CSS file chosen.
- [ ] **Step 2: Apply** the token and `h1` rules above.
- [ ] **Step 3: Run** full tests and `npx ng build`; record the font files' total size from the build output (expected
  a few tens of kB).
- [ ] **Step 4: Commit** (suggest): `style(type): Martian Mono for page titles`.

### Task 5: Motion — title rise, cards, pills, leaves

**Files:**
- Create: `shared/title-rise.ts` (directive `[appTitleRise]`)
- Create: `shared/title-rise.spec.ts`
- Modify: `shared/motion.ts` (add `loadSplitText(): Promise<typeof import('gsap/SplitText').SplitText>`, registered once with gsap, loaded on first use like `loadGsap()`)
- Modify: page `h1` elements to carry `appTitleRise`: Identify, Identification Results, More, Explore, Favorites, Search's heading if any, Category, Plant Group, Where to Buy, Not found (not Plant Detail's hero title, which has its own GSAP intro)
- Modify: `shared/image-card/image-card.css` (hover/focus: card `translateY(-6px)`, image `scale(1.06)` over 900 ms `--ease-out-long`; `.placeholder` sway `rotate(-2deg↔2deg)` 6 s infinite), `features/identify/identification-results-page.css` (candidate cards same lift), `features/explore/explore-page.css` (pill fill transition 300 ms)
- Modify: `features/explore/explore-page.ts` (drop its own title animation from the intro, keep the pill stagger)

**Interfaces:**
- Consumes: `motionAllowed()`, `loadGsap()` from `shared/motion.ts`.
- Produces: `[appTitleRise]` directive, no inputs.

- [ ] **Step 1: Write the failing tests** in `title-rise.spec.ts` (host component `<h1 appTitleRise>{{ title() }}</h1>` with a signal):
  - "does nothing when motion is reduced": `matchMedia` reports reduce; after render the `h1` has no child elements
    and `textContent` equals the title.
  - "keeps the text when the title changes": with motion allowed and `loadSplitText` replaced by a fake whose
    `create(el)` returns `{ words: [...], revert: vi.fn() }`, set the title to `Roses` then to `Tulips`;
    `textContent` is `Tulips`, and `revert` was called before the second split (or the directive did not split
    again) — the element never contains both words.
  - "reverts on destroy": destroying the host calls the fake's `revert`.
- [ ] **Step 2: Run** the spec — expected FAIL.
- [ ] **Step 3: Implement `TitleRise`.** After first render, if `motionAllowed()`: split into words, animate
  `from({ yPercent: 60, opacity: 0, stagger: 0.09, duration: 0.9, ease: 'power3.out' })`, and `revert()` the split on
  completion and on destroy; if the host's text changes before completion, revert immediately. Text is visible the
  whole time when motion is off.
- [ ] **Step 4: Apply** the directive and the CSS listed above, all CSS motion inside `prefers-reduced-motion: no-preference`; hover effects inside `(hover: hover)`.
- [ ] **Step 5: Run** the spec (PASS), full tests, build, prettier on edited files.
- [ ] **Step 6: Commit** (suggest): `feat(motion): title rise, card lift and zoom, pill fill, leaf sway`.

### Task 6: Docs and final verification

**Files:**
- Modify: `Project Docs/01_UX_SPECIFICATION.md` (visual direction sections: replace the warm-neutral palette with the two themes, the theme switch, Martian Mono, the motion list)
- Modify: `Project Docs/02_DECISIONS.md` (new D-074: light and dark themes, Martian Mono, motion rules; cite the spec and mockup link)
- Modify: `Project Docs/09_CURRENT_STATE.md`, `Project Docs/10_TODO.md` (done items; "check both themes on a phone and desktop" as High; the QA pass as the next project)

- [ ] **Step 1: Run** `npm run check:theme`, `npx ng test --watch=false`, `npx ng build`; record the numbers.
- [ ] **Step 2: Update** the docs above with the verified numbers only.
- [ ] **Step 3: Commit** (suggest): `docs: light and dark themes, display type and motion`.
