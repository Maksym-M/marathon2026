# Homework review notes

A short guide for the reviewer: where everything is, what was done for the homework, and what was added beyond it.

## Where to find things

- **Requirements:** `homework-requirements/` (user stories US20–US23), also on [Google Drive](https://drive.google.com/drive/folders/1-aNJheSml2VuiOdUmVfn_ofuI7ENOSfU?usp=sharing)
- **Designs:** `homework-designs/` (PNG export), source in [Figma](https://www.figma.com/design/D0gF2U2c7BunGhHx8QbLtp/Cart--homework-?node-id=0-1&t=Vl7EtObzT3Bz5ggY-1)
- **Live app (frontend + deployed backend):** https://d12ohokkkl4x8r.cloudfront.net/reservations
- **Code:** `backend/restaurant-backend-app` and `frontend/restaurant-frontend-app` in this repo
- **Manual QA:** `homework-qa/` (US11 requirements analysis, test cases, bug reports)
- **Test automation (AQA):** `aqa/restaurant-aqa-framework/`, reviewer notes in [aqa/restaurant-aqa-framework/homework-review.md](aqa/restaurant-aqa-framework/homework-review.md)
- **Local demo without a backend:** `npm start` in `frontend/restaurant-frontend-app`, then open `http://localhost:4200/reservations?demo` or `http://localhost:4200/reservations/demo-reservation/group-order?demo`

## Backend homework: all 3 levels done ✅

| Level | What | Status |
|---|---|---|
| 1 | `GET /guest/groups/{groupReservationId}/sessions`: participants, their dishes, per-participant and overall totals | ✅ Done |
| 2 | `GET /reservations/{reservationId}/guest-session-id` and `POST /group-reservations/{groupReservationId}/submit`: consolidates submitted carts by `dishId`, sets order to `SubmittedByCustomer` and group to `Submitted`, with all 400/404 rules | ✅ Done |
| 3 | `DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`: removes a guest and returns the updated snapshot | ✅ Done |

Only the Owner can call these endpoints. All of them have xUnit tests and are documented in `backend/restaurant-backend-app/docs/ApiRoutes.md`.

## Frontend homework: practice, all 3 levels and the bonus done ✅

| Level | What | Status |
|---|---|---|
| Practice | Participant block and participants list | ✅ Done |
| 1 | Layout from the designs: header, sections, Grand Total, semantic HTML, status shown with text and colour | ✅ Done |
| 2 | `GroupOrdersService`, state in `signal()`/`computed()`, "My Pre-Order", consolidated pre-order, Grand Total counted from submitted carts only, unsubmitted-guests warning, status badge, Submit with confirmation, loading/error/empty states | ✅ Done |
| 3 | Real API, background sync every 15 s (`timer` + `exhaustMap`, stops after Submit and on leaving the page), "Updated at …", Refresh, silent refresh errors | ✅ Done |
| Bonus | Remove Guest with confirmation dialog | ✅ Done |

**Note:** the UI follows our own designs in `homework-designs/`, not the original page mockup. The Owner works in a cart panel over the menu (tabs: Cart, Participants, Dish Summary), and there is also a "Group Pre-Order" page. Because the component structure changed, the tests in `frontend/restaurant-frontend-app/homework-tests/` were rewritten to match. They keep the same levels and the same key checks, for example that Grand Total counts only submitted carts. Run them with `npm run test:hw`.

## Manual QA homework: US11 done ✅

The story under test is US11 "Guest Reservation and Menu View": a Guest opens the menu after joining through the invite link. The build was given as one screenshot of the guest Menu page (`requarements/image (4).png`), checked against the design `requarements/Guest Flow.png`.

| Deliverable | File | What's inside | Status |
|---|---|---|---|
| Requirements analysis | `US11_Requirements_Analysis.md` (also `.docx`) | 30 gaps in the story (G1–G30), each with a question to the BA/PO; 7 inconsistencies in the design (D1–D7); 7 proposed extra ACs | ✅ Done |
| Test cases | `US11_Test_Cases.xlsx` | 17 test cases covering all 5 ACs, plus Sort by, the Dish details modal and the font from the design | ✅ Done |
| Bug reports | `US11_Bug_Reports.xlsx` | 9 bugs in the given template, each with a screenshot | ✅ Done |

**Test run:** 1 Passed, 6 Failed, 10 Not Run. The Not Run cases need clicks, a second Guest or admin access (adding to the cart, switching tabs, sorting, refreshing after an "On Stop" change), which a static screenshot can't show. They are written in full so they can be run on a live build.

**Bugs found:**

| # | Bug | Priority / Severity |
|---|---|---|
| 1 | Every dish card shows the same salad image | High / Major |
| 2 | The header shows "Main page", "Book a Table", "Reservations" and the profile icon to an unregistered Guest | Medium / Major |
| 3 | The reservation info strip has no date | Medium / Minor |
| 4 | Category tabs: an extra "All dishes" tab, and no "Desserts" tab | Medium / Minor |
| 5 | "Sort by" defaults to "Recommended" instead of "Popular First" | Low / Minor |
| 6 | The cart icon is in the info strip instead of the header | Low / Minor |
| 7 | Unavailable dishes are labelled "Out Of Stock" instead of "Sold Out" | Low / Trivial |
| 8 | Serving size format is inconsistent ("200g" vs "450 g") | Low / Trivial |
| 9 | The heading background image differs from the design | Low / Trivial |

**Note:** where the story is silent, expected results follow the design. Where the story and the design disagree (for example "On Stop" in the AC, "Sold Out" in the design), the conflict is raised as a question in the requirements analysis.

## Beyond the homework

### Backend

- **Edit dishes in a cart:** `PUT /guest/groups/{groupReservationId}/sessions/{guestSessionId}/dishes/{dishId}`. The Owner can change a quantity or remove a dish in their own cart and in carts guests have submitted.
- **Remove an empty seat:** `DELETE /guest/groups/{groupReservationId}/seats` removes a seat nobody has joined yet.
- **Reservations list:** `GET /reservations` now returns `tableNumber` and a `groupPreOrder` summary (`status`, `submitted`, `joined`, `seats`) for each reservation.
- **Security fix:** `GET /reservations/{id}` now checks that the user owns the reservation.
- **Edit reservation guard:** `PUT /reservations` rejects a guest count lower than the number of guests who have already joined.
- **Submit rules:** the Owner's part of the snapshot is built from their order, and the group can be submitted when only the Owner has dishes.
- **Feedback:** one comment per visit instead of two, and no feedback for cancelled reservations.
- **Refactoring:** the 30-minute pre-order cut-off check moved to a shared `PreOrderHelper`.

### Frontend

- **Group cart panel** (opened from the header for the nearest group reservation):
  - collapsible guest blocks;
  - a Dish Summary tab (US22);
  - quantity steppers for the Owner;
  - a confirmation before removing a dish from a guest's cart;
  - removing empty seats.
- **Reservations page redesign (US20):**
  - "Upcoming" and "Past & Cancelled" tabs with counts, and an empty state for each tab;
  - a row-style card showing the date, table, guests, pre-order progress ("x/y") and "Invite Guests";
  - one primary action per card;
  - "Edit" and "Cancel" links that appear only before the 30-minute cut-off;
  - a cancel confirmation dialog.
- **Invite Guests modal:** copies the invite link, and offers a fallback when copying fails.
- **Auth fix:** the token interceptor now sends the token for `/guest/groups/` requests. This fixed a 401 on the live environment.
- **Feedback window:** a single comment field, matching the backend change.
- **Toast:** an accessible close button with an `aria-label`.
- **Mock backend:** extended for the new endpoints, so `?demo` works on the reservations page too.

### Product decisions

- The Owner can edit their own cart and the carts guests have submitted. An edited cart stays submitted unless it becomes empty. Guests aren't notified about these edits.
- The Owner confirms before removing a dish from a guest's cart.
- The Owner can remove any guest, and any seat nobody has joined.
- The seat counter reads "X of Y guest seats".
- **Deviation from US20 AC5:** "View Details" is always shown for group reservations (2+ guests).

## How it was checked

- Frontend: `npx tsc -p tsconfig.app.json --noEmit`, `npx tsc -p tsconfig.spec.json --noEmit`, `BACKEND_URL=https://example.com npm run build`, `npm run test:hw`
- Backend: xUnit test suite (`backend/restaurant-backend-app/Tests`)
- Manual end-to-end check on the deployed environment: Owner books a table, a guest joins through the invite link in another browser, the Owner reviews and submits.
- AI: Claude Code was used as a coding assistant. All changes were reviewed and verified with the checks above.
