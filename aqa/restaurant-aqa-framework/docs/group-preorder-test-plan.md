# Group pre-order: test plan (Direction 1, levels 1–3)

**Object under test:** our own deployment, with backend `chtzc4r9g5…/develop` and frontend `d12ohokkkl4x8r.cloudfront.net`.
**Approach:** BDD + Screenplay, built on the existing framework (Reqnroll + TUnit + Playwright .NET).
**Pyramid:** the UI covers the main user journeys. The API covers authorisation, validation and data set-up (guests, their carts).

**Decisions (2026-10-30):**
- **The tests check the app as it is.** The backend is not changed to match the homework's `participants: []`. The difference is documented in §2 and in the README.
- **Each scenario creates the data it checks:** dishes with fixed prices and its own table, all removed afterwards. No scenario depends on the real menu or on another scenario.

---

## 1. Test data strategy

| Need | How | Cleanup |
|---|---|---|
| Location + waiter | A **dedicated "AQA Test Location"** with its own test waiter (`tools/provision_test_location.py`, `WAITER_*` in `.env`). Every reservation there goes to that waiter, so waiter checks give the same result every run, and no real waiter ever sees test bookings. *(First version: Baratashvili; changed, see §8.)* | not touched |
| Table | The test location's **own tables**, at a random free hour 3–60 days ahead; another hour is tried if a parallel scenario took it. *(First version: a sample table per scenario, deleted afterwards. That broke the waiter's list, see §8.)* | never created or deleted |
| Dishes | **Sample dishes with fixed prices**, created **once per run**, one at a time (`RunScopedDishes`), so totals are exact. | removed by the run-level sweep |
| Owner | A newly registered customer, signed in through the API (`@api`) or the login form (`@ui`) | users can't be deleted; unique email, `AQA-Sample-` name |
| Reservation | Owner books a free table, `guestCount = guests + 1`. A group is created automatically when guestCount ≥ 2. | cancelled by the waiter (tracked right after creation); old cancelled ones purged with `tools/purge_test_bookings.py` |
| Guests | Invite token → `GET /guest/invites/{token}` (group id) → `POST /guest/sessions` (session id + token) → add dishes / submit with the guest token | go away with the reservation |
| Other people | another newly registered customer; the waiter; an anonymous visitor (no token); a guest token used on Owner routes | — |

Time-dependent values are generated (`DateTime.UtcNow.AddDays(2)`). Nothing time-based is written in Gherkin.

## 2. Behaviour the homework leaves open, and what we test for

This table goes into the README.

| Case | Our behaviour (what the backend actually does) |
|---|---|
| Group nobody has joined | The Owner is always the first participant (their own session, `My Pre-Order`). With no guests: only the Owner is listed, without dishes, `total: 0`. **This differs from the homework's `participants: []`** because the Owner is a participant from L2 on. |
| Non-Owner (another customer, the waiter) views, submits or removes | `404`, the same answer as for a group that doesn't exist, so nobody can find out which groups exist |
| Not signed in | `401` |
| Guest token on Owner routes | `401` (the Cognito authoriser rejects it) |
| Submitting again | `400` "Group reservation was submitted." |
| Owner has no dishes but guests submitted | allowed only if the Owner's pre-order exists. If it was never started: `400` "Active pre-order for reservation '…' not found." |
| "Submitted carts contain no dishes" | Can't actually happen: a guest can't submit an empty cart ("The cart is empty"), and a submitted cart the Owner empties goes back to *not submitted*. Both paths are tested, and both end in `400` "There is nothing to submit: neither you nor your guests have added any dishes." |
| Removing the last guest | allowed: only the Owner is left, the total becomes the Owner's own, and the seat shows as "Not joined" again |
| Removing the Owner's own session | `404` "Guest session with id owner-… not found." until the Owner's session exists (it is created when the Owner opens the group page). After that: `400` "The owner's session cannot be removed from the group." |
| Grand total | The API `total` counts **every** cart. The page's "Grand Total" counts only **submitted** carts, i.e. what will be sent to the restaurant. |
| A guest changing a submitted cart | refused: `400` "Guest session is already submitted." So "the total changes when a guest changes their cart" is checked on a cart that isn't submitted yet (API). In the UI, the total changes when a guest submits. |
| Removing a guest after the group was submitted | `400` "Group reservation was submitted." |
| Removed guest's link | their session token stops working (`401`) |

