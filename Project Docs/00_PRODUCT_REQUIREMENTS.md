# Plant Encyclopedia — Product Requirements

## 1. Product Overview

Plant Encyclopedia is a modern, image-first plant discovery and identification application.

The product helps users:

1. Discover plants visually.
2. Identify a plant from a photo.
3. Find the exact plant, species, or cultivar when possible.
4. Learn reliable, practical plant information.
5. Find places where the plant may be purchased.
6. Save interesting plants to Favorites.
7. Discover related plants through personalized recommendations.

The product should feel like a real consumer application rather than a technology demonstration.

The core experience is:

> **Discover → Identify → Compare → Learn → Find → Save → Discover More**

The product prioritizes:

1. Information accuracy and trustworthiness
2. Fast perceived and actual performance
3. Frictionless user experience
4. Strong visual design
5. Responsible use of AI

---

# 2. Product Positioning

Plant Encyclopedia is a general-purpose plant encyclopedia and visual discovery product.

It is **not** limited to roses, flowers, or gardening instructions.

The initial target user is:

> A general user who sees an unfamiliar plant and wants to understand what it is, especially when they already know the broad type but want a more specific name or cultivar.

Example:

> "I know this is a rose, but what exact variety is it?"

The application should therefore support both:

* broad plant discovery
* specific plant/cultivar identification

---

# 3. Target Users

## Primary User

A general curious plant discoverer.

Typical behaviors:

* Browses plants visually
* Searches for plant names
* Identifies plants from photographs
* Wants concise and useful care information
* Wants to know where a plant may be purchased
* Saves interesting plants for later

## Secondary User

A plant enthusiast or grower.

This user may eventually want:

* More detailed plant information
* Collections
* Personal plant records
* Care reminders
* Growth journals

These advanced gardening features are not part of the initial MVP.

---

# 4. Core User Journey

The primary product journey is:

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

Users may enter the journey from multiple points.

Examples:

```text
Explore → Plant Group → Plant → Detail

Explore → Search → Plant → Detail

Identify → Identification Results → Plant → Detail

Plant Detail → Similar Plants → More → Plant Detail

Plant Detail → Where to Buy
```

All plant entry points must eventually use the same Plant Detail experience.

---

# 5. Core Product Areas

The application contains the following primary destinations:

* Identify
* Explore
* Favorites
* Login / Account

The primary navigation is a sidebar on desktop and a slide-in sidebar on mobile.

The application does not use a persistent bottom navigation bar.

---

# 6. Plant Data Model Principles

The application's own database is the primary source of truth for the user-facing encyclopedia.

External data sources are used to populate and enrich the internal database.

Primary structured sources include:

* iNaturalist
* GBIF

External data must be normalized and validated before being stored as application data.

The application must not simply expose raw third-party API responses.

Plant records should retain provenance information such as:

* External source
* External identifier
* Retrieved timestamp
* Relevant source metadata

---

# 7. Taxonomy and Naming

The product supports plant information at multiple levels, including:

* Family
* Genus
* Species
* Cultivar / variety
* Common name

The user interface should not expose unnecessary taxonomy complexity.

The naming hierarchy should prioritize user comprehension.

## When a cultivar is available

Example:

```text
Rosa 'Peace'
Hybrid Tea Rose
Rosa × hybrida
```

The first line is the primary user-facing name.

## When a cultivar is unavailable

Example:

```text
Rose
Rosa
```

Scientific names should be displayed because common names vary across languages and regions.

Initially, representative English common names are sufficient for the UI.

---

# 8. Plant Identification

Image identification is a core product capability.

The user should be able to:

1. Open Identify.
2. Take a photo or choose an existing photo.
3. Preview the selected image.
4. Retake or confirm the image.
5. Start identification.
6. Receive ranked plant candidates.
7. Open a candidate's Plant Detail page.

The camera experience should feel familiar and lightweight, similar to common mobile camera/photo workflows.

The application should not promise perfect identification accuracy.

AI-generated candidates must be validated against plants that are supported by the application's own encyclopedia before being presented as encyclopedia results.

