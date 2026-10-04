# Plant Encyclopedia — UX Specification

## 1. UX Design Direction

Plant Encyclopedia is an image-first plant discovery application.

The interface should feel:

* Modern
* Clean
* Calm
* Premium
* Botanical
* Visually immersive
* Fast and responsive

The visual design should prioritize plant photography and readable information rather than dense UI elements.

The product should feel like a polished consumer application rather than a dashboard or technical demonstration.

### Core UX principle

> **Make the interface visually memorable without making the user wait for it.**

Interactive effects are welcome when they improve delight, orientation, or visual identity.

They must never interfere with:

* Navigation
* Reading
* Searching
* Identification
* Saving
* Scrolling
* Perceived responsiveness

---

# 2. Design Language

## 2.1 Visual Tone

The primary visual language should use:

* Warm neutral backgrounds
* Warm beige tones where appropriate
* Brown or dark-brown typography
* Natural plant photography
* Subtle translucent surfaces
* Generous whitespace
* Rounded but restrained components
* Minimal borders
* Soft transitions

The interface should avoid:

* Excessive gradients
* Excessive shadows
* Overly colorful UI controls
* Excessive rounded cards
* Dashboard-like layouts
* Heavy decorative elements

Plant imagery should provide most of the visual richness.

---

# 3. Responsive Navigation

## 3.1 Desktop Sidebar

Desktop uses an expandable/collapsible sidebar.

### Expanded state

```text
┌────────────────────┐
│ 🌿 Plant           │
│    Encyclopedia    │
│                    │
│ 📷 Identify        │
│ 🔍 Explore         │
│ 🔖 Favorites       │
│                    │
│                    │
│ 👤 Login           │
└────────────────────┘
```

The sidebar should remain visually simple.

The logo/branding should be identifiable but should not consume excessive vertical space.

### Collapsed state

```text
┌────┐
│ 🌿 │
│    │
│ 📷 │
│ 🔍 │
│ 🔖 │
│    │
│ 👤 │
└────┘
```

The collapsed sidebar displays icons only.

The user should be able to expand or collapse it.

The transition should be fast and subtle.

---

# 4. Mobile Navigation

Mobile uses a slide-in sidebar.

The sidebar is closed by default.

The top application bar contains:

```text
┌────────────────────────────┐
│ ☰     Plant Encyclopedia  👤 │
└────────────────────────────┘
```

### Top bar

Left:

* Menu button

Center:

* Plant Encyclopedia

Right:

* Login / Account button

The account entry is also available inside the sidebar.

### Mobile sidebar

The sidebar slides in from the left.

It contains:

* Plant Encyclopedia branding
* Identify
* Explore
* Favorites
* Login / Account

Tapping outside the sidebar closes it.

A persistent bottom navigation bar is not used.

---

# 5. Navigation Model

Sidebar navigation and Back navigation have different purposes.

### Sidebar

Sidebar navigation is destination-based.

Example:

> Open sidebar → Explore

This moves directly to Explore.

### Back

Back navigation follows the navigation stack.

Example:

```text
Explore
  ↓
Search
  ↓
Plant A
  ↓
Plant B
```

Back from Plant B returns to Plant A.

Back should preserve relevant previous state when practical.

This may include:

* Search query
* Search results
* Category
* Scroll position
* Explore feed position
* Identification results

The browser's navigation history should not be unnecessarily replaced by application-level navigation.

---

# 6. Home / Identify

Identify is the primary visual action when the application opens.

The first meaningful visual interaction should be plant identification rather than a text search field.

The home/Identify experience should emphasize:

* Camera
* Photo selection
* Visual discovery

There should not be a persistent text search field on the initial Identify screen.

---

# 7. Camera Identification Flow

The identification flow should feel similar to familiar mobile camera/photo workflows.

```text
Identify
   ↓
Camera / Photo Selection
   ↓
Image Preview
   ↓
Identify
   ↓
Identification Results
```

## 7.1 Photo Selection

The user can:

* Take a Photo
* Choose from Photos

These actions may be presented through a bottom sheet on mobile.

---

# 8. Image Preview