## 3. Scenarios

Tags: `@group-preorder` on every scenario. On top of that: one level tag (`@L1`, `@L2` or `@L3`), one stage tag (`@ui` or `@api`), and `@negative`, `@smoke` or `@e2e` where they fit.

### L1: seeing participants (`Features/GroupPreOrder/GroupParticipants.feature`)

| # | Scenario | Stage |
|---|---|---|
| 1.1 | The Owner sees every guest who joined, their dishes, quantities and each guest's total | ui, smoke |
| 1.2 | The grand total equals the sum of the participants' totals | ui |
| 1.3 | The total changes when a guest changes a cart they haven't submitted (API); the page's Grand Total grows when a guest submits (UI) | api + ui |
| 1.4 | A guest who hasn't submitted yet is listed and marked as not submitted | ui |
| 1.5 | A guest who joined without adding dishes is listed | ui |
| 1.6 | A group nobody has joined: only the Owner, empty seats, total 0 | ui + api |
| 1.7 | Only the Owner can see the list: another customer, a guest, the waiter, a visitor who isn't signed in (Outline) | api, negative |
| 1.8 | Another customer opening the Owner's group page is told they have no access | ui, negative |
| 1.9 | A group that doesn't exist | api, negative |

### L2: submitting the consolidated pre-order (`Features/GroupPreOrder/SubmitGroupPreOrder.feature`)

| # | Scenario | Stage |
|---|---|---|
| 2.1 | The Owner submits: sees "Sent to Restaurant"; the same dish from different guests appears once with the total quantity; carts not submitted are left out | ui, smoke |
| 2.2 | After submitting, the order is `SubmittedByCustomer` and the group is `Submitted`, with the merged lines | api |
| 2.3 | The Owner's own dishes are merged with the guests' same dishes | api |
| 2.4 | The waiter sees the submitted order with the same contents the Owner submitted | ui |
| 2.5 | Can't submit: no submitted carts (+ the Submit button is disabled in the UI) | api + ui, negative |
| 2.6 | Can't submit: submitted carts have no dishes (both paths from §2) | api, negative |
| 2.7 | Can't submit: the Owner's pre-order is not found | api, negative |
| 2.8 | Can't submit: the reservation doesn't exist | api, negative |
| 2.9 | Can't submit: the group doesn't exist | api, negative |
| 2.10 | Can't submit: the group belongs to another reservation | api, negative |
| 2.11 | Submitting an already submitted group again | api, negative |
| 2.12 | Submitting by someone other than the Owner: another customer, the waiter, a guest, a visitor (Outline) | api, negative |

### L3: removing a participant (`Features/GroupPreOrder/RemoveParticipant.feature`)

The UI has a "Remove Guest" button, so the main L3 flows run in the UI.

| # | Scenario | Stage |
|---|---|---|
| 3.1 | Removing one of several guests: the others stay, the total is recalculated, the removed guest's dishes don't count | ui, smoke |
| 3.2 | Removing a guest who already submitted their cart | ui |
| 3.3 | Removing the last guest | ui + api |
| 3.4 | A guest from another group (two separate groups, guests swapped): refused | api, negative |
| 3.5 | A guest who doesn't exist: not found | api, negative |
| 3.6 | Removing by someone other than the Owner (Outline) | api, negative |
| 3.7 | The Owner can't remove their own session (after opening the group, so the session exists) | api, negative |
| 3.8 | A removed guest can no longer use their invite session | api, negative |
| 3.9 | Can't remove after the group was submitted | api, negative |

### End-to-end (`Features/GroupPreOrder/GroupPreOrderJourney.feature`)

| # | Scenario | Stage |
|---|---|---|
| E.1 | Three guests add dishes → the Owner reviews → removes one → submits → the waiter sees the order without the removed guest's dishes | ui, e2e, L3 |

Draft Gherkin (the style to follow):

