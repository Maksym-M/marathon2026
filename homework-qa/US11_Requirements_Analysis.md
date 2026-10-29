# US11 — Guest Reservation and Menu View: Requirements Analysis

**Sources:** `requarements/US11_Guest Reservation and Menu View.md`, design `requarements/Guest Flow.png`, related story `Requirements/US10_Guest Joining a Group Pre-order Session via Link.md`.

## 1. Summary

The story is clear about its main goal: a Guest can see the menu and pre-order available dishes. However, it leaves out several behaviours that appear in the design. The design itself is also inconsistent in a few places. Without answers to the questions below, testers and developers will make different assumptions, especially about the unavailable-dish state, the "Add to subcart" feedback, and data freshness.

## 2. Gaps in the User Story

### 2.1 Title, scope and pre-conditions

| # | Gap | Question to BA/PO |
|---|-----|-------------------|
| G1 | The title says "Reservation **and** Menu View", but the description and ACs only cover the menu. The reservation info strip ("You are making a pre-order for the reservation…") appears in the design but has no AC. | Which reservation fields must the strip show (address, table, date, time)? In what format? |
| G2 | The pre-condition refers to **US_G10**, but the joining story is named **US10**. | Please align the story IDs. |
| G3 | The pre-condition says status "Created/"Reserved"", while US10 only mentions "Reserved". | Which exact statuses allow menu access? |
| G4 | The story doesn't say what happens if the reservation is cancelled or finishes while the Guest is already on the menu. | Should the Guest be redirected to the "session expired" page on the next action or refresh? |
| G5 | The story doesn't say what happens when the Guest refreshes the page or opens the link again after joining. | Does the Guest stay joined without re-entering their name, or is the name asked again? |
| G6 | The header and breadcrumbs aren't specified for a Guest. The design shows only the logo and cart icon, but a "Main page" breadcrumb is present. | Can an unregistered Guest go to Main page, Book a Table or Reservations, or log in? Is the session kept if they leave the menu? |

### 2.2 Menu structure (AC1)

| # | Gap | Question to BA/PO |
|---|-----|-------------------|
| G7 | "Full restaurant menu" isn't defined. It's unclear whether this means the menu of the reservation's location or a global menu. | Whose menu is shown? |
| G8 | Categories are only examples ("e.g."). The order, the default tab and the presence of an "All dishes" tab aren't defined. The design shows Appetizers / Main Courses / Desserts, with Appetizers selected. | Please give the final category list, their order and the default tab. |
| G9 | The design has a **Sort by** control (Popular First, Newest First, Price: Low to High, Price: High to Low), but no AC covers it. | Is sorting in scope? What is the default sort? How is "Popular" calculated? Where do unavailable dishes appear in the sort order? |
| G10 | Empty, loading and error states aren't defined: an empty category, a menu with no dishes, or a failed API call. | What should the Guest see in each case? |
| G11 | The number of dishes per category and pagination or lazy loading aren't defined. | Is there a limit? |

### 2.3 Dish card content (AC2)

| # | Gap | Question to BA/PO |
|---|-----|-------------------|
| G12 | The price format isn't defined. The design shows whole numbers ("$17") and also "12$" in another screen. Real prices can have cents ($22.50). | What currency, symbol position and number of decimals should prices use? |
| G13 | The weight format isn't defined. The design uses "430 g". | Please confirm the format "<N> g". |
| G14 | AC2 asks for the "current availability status" on each item, but the design has no label for **available** dishes. Only unavailable dishes have a badge. | Is a missing badge an acceptable way to show "available"? |
| G15 | Long names, missing images and missing prices or weights aren't covered. | Should long names wrap or be truncated? Is there a placeholder image? |
| G16 | The design shows a **Dish details modal** (description, nutrition, Pre-order button) that no AC mentions. | Is the modal part of US11? Can it be opened for an unavailable dish? |

### 2.4 Pre-order action (AC3)