After selecting or taking an image, show a preview before identification.

Primary actions:

* Retake
* Identify

The selected image should remain visible.

The user must explicitly start identification.

---

# 9. Identification Loading

During identification:

* Keep the selected image visible.
* Avoid replacing the screen with a generic spinner.
* Display concise feedback:

> **Identifying your plant…**

Do not display artificial progress percentages.

Do not expose detailed AI pipeline stages unless a future implementation has a genuine user-facing reason to do so.

---

# 10. Identification Results

The results page contains:

```text
┌────────────────┐
│                │
│      #1        │
│                │
└────────────────┘

┌──────┬──────┬──────┐
│  #2  │  #3  │ MORE │
└──────┴──────┴──────┘
```

## Primary candidate

The #1 candidate receives the largest visual treatment.

It should show:

* Plant image
* Primary plant/cultivar name
* Representative common name where appropriate
* Scientific name where appropriate

The candidate should not display confidence percentages.

## Secondary candidates

#2 and #3 receive smaller cards.

## More

More is a discovery action, not a fourth candidate.

It opens a related plant grid centered on the identification context.

---

# 11. Identification Card Interaction

Desktop:

* Hovering over a card slightly changes the image treatment.
* The image becomes lighter/faded.
* Plant name information appears over the image.

Mobile:

* No hover interaction.
* Tapping a candidate opens Plant Detail.
* A long press may be used when necessary to expose additional card information, but normal navigation should remain tap-based.

The interaction must not prevent the user from understanding that the card is selectable.

---

# 12. Explore

Explore is the primary discovery surface.

The initial Explore screen should not display a persistent search field.

Instead, the header contains:

* Explore title
* Search icon

Selecting the search icon enters Search mode.

---

# 13. Explore Initial State

For users without meaningful behavioral data:

* Provide diverse curated discovery.
* Keep major categories approximately balanced.
* Avoid popularity-only ranking.
* Avoid purely random content.
* Prevent the same plant/cultivar from appearing multiple times.

The first feed should maintain category and plant diversity.

A feed should not look like:

```text
Rose
Rose
Rose
Tulip
Rose
Rose
```

even if roses are popular.

Different cultivars may appear separately.

---

# 14. Explore Personalized Feed

For users with meaningful behavioral data, the default conceptual recommendation mix is:

```text
60%  Strongly related interests
20%  Adjacent / somewhat related
20%  Novel / substantially different
```

This is a recommendation guideline, not a requirement to produce mathematically exact percentages in every small viewport.

The system should maintain diversity within the personalized portion.

For example, a user who frequently views roses may receive:

```text
Rosa 'Peace'
Rosa 'Iceberg'
Rosa 'Mr. Lincoln'
Other related flowers
Other plant families
Unfamiliar plants
```

but should not receive the same cultivar repeatedly.

---

# 15. Explore Feed State

When navigating away from Explore and returning through Back navigation, preserve the relevant feed state where practical.

This includes:

* Scroll position
* Loaded results
* Search context if applicable
* Current recommendation state

When the user leaves Explore entirely and later enters it again through sidebar navigation, the feed may recompose or refresh.

Personalization should remain intact.

---

# 16. Explore Categories

Categories are displayed near the beginning of Explore.

Use moderately large horizontal pills.

Example:

```text
[ Flowers ] [ Trees ] [ Fruits ] [ Vegetables ] [ Herbs ] [ Succulents ]
```

Behavior:

* Horizontal scrolling
* Hidden scrollbar
* Swipe on mobile
* Wheel/drag support on desktop
* Fade/gradient at the visible edges
* Remove the corresponding fade when the user reaches an end

All primary categories should be visible in the scrollable sequence without requiring a category menu.

---

# 17. Explore Plant Cards

Explore cards are image-first.

Desktop:

* Masonry/image grid
* Bookmark in the upper-right
* Hover reveals plant information
* Image receives subtle visual treatment during hover

Mobile:

* Two-column masonry grid
* Bookmark in the upper-right
* Tap opens Plant Detail
* No hover-dependent information

The same plant/cultivar must not be represented by multiple cards in the same feed.

