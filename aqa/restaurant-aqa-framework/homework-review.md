# AQA homework review notes

A short guide for the reviewer: what was done for the Test Automation homework, where each requirement is covered, and how to run it.

## Where to find things

- **Direction and approach, setup, run commands, report location:** [README.md](README.md), section "Homework solution"
- **Scenarios:** [Features/GroupPreOrder/](Features/GroupPreOrder/)
- **CI runs (report and Playwright traces as the `aqa-test-results` artifact):** [Actions → AQA Tests](https://github.com/Maksym-M/marathon2026/actions/workflows/aqa-tests.yml)
- **Application under test (our own frontend and backend):** https://d12ohokkkl4x8r.cloudfront.net

## Direction 1, group pre-order: all 3 levels done ✅

- **Direction:** 1. We test our own deployed backend and frontend (see [../../homework-review.md](../../homework-review.md) for the Backend and Frontend homework).
- **Approach:** BDD + Screenplay, extending the existing framework (Reqnroll + TUnit + Playwright for .NET). Gherkin describes behaviour, Screenplay tasks and questions do the work through the API or the browser, page objects only hold locators.
- **Test pyramid:** UI for the main journeys, API for authorisation, validation and test data set-up.

| Level | Feature file | API | UI | Status |
|---|---|---|---|---|
| L1: seeing participants | [GroupParticipants.feature](Features/GroupPreOrder/GroupParticipants.feature) | 10 | 5 | ✅ Done |
| L2: submitting the consolidated pre-order | [SubmitGroupPreOrder.feature](Features/GroupPreOrder/SubmitGroupPreOrder.feature) | 14 | 3 | ✅ Done |
| L3: removing a participant | [RemoveParticipant.feature](Features/GroupPreOrder/RemoveParticipant.feature) | 11 | 1 | ✅ Done |
| L3: end-to-end journey | [GroupPreOrderJourney.feature](Features/GroupPreOrder/GroupPreOrderJourney.feature) | – | 1 | ✅ Done |

## Homework requirements and where they are covered

### L1: seeing participants

| Requirement | Scenario |
|---|---|
| Guests join and add dishes; the Owner sees everyone, their dishes, quantities and totals | "The owner sees each guest's dishes, quantities and total" (API), "The owner's page shows what each guest ordered" (UI) |
| The total is the sum of the participants and follows a guest's cart changes | "The total follows a guest's changes to their cart" (API), "The page's grand total grows when a guest submits their cart" (UI) |
| A guest who hasn't submitted, and a guest with no dishes, are listed and marked | "A guest who has not submitted yet…", "A guest who joined without ordering anything…" (API), "The page marks guests who have not submitted, or ordered nothing" (UI) |
| A group nobody has joined | "A group nobody has joined lists only the owner" (API), "…shows only the owner and free seats" (UI) |
| Only the Owner sees the list: not another customer, a guest, the waiter or a visitor | "Someone other than the owner cannot see the participants" (API outline, 4 roles), "Another customer cannot open the owner's group pre-order" (UI) |
| A group that doesn't exist | "The owner looks for a group that does not exist" |

### L2: submitting the consolidated pre-order

| Requirement | Scenario |
|---|---|
| Same dishes merged and summed, unsubmitted carts left out, order `SubmittedByCustomer`, group `Submitted` | "Same dishes are merged and carts that were not submitted are left out" (API), "The owner submits on the page and sees what the restaurant receives" (UI) |
| The waiter sees the submitted order | "The waiter sees the pre-order the owner submitted" (API), "The waiter sees the submitted pre-order on their dashboard" (UI) |
| No submitted cart | "Nobody has submitted a cart" (API), "The page does not let the owner submit while nothing can be sent" (UI) |
| Submitted carts hold no dishes | "A guest cannot submit an empty cart", "The only submitted cart was emptied by the owner" |
| Order not found | "The owner never started a pre-order of their own" |
| Reservation or group doesn't exist | "The reservation does not exist", "The group does not exist", "The group belongs to another reservation" |
| Submitting a second time | "A group that was already submitted cannot be submitted again" |
| Submit by someone other than the Owner | "Someone other than the owner cannot submit" (outline, 4 roles) |

### L3: removing a participant

The page has a Remove Guest button (the Frontend bonus), so removal is covered in the UI as well as the API.

| Requirement | Scenario |
|---|---|
| Remove one of several: the rest stay, the total is recalculated | "Removing one guest keeps the others and recalculates the total" (API) |
| Remove a guest who already submitted | "The owner removes a guest who already submitted, on the page" (UI) |
| Remove the last guest | "Removing the last guest leaves only the owner" |
| A guest of another group: 400 | "A guest of another group cannot be removed" |
| A guest who doesn't exist: 404 | "A guest who never joined cannot be removed" |
| Removal by someone other than the Owner | "Someone other than the owner cannot remove a guest" (outline, 4 roles) |
| End to end: guests order → Owner reviews → removes one → submits → waiter sees the order without them | "The waiter receives the pre-order without the guest the owner removed" (UI) |
| Extra checks | "A removed guest can no longer use their invite", "The owner cannot remove their own place in the group", "A guest cannot be removed once the group is submitted" |

## General requirements

| Requirement | How it's met | Status |
|---|---|---|
| End-to-end scenarios; data set-up and state checks outside the UI allowed | UI scenarios drive the page; set-up (guests, carts) goes through the API | ✅ |
| Positive and negative scenarios on every level | `@negative` scenarios on L1, L2 and L3 | ✅ |
| Test data created before and removed after | Bookings at a dedicated test location, cancelled after each scenario; dishes created once per run and swept; customers get unique `AQA-Sample-` names | ✅ |
| Time-dependent data generated dynamically | Bookings at a random free hour 3–60 days ahead | ✅ |
| Independent tests, can run in parallel | Each scenario seeds its own data; `MAX_PARALLEL_TESTS` controls parallelism | ✅ |
| Report, and material for failed tests | TUnit and TRX report, Reqnroll HTML report, Playwright trace for every failed UI scenario | ✅ |
| CI with report and artifacts; a failing test doesn't stop the pipeline | `aqa-tests.yml` (nightly and on demand, `continue-on-error`, artifacts always uploaded); `aqa-quality.yml` (format, build, Gherkin lint on every push) | ✅ |
| README: direction, approach, set-up, subsets by tag or level, report location, chosen behaviour | [README.md](README.md) | ✅ |
| No `.env`, passwords or URLs in the repository | `.env` is git-ignored; CI uses repository variables and secrets | ✅ |
| ⭐ Report Portal and a pipeline on a cloud provider | Not done | ❌ |

## Behaviour the homework leaves open

Chosen behaviour (the backend's) is listed in full in [README.md](README.md), "Behaviour the homework leaves open". The main points:

- **A group nobody has joined:** the Owner is always listed first, with their own pre-order, so the list holds only the Owner with nothing ordered and a total of 0. This differs from the homework's `participants: []`, because from L2 on the Owner is a participant too.
- **Someone other than the Owner** (another customer, the waiter): 404, the same as a group that doesn't exist, so nobody can find out which groups exist.
- **Not signed in, or a guest's invite token:** 401.
- **Submitting again:** 400 "Group reservation was submitted."
- **Removing the last guest:** allowed; the Owner is left alone.

## Known defects

Scenarios that fail because the application is wrong are left red on purpose, tagged `@known-defect`, with a comment giving the expected behaviour. They are not test bugs.

| # | Defect | Scenarios |
|---|---|---|
| 1 | After a removal, the participants list can still contain the removed guest (it's read from an eventually consistent index) | 3 in RemoveParticipant.feature (intermittent) |
| 2 | Submitting right after a removal can still send the removed guest's dishes to the restaurant | 1 in GroupPreOrderJourney.feature (intermittent) |

Three more defects (dish list overwrites, the waiter list failing on a deleted table, a slow waiter dashboard) are described in the README with their workarounds.

## How to run

```bash
cp .env.example .env        # fill in UI_URL, API_URL and the waiter account
dotnet build
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]"   # all three levels
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=L1]"               # one level: L1, L2 or L3
```

Results go to `TestResults/`. Full set-up (the dedicated test location and waiter) is in the [README](README.md).