```gherkin
@group-preorder
Feature: Group pre-order participants

  Background:
    Given the restaurant serves:
      | dish  | price |
      | Soup  | 10.00 |
      | Steak | 25.00 |
    And a customer has booked a table for 4 and invited guests

  @ui @L1 @smoke
  Scenario: The owner sees what every guest ordered
    Given Anna has joined and submitted 2 Soup
    And Ben has joined and submitted 1 Steak
    When the owner opens the group pre-order
    Then the owner sees the participants:
      | participant | dishes    | total |
      | Anna        | Soup × 2  | 20.00 |
      | Ben         | Steak × 1 | 25.00 |
    And the grand total is 45.00

  @api @L1 @negative
  Scenario Outline: Only the owner can see the participants
    When <someone> asks for the group's participants
    Then they are not shown the participants

    Examples:
      | someone                        |
      | another customer               |
      | the waiter                     |
      | Anna, using her guest link     |
      | a visitor who is not signed in |
```

## 4. Framework additions

People: there's one actor per scenario. Each person (Owner, Anna, another customer, the waiter) is a `Participant` record (name, role, bearer token, guest session id) **remembered under their name**. Tasks take the person's name and call the API with that person's token.

| Layer | New |
|---|---|
| `Api/` | `GroupOrdersApi` (sessions, delete session, edit dish, remove seat, submit, status), `GuestSessionsApi` (invite lookup, create session, add/remove dish, submit, menu); `ReservationsApi` += invite-token, guest-session-id |
| `Api/Dtos/GroupOrders/` | `GroupSnapshotResponse`, `ParticipantResponse`, `GroupItemResponse`, `InviteResponse`, `CreateGuestSessionResponse`, `SubmitGroupPreOrderRequest`, `GroupStatusResponse` |
| Tasks | `RegisterAndSignInCustomer(name)`, `ServeDishes(table)`, `BookAGroupTable(guests)` (table + reservation + cleanup), `JoinTheGroup(guest)`, `AddToGuestCart(guest, dish, qty)`, `SubmitGuestCart(guest)`, `AddToOwnersPreOrder(dish, qty)`, `ViewTheParticipants(as)`, `SubmitTheGroupPreOrder(as, groupOverride?, reservationOverride?)`, `RemoveParticipant(guest, as, fromGroup?)`, `OpenTheGroupPreOrderPage`, `RemoveGuestOnThePage(guest)`, `SubmitOnThePage`, `SwitchToTheWaiter`, `OpenTheWaiterPreOrder` |
| Questions | `TheParticipants` (API snapshot), `TheParticipantsShown` (UI panels), `TheGrandTotalShown`, `TheSubmittedDishSummary`, `TheOrderOfTheReservation`, `TheGroupStatus`, `TheWaiterPreOrderShown` |
| `Ui/` | `GroupOrderPage` (+ `ParticipantPanelComponent`, `ConfirmDialogComponent`, `DishSummaryComponent`), `WaiterReservationsPage`, `WaiterPreOrderPage` |
| Steps | `GroupPreOrderStepDefinitions` (+ group outcomes in `OutcomeStepDefinitions`), transformation `{someone}` → persona |
| README | direction and approach at the top, chosen behaviour (§2), running by `@L1/@L2/@L3`, where the report is |
| CI | `.github/workflows/aqa-tests.yml` (on demand + nightly; secrets from README; TestResults uploaded as an artifact; a failing test doesn't stop the pipeline) and `aqa-quality.yml` |

## 5. Implementation order

| Step | Content | Done when |
|---|---|---|
| **S0 Spike** ✅ 2026-10-30 | see §7 | findings recorded in this file; plan adjusted |
| **M1** API layer + seeding | clients, DTOs, set-up tasks, `Participant` memory | one `@api` "Owner sees an empty group" scenario passes twice in a row |
| **M2** L1 | 1.1–1.9 | `--treenode-filter …[Category=L1]` green |
| **M3** L2 | 2.1–2.12 incl. the waiter UI | L2 green |
| **M4** L3 + E2E | 3.1–3.9, E.1 | L3 green; whole `@group-preorder` green twice, in parallel |
| **M5** Delivery | README, CI workflows, GitHub variables and secrets, public repo link | a manual workflow run on GitHub shows the report and artifacts |
| *M6 (optional ★)* | Report Portal (local Docker) + cloud pipeline | — |

The skills in `.claude/skills/` match these steps: `aqa-scenario` (each scenario across its layers), `aqa-page-object` (M2–M4 UI), `aqa-run-triage` (every "done when").

## 6. Risks

- **The group page refreshes every 15 s.** Data set up through the API before the page opens is fine. For changes made while the page is open, the tests click "Refresh" instead of waiting.
- **Shared live environment.** Sample dishes and tables show up briefly on Baratashvili's real menu and floor plan; they are removed after the scenario and swept at the end of the run.
- **Reservations, guest sessions and users can't be swept** (no prefix, or no delete endpoint). The per-scenario cleanup is the only cleanup, so it is registered right after each record is created.
- **The 30-minute lock.** Fixtures are always ≥ 2 days ahead, so the lock never triggers by accident.

## 7. Spike S0 findings (live API, 2026-10-30)

Run as a throwaway script. Everything it created was removed afterwards, except the two sample customers, which can't be deleted.

| # | Question | Answer, and what it changes |
|---|---|---|
| S1 | Does a reservation on a sample table at Baratashvili go to the Baratashvili waiter? | ✅ `waiterId` matches. |
| S2 | Does the guest menu include a sample dish that was just added? | ✅ yes |
| S3 | Can the reservation be cleaned up after the group is submitted? | ✅ the **waiter** can cancel it (`DELETE /reservations` body `{"id": …}`), and the table can be deleted after that. Cleanup uses the waiter token, like every other cleanup. |
| S4 | Which carts does the page's Grand Total count? | only submitted ones (`consolidatePreOrder`); the API `total` counts every cart. See §2. |
| S5 | How does the waiter reach the pre-order? | `/waiter-reservations` → the reservation card's "Pre-order" button → `/waiter-pre-order` |
| S6 | Which group-status route works? | `GET /guest/groups/{id}/status`. `/group-reservations/{id}/status` is not deployed (403 from the API Gateway); ApiRoutes.md is wrong. |

Other facts that change M1:
- **There are two error body shapes:** `{"error": "…"}` and `{"message": "…", "errorCode": "…"}` (all `ValidationException`s). `ApiResponseExtensions.ReadErrorMessageAsync` only reads `error`, so it has to read `message` too.
- `DELETE /reservations` expects `{"id": …}`. The framework's `CancelReservationRequest` already maps it correctly; only the spike script sent the wrong key.
- `POST /guest/sessions` answers `{SessionId, SessionToken}` (PascalCase). Guest calls use `/guest/sessions/{id}/dishes|submit|menu` with `Bearer <SessionToken>`.
- Reservation and order `status` come back as **numbers** (reservation `0` = Reserved, order `3` after a group submit). The DTOs need numeric enums, or a check against `OrderStatus`.
- Observed messages: not Owner / missing group → `404` "Group reservation was not found". Submit by a non-Owner / missing reservation → `404` "Reservation not found.". Guest or anonymous → `401` "Unauthorized". Missing guest → `404` "Guest session with id {id} not found.". Removed guest's token → `401` "Invalid guest session token". Second submit / removal after submit → `400` "Group reservation was submitted.". No Owner order → `400` "Active pre-order for reservation '{id}' not found.".
- The merge works: Owner Soup ×1 + Anna Soup ×2 → order Soup ×3. A removed guest's dishes are not included.

## 8. Findings during implementation (2026-10-30)

| Finding | Kind | What was done |
|---|---|---|
| The `api-handler` Lambda had 128 MB; new instances took 5–7 s and calls under load timed out | environment | raised to 1024 MB (live + `lambda_config.json`) |
| DynamoDB tables were provisioned at 1 read and 1 write unit; Dishes was throttled 572 times, so guest "add dish" took 50–85 s | environment | all tables on-demand (live + `deployment_resources.json`) |
| The AWS account allows 10 concurrent Lambda executions; bursts get throttled (500) | environment | `MAX_PARALLEL_TESTS=2`; a quota increase is recommended |
| Creating or deleting a dish rewrites the location's dish list without a version check, so concurrent dishes get lost | app defect | dishes created once per run, one at a time |
| The participants list returned after a removal can be stale (eventually consistent index) | app defect | `@known-defect` (3 scenarios) |
| Submitting right after a removal can merge the removed guest's cart | app defect | `@known-defect` (E2E) |
| The waiter's reservation list fails whole (500) if a reservation's table was deleted; deleting such a table is allowed | app defect | tests no longer delete tables; the 144 broken test records were removed |
| The waiter dashboard makes 3 requests per reservation (slow with many bookings) | app defect | waiter UI steps wait up to 60 s; test bookings moved to a dedicated location; purge tool |
| The storefront leaves the login page before it has stored the user; navigating away then expires the session | test bug | `SignInThroughTheLoginForm` now waits until the user is stored |