---

# 18. Search Mode

Search begins when the user selects the Search icon from Explore.

Search mode contains:

```text
←  [ Search plants... ]
```

The search bar remains sticky while searching.

When no query has been entered, recent searches may be shown.

---

# 19. Search Suggestions

Suggestions should be database-first.

Relevant matches may include:

* Common name
* Scientific name
* Cultivar
* Plant group
* Taxonomic relationship

Suggestions should update as the user types.

Selecting a suggestion executes the search.

Pressing Enter also executes the search.

---

# 20. Recent Searches

Recent searches are text-based.

Each item has an individual remove action.

Example:

```text
Recent Searches

Rose                         ×
Peony                        ×
Rosa 'Peace'                 ×
```

Removing a recent search:

* Removes it from Search History.
* Removes its direct recommendation influence where applicable.
* Does not remove the corresponding plant from the encyclopedia.

---

# 21. Search Results

Search results use the same image-first visual language as Explore.

Use:

* Masonry/image grid
* Two columns on mobile
* Responsive layout on desktop
* Infinite scrolling
* Bookmark controls

Search ranking prioritizes query relevance.

Explore personalization should not override strong search relevance.

---

# 22. Category Page

Category pages provide focused browsing.

Example:

```text
Explore
  ↓
Flowers
```

The Category page header contains:

* Back button
* Category title
* Optional Search action where useful

The category page shows image-based plant-group cards.

Example:

```text
┌──────────┐ ┌──────────┐
│  Rose    │ │  Tulip   │
│  image   │ │  image   │
└──────────┘ └──────────┘

┌──────────┐ ┌──────────┐
│  Peony   │ │  Orchid  │
│  image   │ │  image   │
└──────────┘ └──────────┘
```

Plant-group cards are visual navigation elements.

---

# 23. Plant Group Page

Selecting a plant group opens a focused plant/cultivar grid.

Example:

```text
Flowers
  ↓
Roses
```

The page displays:

```text
Peace
Iceberg
Mr. Lincoln
Juliet
Double Delight
...
```

Each item is an image card.

Selecting a plant/cultivar opens Plant Detail.

---

# 24. Category Purity

Category browsing must remain within the selected category.

For example:

```text
Flowers
  ↓
Roses
```

must not insert unrelated:

* Trees
* Vegetables
* Herbs
* Succulents

into the primary browsing results.

Cross-category discovery belongs to Explore.

Behavior within categories can still influence future Explore recommendations.

---

# 25. Plant Detail

All plant entry points use the same Plant Detail experience.

Possible entry points:

* Explore
* Search
* Identification Results
* More
* Favorites
* Category browsing

The page structure is:

```text
Hero
↓
Name / Classification
↓
Quick Care
↓
Blooming
↓
Health
↓
About
↓
Where to Buy
↓
Similar Plants
```

---

# 26. Plant Detail Hero

The hero should be nearly full-screen.

Desktop and mobile both use immersive photography.

Controls:

```text
←                              🔖
```

Back:

* Upper-left
* Remains accessible over the image

Bookmark:

* Upper-right
* Remains visually stable over the hero

---

# 27. Hero Scroll Transition

The hero uses a scroll-driven transition.

At the beginning:

* Image dominates the viewport.
* Text sits directly over the image.

As the user scrolls:

* Image gradually becomes more transparent.
* Text becomes easier to read.
* Background transitions toward warm beige/neutral tones.

Avoid a hard image-to-content boundary.

The transition should feel like the image is naturally dissolving into the information.

---

# 28. Plant Naming Hierarchy

If a cultivar exists:

```text
Rosa 'Peace'
Hybrid Tea Rose
Rosa × hybrida
```

If a cultivar does not exist:

```text
Rose
Rosa
```

The first line should be the most understandable plant/cultivar name.

The second line provides a useful plant-group/type descriptor where available.

The scientific name follows.

---

# 29. Quick Care

Quick Care contains four compact cards:

```text
┌─────────────┐ ┌─────────────┐
│ ☀ Light     │ │ 💧 Water    │
│ Full Sun  ↓ │ │ Soil dry ↓  │
└─────────────┘ └─────────────┘

┌─────────────┐ ┌─────────────┐
│ 🌡 Temp     │ │ 🌱 Soil     │
│ 15–25°C   ↓ │ │ Well-drain ↓│
└─────────────┘ └─────────────┘
```

Cards:

* Are clickable
* Have a downward arrow in the lower-right
* Expand to reveal additional explanation
* Change the arrow direction when expanded

The entire card should be clickable.

Care information should favor actionable conditions over arbitrary fixed schedules.

---

# 30. Blooming

Blooming is concise.

Example:

```text
Blooming

🌸 May–October
↻ Repeat bloomer
```

It is not an accordion.

Avoid excessive gardening detail.

---

# 31. Health

Health is concise.

Potential content:

* Common pests
* Common diseases
* Disease resistance when reliable

It is not an accordion in the MVP.

---

# 32. About

About may be collapsible.

Collapsed:

```text
About
Origin and background...
                              ↓
```

Expanded:

* Origin
* Short history
* Background
* Other relevant contextual information

The content should generally remain around a few concise paragraphs rather than becoming a long encyclopedia article.

---

# 33. Where to Buy

Where to Buy appears before Similar Plants.

Plant Detail contains a compact entry card:

```text
🌱 Where to Buy

Find nurseries, garden centres
& flower shops near you

Explore →
```

Selecting the card opens the dedicated Where to Buy page.

---

# 34. Where to Buy Page

The page contains a location selector:

```text
[ 📍 Vancouver, BC ]
```

### Nearby

Businesses are displayed with:

* Distance
* Rating
* Relevance as appropriate

Default ordering:

> Distance

Alternative sorting:

> Rating
> Relevance

### Online

Online retailers may be displayed separately.

Do not present inventory as live unless supported by a reliable inventory integration.

Approximate prices should communicate their source/date where appropriate.

---

# 35. Location Permission UX

Do not request location permission on application entry.

Request location only when the user enters Where to Buy.

If permission is granted:

> Use the current location.

If denied:

> Use Vancouver, BC as the default location.

Allow the user to manually change the location.

The user should still be able to use the rest of the application without granting location access.

---

# 36. Similar Plants

Similar Plants appears after Where to Buy.

Initial view:

```text
Similar Plants

┌────────┐ ┌────────┐ ┌────────┐
│        │ │        │ │        │
│ Plant  │ │ Plant  │ │ Plant  │
└────────┘ └────────┘ └────────┘

        ← swipe →
        
View More →
```

Show three cards initially.

A horizontal carousel reveals approximately three additional cards.

`View More →` opens the More page.

---

# 37. More

More is a contextual discovery grid.

It should answer:

> "What else is related to this plant?"

The page is centered on the current plant/context.

It is not:

* A fourth identification candidate
* A replacement for Explore
* A global random feed

Selecting a plant opens Plant Detail.

---

# 38. Favorites

Favorites uses the same image-first grid language.

Normal state:

```text
┌────────┐ ┌────────┐
│    🔖  │ │    🔖  │
│ image  │ │ image  │
└────────┘ └────────┘
```

Unbookmarking removes the item immediately.

Show:

> Removed from Favorites. Undo

through a snackbar.

---

# 39. Favorites Authentication

If a user is not authenticated and selects Bookmark:

Use a bottom sheet on mobile and an appropriate modal/dialog treatment on desktop.

Content:

```text
Save this plant

Sign in to save plants to your Favorites.

[ Sign in ] [ Cancel ]
```

After successful authentication, preserve the user's original intent where practical.

---

# 40. Favorites Arrange Mode

Normal Favorites browsing and editing are separate modes.

## Mobile

Long press a card.

Enter Arrange Mode.

Cards:

* Jiggle subtly
* Display a delete/minus control
* Can be dragged to reorder

## Desktop

Use an explicit Arrange/Edit action or contextual menu.

Do not use hover alone to activate editing.

The MVP stores logical ordering rather than raw screen coordinates.

---

# 41. Favorites Empty State

When Favorites contains no plants:

Display three decorative plant images.

The images:

* Are non-clickable
* Are visual examples
* Use a subtle fade/gradient toward the CTA

Primary CTA:

> **Explore Plants →**

Selecting it navigates to Explore.

The decorative images disappear once the user has saved plants.

---

# 42. Loading States

## General Content

Use skeleton UI.

Avoid full-screen loading screens when existing content can remain visible.

Skeletons should approximate the final layout.

## Plant Detail

Skeleton:

* Hero image placeholder
* Name placeholder
* Quick Care placeholders
* Content section placeholders

## Image Identification

Keep the selected image visible.

Display:

> **Identifying your plant…**

The loading state should feel responsive rather than theatrical.

---

# 43. Empty States

## Search

```text
No plants found

We couldn't find a match for "[query]".

Try another name or search term.
```

If appropriate, offer image identification as an alternative.

## Category

```text
No plants available yet.
```

Do not replace the category with unrelated recommendations.

---

# 44. Error States

Errors should be contextual and recoverable.

## Data / Network Error

```text
Something went wrong

We couldn't load the plants.
Please check your connection and try again.

[ Try Again ]
```

## Identification Error

```text
We couldn't identify this plant

The image may be unclear, or the plant
may not be in our database yet.

[ Try Again ] [ Choose Another Photo ]
```

Do not claim that a plant does not exist solely because the AI failed to identify it.

---

# 45. Image Interaction

Plant imagery is the primary visual element.

Images should:

* Load progressively
* Preserve appropriate aspect ratios
* Avoid unnecessary layout shifts
* Use responsive resolutions
* Lazy-load below-the-fold images

Image cards should not contain excessive UI overlays.

The bookmark icon is the primary persistent card control.

Hearts are not used as a save metaphor.

---

# 46. Desktop Hover Interactions

Desktop may use hover interactions to reveal information.

For plant cards:

```text
Normal
   ↓ hover

Image becomes subtly lighter/faded
   +
Plant name appears over image
```

The transition should be short and subtle.

Hover must never be required to understand navigation.

---

# 47. Mobile Touch Interactions

Mobile does not depend on hover.

Primary interaction rules:

* Tap → open/select
* Long press → contextual editing where explicitly defined
* Swipe → horizontal carousels
* Scroll → content/navigation transitions

Avoid gesture-only functionality for critical actions.

---

# 48. Bottom Sheets

Bottom sheets are preferred for contextual mobile interactions.

Use cases include:

* Take a Photo / Choose from Photos
* Authentication prompts
* Sorting options
* Contextual actions

Bottom sheets should:

* Be dismissible
* Have clear actions
* Avoid excessive content
* Preserve the user's context

---

# 49. Botanical Micro-Interactions

The product may include subtle botanical effects to establish a distinctive visual identity.

Potential effects include:

## Botanical Cursor

Desktop may use a custom cursor treatment inspired by:

* Brown branches
* Stems
* Botanical shapes

The cursor should remain visually precise enough for normal interaction.

## Falling Leaves

Occasional leaf particles may appear during desktop interaction.

The effect should be:

* Sparse
* Short-lived
* Randomized
* Subject to a cooldown

It must not become continuous background noise.

## Cursor-Following Circle

A cursor-following circle may be used in selected hero/landing areas.

The circle may create a subtle color inversion or image treatment.

Do not apply the effect globally.

---

# 50. Motion Guidelines

Motion should communicate:

* State change
* Navigation
* Spatial relationship
* Interaction feedback

Motion should not exist solely to delay an action.

Preferred properties:

* `transform`
* `opacity`

Avoid animation patterns that repeatedly trigger expensive layout calculations.

Core interactions should feel immediate.

Decorative animations must never delay:

* Page navigation
* Bookmark actions
* Search
* Identification
* Back navigation
* Opening/closing bottom sheets

---

# 51. Reduced Motion

Respect the user's reduced-motion preference.

When reduced motion is enabled:

* Disable falling leaves
* Disable cursor-following effects
* Reduce scroll-driven motion
* Replace complex transitions with simple fades or instant state changes
* Preserve all functionality

No feature should depend on decorative motion.