| # | Gap | Question to BA/PO |
|---|-----|-------------------|
| G17 | Button visibility isn't defined. In the design only one card shows a [Pre-Order] button, which looks like a hover state. | Is the button always visible, or only on hover? |
| G18 | The feedback after adding a dish isn't defined: a toast, the cart counter badge (the design shows a badge with "1"), or a change in the button state. | What should the Guest see after adding a dish? |
| G19 | It isn't defined what happens when the same dish is added again, or whether there is a maximum quantity. | Should the quantity increase or a new line be added? Is there a quantity limit? |
| G20 | It isn't defined who can see the personal subcart (only this Guest, or also the Owner and other Guests), or where it is stored if the page is refreshed. | Who sees the subcart? Does it survive a refresh? |
| G21 | The button label differs between screens: "Pre-order" (AC and modal) and "Pre-Order" (cards). | Which label is correct? |

### 2.5 Unavailable ("On Stop") dishes (AC4)

| # | Gap | Question to BA/PO |
|---|-----|-------------------|
| G22 | The status has three different names: **"On Stop"** in the AC, **"Sold Out"** in the design and "Out Of Stock" in the build. | What is the single label the Guest should see? |
| G23 | The look of an unavailable card isn't defined. The design greys out the whole card and shows no button. | Should the button be hidden or shown as disabled? |
| G24 | It isn't defined what happens if a dish goes On Stop **after** the menu was loaded and the Guest then clicks Pre-Order. | What error message should the Guest see? |
| G25 | It isn't defined what happens if a dish goes On Stop **after** it was added to the subcart. | Is it removed or flagged in the cart? Can the pre-order still be submitted? |

### 2.6 Data freshness (AC5)

| # | Gap | Question to BA/PO |
|---|-----|-------------------|
| G26 | The menu is only required to be current "at open or refresh". Real-time updates (polling or push) are therefore not expected. | Please confirm this is acceptable given G24 and G25. |
| G27 | The story doesn't say which data must be refreshed (availability, price, weight, new or removed dishes, images) or where that data comes from (admin/waiter panel). | This affects the test data set-up. |

### 2.7 Non-functional

| # | Gap |
|---|-----|
| G28 | No performance criteria are given, such as menu load time. |
| G29 | No accessibility criteria are given: alt text for dish images, contrast of the greyed-out text on unavailable cards, keyboard access to tabs and buttons. |
| G30 | Supported browsers and screen sizes aren't listed. The design is desktop-only (1440px). |

## 3. Design inconsistencies (Guest Flow.png)

| # | Where | Issue |
|---|-------|-------|
| D1 | Card vs Dish modal | "Fresh Strawberry Mint Salad" costs **$17** on the card but **$15** in the modal. |
| D2 | Invite modal vs info strip | The invite modal shows "Rustavelli 46 Ave", 12:00–14:00. The strip shows "48 Rustaveli Avenue", 12:00–13:30. |
| D3 | Category tabs | Typo: "Main Cources". |
| D4 | Cart panel | The Grand Total is **$27**, but the items add up to $17 + $10 + $19 = **$46**. |
| D5 | Menu behind the cart | The price is written "12$" here and "$12" on the menu. |
| D6 | Pre-order submitted screen | It lists 2 items that total $27, while the cart above shows 3 items. |
| D7 | Unavailable dish badge | The badge says "Sold Out", but the US says "On Stop". |

## 4. Proposed additional acceptance criteria

1. The reservation info strip shows: "You are making a pre-order for the reservation {address}, Table {N}, {Mmm DD, YYYY}, {HH:MM – HH:MM}".
2. The category tabs are Appetizers, Main Courses and Desserts, in that order. Appetizers is selected by default. Only dishes of the selected category are shown.
3. Sort by offers Popular First (default), Newest First, Price: Low to High and Price: High to Low.
4. The price format is "$" followed by the amount, with no decimals for whole amounts (TBC). The weight format is "{N} g".
5. An unavailable dish card is greyed out (image and text), shows a "Sold Out" badge, and has no active Pre-Order button. Clicking it doesn't change the cart.
6. After [Pre-Order] is clicked, the dish is added to the Guest's subcart with quantity 1, and the cart badge counter increases by 1. Clicking the same dish again increases its quantity.
7. If a dish became unavailable after the page loaded, clicking [Pre-Order] shows the error "This dish is no longer available" and the menu refreshes.