AI should primarily act as an identification/candidate-generation engine.

AI should **not** be treated as the authoritative source for plant care information.

---

# 9. Identification Results

Identification results display:

* One primary candidate
* Two additional candidates
* A More entry

Conceptually:

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

The primary candidate is the highest-ranked validated candidate.

The More entry is **not a fourth candidate**.

It opens a broader discovery grid centered on the identification result.

The interface must not display artificial confidence percentages.

Unvalidated AI candidates must not be presented as authoritative encyclopedia results.

---

# 10. Explore

Explore is the primary plant discovery experience.

Explore should feel personalized while maintaining discovery and diversity.

## Initial users

For users without meaningful behavioral data:

* Category representation should be approximately balanced.
* Plants should be diverse.
* Recommendations should not be popularity-only.
* The feed should provide curated discovery rather than pure randomness.

The system should avoid repeatedly displaying the same plant.

## Users with behavioral data

The default recommendation mix is:

* **60%** strongly related to the user's interests
* **20%** somewhat related / adjacent categories
* **20%** substantially different categories

This ratio is a product-level guideline rather than a requirement that every exact batch contain mathematically perfect proportions.

The purpose is to balance personalization with exploration.

---

# 11. Recommendation Diversity Rules

The same plant or cultivar must not appear more than once within a recommendation feed.

For example, the following is valid:

```text
Rosa 'Peace'
Rosa 'Mr. Lincoln'
Rosa 'Iceberg'
```

The following is invalid:

```text
Rosa 'Peace' — Image A
Rosa 'Peace' — Image B
Rosa 'Peace' — Image C
```

One plant/cultivar may have many source images internally, but a recommendation feed should represent that plant with at most one image card.

Different cultivars of the same plant group may appear as separate cards.

Personalization must not eliminate all category diversity.

Even highly personalized users should retain an exploration component.

---

# 12. Explore Recommendation Philosophy

Explore should gradually move from:

> Diversity

toward:

> Personalization + Diversity

as user behavior accumulates.

Relevant signals may include:

* Plants viewed
* Plants bookmarked
* Search activity
* Plant groups viewed
* Categories explored
* Recent interactions

The recommendation system should avoid trapping users in an overly narrow interest bubble.

The exact recommendation weights may be tuned after observing real usage.

The initial implementation may use rule-based ranking rather than machine learning.

---

# 13. Explore Categories

Explore provides broad plant categories such as:

* Flowers
* Trees
* Fruits
* Vegetables
* Herbs
* Succulents

All primary categories are visible from the beginning.

Categories use moderately large horizontal pills.

The category row:

* Supports horizontal scrolling
* Hides the scrollbar
* Uses edge fading/gradients to indicate additional content
* Removes the fade when the user reaches the corresponding end

The product currently assumes this category set is stable and does not need to dynamically add categories during normal use.

---

# 14. Category Browsing

Category browsing is intentionally different from Explore.

Example:

```text
Explore
   ↓
Flowers
   ↓
Roses
   ↓
Specific Rose Cultivars
   ↓
Plant Detail
```

A Category page should show image-based plant-group cards.

For example:

```text
Roses
Tulips
Peonies
Orchids
Lilies
Hydrangeas
...
```

The user selects a plant group by clicking its image card.

The next page displays plants/cultivars within that group.

Selecting a specific plant opens Plant Detail.

## Category Purity

Category pages must remain within the selected category.

For example:

> Flowers

must not insert Trees, Vegetables, or unrelated plants as recommendations.

Similarly, a Rose browsing page should remain focused on the relevant rose group.

Cross-category discovery belongs primarily to Explore.

User behavior inside Category pages can still contribute to Explore personalization signals.

---

# 15. Search

Search is accessible from Explore.

The initial Explore screen does not display a persistent search bar.

Instead, a Search icon activates Search mode.

Search mode includes:

* Back navigation
* Sticky search bar
* Recent searches
* Database-backed suggestions
* Search results grid
* Infinite scrolling

Search suggestions should prioritize the application's own database.

Search should support relevant matching against:

* Common names
* Scientific names
* Cultivars
* Plant groups
* Taxonomy-related terms

If an appropriate plant is not already present in the internal database, the system may retrieve structured external information and ingest a validated plant record.

Search results should prioritize query relevance rather than Explore personalization.

---

# 16. Search History

Search History and View History are separate concepts.

## Search History

Contains explicit search queries.

Users can remove individual search entries.

Removing a search should also remove that query's recommendation influence where applicable.

Removing search history must not delete plant records from the encyclopedia.

---

# 17. View History

View History records plants that the user actually opens.

View History is primarily an internal recommendation signal in the MVP.

It does not need to be exposed as a user-facing history page initially.

Recently viewed plants should not automatically be presented as a separate UI section in the MVP.

View history may influence Explore recommendations.

---

# 18. Favorites

Favorites is a user-facing saved-plant collection.

Favorites requires authentication.

The MVP contains one collection:

> All Favorites

Folders are intentionally deferred.

Favorites uses an image-first grid similar to Explore and Search.

Each card includes a bookmark control.

Unbookmarking a plant immediately removes it from Favorites.

A snackbar should provide:

> Removed from Favorites. Undo

The Undo action should restore the bookmark.

---

# 19. Favorite Authentication Flow

If an unauthenticated user selects the bookmark action:

```text
Save this plant

Sign in to save plants to your Favorites.

[ Sign in ] [ Cancel ]
```

Authentication should occur only when required.

The application should attempt to preserve the user's original intent so that after successful login the bookmark action can continue naturally.

---

# 20. Favorites Arrange Mode

Normal Favorites mode should remain visually clean.

Mobile:

* Long press enters Arrange Mode.
* Cards use a subtle jiggle/wobble animation.
* Delete/minus controls appear.
* Cards can be dragged to reorder.

Desktop:

* Use an explicit Arrange/Edit action or contextual menu.
* Do not rely on hover alone to activate editing.

The MVP should store a logical ordering value rather than raw screen coordinates.

Future folder support can build on this ordering model.

---

# 21. Empty Favorites

When there are no saved plants, Favorites displays three decorative plant images above the primary CTA.

These images:

* Are not clickable
* Are examples of plants users could discover
* Fade/transition toward the lower part of the screen

The primary CTA is:

> Explore Plants →

The CTA navigates to Explore.

Once the user has Favorites, this empty state disappears.

---

# 22. Plant Detail

Every plant entry point uses the same Plant Detail page.

Entry points include:

* Explore
* Search
* Identification
* More
* Favorites
* Category browsing

Plant Detail structure:

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

# 23. Plant Detail Hero

The hero is an immersive, nearly full-screen image.

Controls:

* Back button in the upper-left
* Bookmark button in the upper-right

Plant information appears directly over the image.

As the user scrolls:

* The image gradually fades/transitions
* The background moves toward a warm beige/neutral tone
* Text becomes increasingly readable

The transition should feel continuous rather than like a hard image boundary.

---

# 24. Plant Detail Quick Care

Quick Care contains four compact cards:

* Light
* Water
* Temperature
* Soil

Example:

```text
Light
Full Sun

Water
When top 2–3 cm of soil is dry

Temperature
15–25°C

Soil
Well-drained
```

Care information should be actionable and condition-based where reliable.

Each card is expandable.

The card includes a small downward arrow in the lower-right corner.

When expanded, the arrow changes direction.

The entire card is interactive.

---

# 25. Plant Detail Content

## Blooming

Blooming is concise and not an accordion.

Example:

```text
🌸 May–October
↻ Repeat bloomer
```

## Health

Health is concise and not an accordion.

Possible information includes:

* Common pests
* Common diseases
* Disease resistance when reliable

## About

About may be collapsible because content length varies.

It may contain:

* Origin
* Background
* Short history
* Other useful contextual information

The initial collapsed view should remain concise.

---

# 26. Growing Section

A dedicated Growing section is excluded from the MVP.

Detailed content such as:

* Pruning instructions
* Propagation
* Fertilizing schedules
* Bouquet stem handling
* Winter protection