---

# 52. Mobile Motion

Mobile should use a more restrained motion language than desktop.

Do not use:

* Cursor effects
* Hover effects
* Excessive parallax
* Large decorative particle effects

Prefer:

* Short transitions
* Bottom-sheet movement
* Subtle card feedback
* Natural scrolling
* Small state transitions

---

# 53. Performance-Oriented UX

Visual effects must not compromise responsiveness.

## Images

Use:

* Responsive image sizes
* WebP/AVIF where supported
* Lazy loading
* Optimized thumbnails
* CDN delivery where appropriate
* Prefetching only when justified

## Feeds

Initial feed size should be relatively small, approximately 12–20 cards.

Additional content should load progressively.

## Navigation

Navigation should not wait for decorative animation to complete.

## Perceived Performance

When data is not immediately available:

* Preserve existing content where possible.
* Use skeletons.
* Avoid blank screens.
* Give concise status feedback.

---

# 54. Accessibility

All major interactions must remain usable without visual effects.

Requirements:

* Keyboard navigation on web
* Visible focus states
* Accessible names for icon-only buttons
* Appropriate alt text
* Sufficient text contrast
* Sufficient touch target size
* No color-only communication
* Reduced-motion support

Custom cursors must not replace semantic interaction feedback.

Hover-only information must have an accessible alternative.

---

# 55. Responsive Layout Principles

The layout should adapt continuously rather than relying on a single desktop/mobile breakpoint.

General behavior:

### Desktop

* Expanded/collapsible sidebar
* Larger masonry grid
* Hover interactions
* More generous horizontal composition
* More expressive decorative motion

### Tablet

* Collapsible sidebar
* Responsive grid
* Reduced decorative motion
* Touch-friendly interactions

### Mobile

* Slide-in sidebar
* Two-column plant grid
* Top application bar
* Touch-first interaction
* Bottom sheets
* Restrained motion

Exact breakpoints should be selected during implementation based on layout requirements rather than arbitrary device names.

---

# 56. Interaction Priority

When multiple interactions compete visually, use this priority order:

```text id="b0n0p5"
1. Core task
   ↓
2. Navigation
   ↓
3. Content comprehension
   ↓
4. Feedback
   ↓
5. Decorative interaction
```

For example, a leaf animation must never visually overpower a plant identification result.

---

# 57. UX Invariants

The following rules must not be violated without an explicit product decision.

1. The same plant/cultivar must not appear more than once in a recommendation feed.
2. Different cultivars may appear as separate cards.
3. Category pages remain category-pure.
4. Cross-category discovery belongs primarily to Explore.
5. Explore balances personalization with diversity.
6. Explore uses the conceptual 60/20/20 recommendation mix for users with meaningful behavioral data.
7. AI identification does not imply certainty.
8. AI does not serve as the authoritative source for care information.
9. Plant Detail is shared across all plant entry points.
10. Back navigation and sidebar navigation have different purposes.
11. Favorites uses bookmarks rather than hearts.
12. Authentication is requested when needed rather than at application entry.
13. Location permission is requested only when entering Where to Buy.
14. Decorative animation must never delay core actions.
15. Reduced-motion users must retain full functionality.
16. Performance takes priority over visual effects.

---

# 58. UX Quality Bar

Before considering a screen complete, verify:

### Visual

* Is the plant imagery the visual focus?
* Is the hierarchy immediately understandable?
* Does the interface feel clean rather than crowded?
* Are decorative effects subtle enough?

### Interaction

* Can the user understand what is clickable?
* Does every action provide immediate feedback?
* Does Back behave predictably?
* Does mobile work without hover?

### Performance

* Does the screen avoid unnecessary loading?
* Are images appropriately optimized?
* Does animation remain smooth?
* Does navigation happen without artificial delay?

### Accessibility

* Can the core task be completed with keyboard navigation on web?
* Are controls labeled?
* Does reduced motion work?
* Is information available without hover?

### Product consistency

* Does the screen follow established Plant Encyclopedia patterns?
* Does it preserve category purity where applicable?
* Does it avoid introducing an unapproved recommendation behavior?
