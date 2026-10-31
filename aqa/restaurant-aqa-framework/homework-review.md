# AQA homework review notes

**Direction 1, group pre-order: all 3 levels done ✅.** We test our own deployed backend and frontend. Approach: BDD + Screenplay (Reqnroll + TUnit + Playwright for .NET), UI for the main journeys, API for authorisation, validation and test data.

## Where to find things

- **Setup, run commands, report location, chosen behaviour:** [README.md](README.md), section "Homework solution"
- **Scenarios:** [Features/GroupPreOrder/](Features/GroupPreOrder/)
- **CI runs (report and traces in the `aqa-test-results` artifact):** [Actions → AQA Tests](https://github.com/Maksym-M/marathon2026/actions/workflows/aqa-tests.yml)
- **Application under test:** https://d12ohokkkl4x8r.cloudfront.net

## What's covered

| Level | Feature file | API | UI | What it checks |
|---|---|---|---|---|
| L1 | [GroupParticipants.feature](Features/GroupPreOrder/GroupParticipants.feature) | 10 | 5 | Participants, dishes and totals; unsubmitted and empty carts; an empty group; access only for the Owner; a group that doesn't exist |
| L2 | [SubmitGroupPreOrder.feature](Features/GroupPreOrder/SubmitGroupPreOrder.feature) | 14 | 3 | Merged dishes, unsubmitted carts left out; the waiter sees the order; every "cannot submit" rule; a second submit; a submit by someone other than the Owner |
| L3 | [RemoveParticipant.feature](Features/GroupPreOrder/RemoveParticipant.feature) | 11 | 1 | Removing one of several, a submitted guest, the last guest; another group's guest (400); a guest who doesn't exist (404); removal by someone other than the Owner |
| L3 | [GroupPreOrderJourney.feature](Features/GroupPreOrder/GroupPreOrderJourney.feature) | – | 1 | End to end: guests order → Owner removes one → submits → waiter sees the order without them |

General requirements are met: data is created and cleaned up per scenario, dates are dynamic, tests run in parallel, failed UI tests leave a Playwright trace, and CI uploads the report without stopping on failures. The ⭐ task (Report Portal + a cloud pipeline) is not done.

## Worth knowing

- **Empty group:** the Owner is always listed, so a group nobody has joined shows only the Owner with a total of 0, not `participants: []`. Other choices for undefined cases are in the README.
- **`@known-defect`:** 4 scenarios fail on purpose, because of real application bugs (a removed guest can still appear in the list or in the submitted order). They are described in the README.

## How to run

```bash
cp .env.example .env        # fill in UI_URL, API_URL and the waiter account
dotnet build
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]"   # all levels
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=L1]"               # one level: L1, L2 or L3
```