may be considered later based on actual user needs.

The MVP should avoid turning Plant Encyclopedia into a full gardening manual.

---

# 27. Where to Buy

Where to Buy appears above Similar Plants.

Plant Detail contains a compact entry card:

> 🌱 Where to Buy
> Find nurseries, garden centres & flower shops near you
> Explore →

Selecting it opens a dedicated Where to Buy page.

The MVP initially focuses on Vancouver / British Columbia.

The page can contain:

## Nearby

Nearby businesses may include:

* Nurseries
* Garden centres
* Flower shops

Default ordering:

> Distance

Alternative sorting:

* Rating
* Relevance

## Online

Online retailers may also be shown.

The product must not claim live inventory unless a reliable inventory API is available.

Approximate prices should include source/date information where possible.

---

# 28. Location Permissions

Location permission must not be requested when the application first opens.

Location is requested only when the user enters Where to Buy.

If location access is granted:

> Use the user's current location.

If location is denied or unavailable:

> Default to Vancouver, BC and allow manual location selection.

---

# 29. Similar Plants

Similar Plants appears at the bottom of Plant Detail.

The initial view contains three cards.

A horizontal carousel allows users to reveal approximately three additional cards.

A:

> View More →

action opens a broader More page.

Similar Plants should remain centered on the current plant.

The MVP may use:

* Taxonomic relationships
* Plant-group relationships
* Curated relationships

More advanced visual similarity can be introduced later.

---

# 30. More Page

More is a discovery grid centered on the current plant or context.

It is not a fourth identification result.

It is not a replacement for Explore.

Its purpose is:

> "Show me more plants related to what I am currently looking at."

The user can select a plant and enter Plant Detail.

---

# 31. Navigation

Desktop uses an expandable/collapsible sidebar.

Expanded:

```text
Plant Encyclopedia

Identify
Explore
Favorites

Login
```

Collapsed:

```text
🌿
📷
🔍
🔖

👤
```

Mobile uses a slide-in sidebar.

The mobile top bar contains:

* Menu button on the left
* Plant Encyclopedia centered
* Login/account button on the right

The login/account entry is also available inside the sidebar.

---

# 32. Back Navigation

Back navigation is separate from destination navigation.

Sidebar navigation moves directly to destinations.

Back navigation returns to the previous navigation state.

Example:

```text
Explore
  ↓
Search
  ↓
Plant A
  ↓
Plant B
  ↓
Plant C
```

Back should return:

```text
Plant C
  ↓
Plant B
  ↓
Plant A
  ↓
Search
```

The application should preserve relevant navigation context such as:

* Search query
* Scroll position
* Feed state
* Category
* Previous result set

where practical.

---

# 33. Mobile UX

Mobile uses a responsive, touch-first adaptation of the desktop product.

Core principles:

* Two-column image grid
* Image-first cards
* No hover-dependent interaction
* Tap for navigation
* Long press for Favorites Arrange Mode
* Bottom sheets for contextual actions
* Immersive Plant Detail
* Vertical scrolling

The product structure remains consistent between web and mobile while interactions adapt to the available input method.

---

# 34. Loading States

## General Data

Use skeleton UI rather than blocking the entire screen with a spinner.

Skeletons should preserve the approximate structure of the content being loaded.

## Plant Detail

Use structured skeleton content for:

* Hero image
* Plant name
* Quick Care
* Content sections

## Image Identification

Keep the user's selected image visible.

Display a concise message:

> Identifying your plant…

Do not expose unnecessary internal AI processing steps.

Do not use fake progress percentages.

---

# 35. Empty States

## Search

When no relevant plant is found:

```text
No plants found

We couldn't find a match for "[query]".

Try another name or search term.
```

If appropriate, the product may offer image identification as an alternative discovery method.

## Category

If a category or plant group has no available records:

```text
No plants available yet.
```

Do not replace the empty category with unrelated plants from another category.

---

# 36. Error Handling

Errors should be understandable and recoverable.

Network/data loading failures should provide:

* Clear contextual message
* Try Again action

