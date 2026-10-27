# Light and dark themes, and production-level motion — design

Date: 2026-10-26. Status: draft for the developer's approval.
Mockups: https://claude.ai/artifact/3thztfCcY4X5NMUhREE8TK (private canvas; boards A and B are the two themes).

## Intent

The developer wants the Plant Encyclopedia to look and feel like a real, market-level product. The reference is
OFF+BRAND's Lando Norris website. What the developer liked there is **the motion and the smoothness of the
animation**. The colours should not copy that site: they must fit the plant concept.

The layout the app has today stays. What changes is colour, display typography, and motion.

## Goals

1. **Two exchangeable themes** (the developer's choice):
   * **Dark: "Night Greenhouse"** (mockup A). A deep evergreen-black ground, bone-white text, and one fresh lime
     "new growth" accent.
   * **Light: "Greenhouse Daylight"** (mockup B). A pale daylight ground, evergreen text, a terracotta-pot accent,
     and sage tints.
   * A visitor can switch between them at any time.
2. **A bold display face for big headlines.** Martian Mono (the developer's choice; the mockups showed Fraunces) is
   used for page titles and large display text; Nunito stays for body and UI text, with Gowun Dodum as today for
   Korean.
3. **Smooth, weighty, calm motion**, inspired by the reference site and toned for a plant product:
   * headlines rise in;
   * cards lift, and their images slowly zoom on hover;
   * pills and buttons fill with colour on hover;
   * smooth scrolling on desktop (already present: Lenis).
4. **Every screen gets both themes**: Identify and its results and More, Explore, Search, Category, Plant Group,
   Plant Detail, Favorites (including Arrange mode), Where to Buy, the account pages, Not found, the shell (sidebar,
   top bar, drawer), the snackbar and the sign-in prompt.

## Non-goals

* No layout or navigation changes, and no new features.
* Plant photos are not recoloured or filtered; only what surrounds them changes.
* No change to the mobile app plan (D-067) or to backend code.

## Anti-goals (a failure even if it works)

* Text that is hard to read in either theme (below 4.5:1 for normal text).
* Motion that delays a task: nothing waits on an animation before the user can read or act.
* Motion for people who asked their device for reduced motion.
* A flash of the wrong theme when the page loads.
* A neon, gaming look in the dark theme: the lime is an accent, never a large area.

## Decided (by the developer)

* Themes A and B as dark and light, exchangeable.
* Martian Mono for display type, instead of the Fraunces shown in the mockups.
* The layout stays.
* Look first, then a full QA pass in a real browser (a separate project after this one).

## The two palettes

Colours are design tokens on the root element. The existing token names stay, so components keep working, and a few
are added. The values below are from the approved mockups.

| Token | Dark (A) | Light (B) | Used for |
|---|---|---|---|
| `--color-bg` | `#0E1612` | `#F4F1EA` | page ground |
| `--color-surface` | `#16211B` | `#E9E5D9` | cards, sidebar, panels |
| `--color-surface-strong` | `#1F2D25` | `#DCD7C8` | pressed or selected surfaces, skeletons |
| `--color-text` | `#EEF2E9` | `#1E3328` | text, outlines of primary buttons |
| `--color-text-muted` | `#A3B1A6` | `#4F6155` | secondary text |
| `--color-accent` (new) | `#C8E86B` | `#A9532E` | primary buttons, the headline accent, active pills |
| `--color-on-accent` (new) | `#0E1612` | `#FFFFFF` | text on the accent |
| `--color-border` (new) | `#2E3D35` | `#B9C4B5` | outlines and dividers (decorative) |
| `--color-icon` | `#A3B1A6` | `#4F6155` | outlined icons |
| `--color-focus` | `#C8E86B` | `#A9532E` | focus ring |
| `--color-danger` (new) | `#F2917C` | `#A4462F` | form errors (today's hard-coded `#a4462f`) |

`--color-surface-strong` in both themes, `--color-border` in the dark theme and the dark `--color-danger` are my
calls: the mockups did not need them. The builder may tune them, keeping the contrast rule. Danger contrast: dark
8.0 on ground and 7.2 on surface; light 5.3 on ground and 4.8 on surface.

Contrast, computed (WCAG 2.x):

* Dark: text on ground 16.2, text on surface 14.6, muted on ground 8.2, muted on surface 7.4, accent on ground 13.3,
  ground-colour text on accent 13.3.
* Light: text on ground 11.9, text on surface 10.7, muted on ground 5.9, muted on surface 5.3, accent on ground 4.7,
  white on accent 5.3.
* **One rule follows:** in the light theme, terracotta *text* reaches only 4.2 on the card surface, so accent-coloured
  text sits only on the page ground. On surfaces, use the text colour or an accent-filled control.

Overlays that sit on photos keep their own fixed colours in both themes, for example the dark gradient under
"Identifying your plant…" and the name panel over the Plant Detail hero. Today's hard-coded browns become tokens or
theme-neutral values. There are about 29 hard-coded colours in component CSS; the Google and GitHub logo colours
stay as the providers require.

## Choosing the theme

* **First visit:** follow the device setting (`prefers-color-scheme`).
* **Switch:** a theme toggle in the sidebar on desktop, and in the top bar on phones. It is a real button with an
  accessible name ("Switch to dark theme" or "Switch to light theme") and `aria-pressed` or an equivalent state.
* **Remembering:** the choice is kept in this browser (`localStorage`, guarded so the app still works when storage
  is blocked). Once someone has chosen, the choice wins over the device setting. My call, for the builder to
  confirm with the developer if a "follow my device again" option is wanted later: no third "system" option for now.
* **No flash:** a tiny inline script in `index.html` sets the theme on `<html>` before Angular starts.
* The browser chrome colour (`<meta name="theme-color">`) and the `color-scheme` property follow the theme, so form
  controls and scrollbars match.

## Typography

* Martian Mono (SIL OFL 1.1) is bundled with the app like Nunito, so there is no request to a font service. This is
  one new package: `@fontsource-variable/martian-mono` 5.3.0 (OFL-1.1, checked on npm on 2026-10-26), Latin subset
  only.
* Martian Mono is for page titles (`h1`) and large display lines only. Nunito stays for everything else: body text,
  labels, buttons, and plant names on cards.
* A monospaced face is much wider than Fraunces at the same size, so display sizes are smaller than the mockup's
  96 px: around 64 px at desktop width and around 34 px on phones, with slightly tighter letter spacing. The builder
  tunes the exact sizes so titles such as "Possible matches" and "Where to Buy" fit on one line on a 390 px phone.
* Martian Mono has no Korean; Korean titles fall back to Gowun Dodum.

## Motion

Existing pieces stay: GSAP (already used for the Plant Detail hero and the Explore intro), Lenis smooth scrolling on
desktop with a fine pointer, the `Reveal` directive for cards coming into view, and View Transitions between pages.
The work makes them consistent and adds the effects below. All timings use one or two easing curves defined as
tokens (today's `--ease-out-soft`, plus a longer one for large movements).

* **Page titles rise in** on arrival, staggered by word, as in the mockups (about 900 ms).
* **Cards** (image cards on every grid, identification candidates, Favorites) lift slightly and their image zooms
  slowly on hover or keyboard focus. Touch screens get no hover effects.
* **Pills and buttons** fill with the accent or text colour on hover and focus.
* **Theme switch:** colours cross-fade (about 300 ms), not snap.
* **Smooth scrolling** stays desktop-only with a fine pointer, as today.
* **Reduced motion:** everything above is off, and content appears directly.

My calls, which the builder may refine: a slow "sway" on decorative leaf placeholders, as in the mockups; no custom
cursor (it fights accessibility and touch); no scroll-jacking, so scrolling always moves the page at the user's
pace.

## Constraints

* Angular 22, the existing CSS-per-component structure, and existing tokens; no CSS framework is added.
* Performance: motion uses `transform` and `opacity` only; nothing new blocks the first paint; the font is
  subsetted or variable and loaded with `font-display: swap`.
* Accessibility: the 4.5:1 rule above, focus rings visible in both themes, the theme toggle reachable by keyboard
  and named for screen readers, and reduced motion respected.
* All current frontend tests keep passing (134 today). Tests are added for the theme choice (device default,
  remembered choice, toggle) and for reduced motion turning effects off where that is testable.
* Docs: UX_SPECIFICATION's visual sections and DECISIONS get the new palettes, typography and theme behaviour (a new
  decision record); CURRENT_STATE and TODO are updated after validation.

## Left to the builder

* The order of work (tokens and theme switching first is the natural start).
* Exact durations and staggers within the ranges above, and which existing GSAP effects to retune.
* The toggle's icon (sun and moon outlined in the app's icon style), and its exact place in the phone top bar.
* Whether the sidebar's "Plant Encyclopedia" name also uses Martian Mono (my suggestion: yes).
* Shadows: today's brown shadows become a theme token, darker and softer in the dark theme.
* Small token additions where a component needs one, keeping the contrast rule.

## Verification

* Unit tests as above; `ng build` without warnings.
* A contrast check of every text token pair in both themes, run as a small script or test, with its results recorded.
* The developer looks at both themes in a browser on a phone and on desktop. The full QA pass comes after this
  project.