Example:

```text
Something went wrong

We couldn't load the plants.
Please check your connection and try again.

[ Try Again ]
```

Identification failures should avoid falsely claiming that the plant does not exist.

Example:

```text
We couldn't identify this plant

The image may be unclear, or the plant
may not be in our database yet.

[ Try Again ] [ Choose Another Photo ]
```

Error messages should describe the user's next useful action.

---

# 37. Accessibility

Accessibility is a product requirement, not an optional enhancement.

The application should support:

* Keyboard navigation for major web interactions
* Accessible labels for controls
* Sufficient color contrast
* Meaningful alternative text for images
* Touch targets of appropriate size
* Information that is not communicated through color alone
* Reduced-motion preferences
* Functional UI when decorative animation is disabled

Decorative interactions must never be required to understand or operate the application.

---

# 38. Performance

Performance is a first-class product requirement.

The application should prioritize:

* Fast perceived response
* Fast navigation
* Efficient image delivery
* Minimal unnecessary API requests
* Smooth scrolling
* Lightweight animations

## Images

Use:

* Responsive images
* WebP and/or AVIF where appropriate
* Thumbnail sizes
* Lazy loading
* Appropriate prefetching
* CDN delivery where appropriate

## Infinite Feeds

Do not load the entire dataset at once.

Load a relatively small initial batch and progressively load additional results.

An initial target of approximately 12–20 cards may be used and tuned during implementation.

## API and Data

Prefer:

```text
Own Database
   ↓
Cache
   ↓
External API
```

rather than repeatedly requesting external sources.

External data retrieval should be minimized and cached where appropriate.

---

# 39. Interaction Design

The visual design should be modern, clean, image-first, and subtly interactive.

The product may use distinctive botanical interactions inspired by high-end interactive portfolio/web experiences.

Examples may include:

* Botanical custom cursor treatment
* Occasional falling leaf effects
* Cursor-following visual effects
* Image/color inversion interactions
* Scroll-driven transitions
* Subtle hover states
* Gentle bookmark transitions

These interactions must remain subordinate to usability.

The governing principle is:

> **Performance and usability take priority over decorative animation.**

Decorative effects should:

* Not delay navigation
* Not block user input
* Not interfere with reading
* Not make scrolling feel slow
* Avoid unnecessary layout recalculation
* Prefer efficient transforms and opacity
* Respect reduced-motion preferences
* Be more conservative on mobile

Desktop may use more expressive interaction.

Mobile should remain cleaner and more tactile because there is no mouse/hover interaction.

---

# 40. Product Trust

The application should clearly distinguish between:

* Verified/source-backed encyclopedia information
* AI-generated identification candidates
* Approximate retailer/price information

AI must not be used to fabricate authoritative plant information.

When information is uncertain or unavailable, the product should communicate that limitation rather than presenting speculation as fact.

---

# 41. MVP Non-Goals

The following are intentionally excluded from the initial MVP:

* Full gardening manual
* Detailed pruning/propagation system
* Pet safety information without reliable specialized sources
* User folders for Favorites
* Advanced social features
* Chat functionality
* Live retailer inventory without a reliable API
* Full recommendation machine-learning system
* iOS application
* Excessive gamification
* Heavy decorative animation
* Google Images as a core plant-data source

These may be reconsidered after the core product is working and user behavior provides evidence for their value.

---

# 42. Product Success Criteria

The MVP should demonstrate that a user can:

1. Open the application and understand its purpose immediately.
2. Identify a plant using a photo.
3. Browse plants visually.
4. Search for a plant.
5. Open a detailed plant page.
6. Understand the plant's basic care requirements.
7. Find related plants.
8. Explore where the plant may be purchased.
9. Save a plant to Favorites after authentication.
10. Receive useful and diverse Explore recommendations.

The product should feel fast, coherent, visually polished, and trustworthy.

---

# 43. Guiding Product Principle

Plant Encyclopedia should not feel like a collection of technical features.

It should feel like:

> **A beautiful, fast, trustworthy place to discover plants and understand exactly what you are looking at.**
