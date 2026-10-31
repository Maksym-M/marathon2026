# Restaurant AQA framework

End-to-end tests for the Green & Tasty restaurant application: Reqnroll (Gherkin) + TUnit + Playwright for .NET, with the Screenplay pattern.

Guidance for extending the suite (writing Gherkin, steps, Screenplay, test data, working with an AI assistant) is in [CLAUDE.md](CLAUDE.md).

## Homework solution: Direction 1, group pre-order (levels 1–3)

Reviewer notes, with each homework requirement mapped to its scenario: [homework-review.md](homework-review.md).

- **Direction:** 1. We test our own deployed backend and frontend: the group pre-order (owner, invited guests, waiter).
- **Approach:** BDD + Screenplay, extending this framework. Gherkin describes behaviour in business language; Screenplay tasks and questions do the work through the API or the browser; page objects only hold locators.
- **Test pyramid:** the UI covers the main journeys (the owner sees participants and totals, submits, removes a guest; the waiter sees the order). The API covers authorisation, validation and test data set-up (guests and their carts).

| Level | Feature file | API | UI |
|---|---|---|---|
| L1: seeing participants | [GroupParticipants.feature](Features/GroupPreOrder/GroupParticipants.feature) | 10 | 5 |
| L2: submitting the consolidated pre-order | [SubmitGroupPreOrder.feature](Features/GroupPreOrder/SubmitGroupPreOrder.feature) | 14 | 3 |
| L3: removing a participant | [RemoveParticipant.feature](Features/GroupPreOrder/RemoveParticipant.feature) | 11 | 1 |
| L3: end-to-end journey | [GroupPreOrderJourney.feature](Features/GroupPreOrder/GroupPreOrderJourney.feature) | – | 1 |

### Set up (once per environment)

1. `cp .env.example .env`, then set `UI_URL` and `API_URL` (see "Configuration" below).
2. Provision the dedicated test location. The scenarios book only there, so no real waiter's dashboard ever shows test bookings:
   ```bash
   python3 tools/provision_test_location.py --env .env --region <aws-region> --user-pool-id <cognito-pool-id>
   ```
   This creates "AQA Test Location" with four tables and a test waiter assigned to it, and writes `WAITER_*` / `TESTTABLE_*` into `.env`. It needs the AWS CLI, signed in to the backend's account.
3. Set `MAX_PARALLEL_TESTS=2` if the backend account has the default limit of 10 concurrent Lambda executions; with more parallel scenarios, requests get throttled.

### Run

```bash
dotnet build
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]"                     # all three levels
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=L1]"                                 # one level: L1, L2 or L3
dotnet run --no-build -- --treenode-filter "/*/*/*/*[(Category=group-preorder)&(Category=api)]"     # one layer: api or ui
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=e2e]"                                # the end-to-end journey
```

The report goes to `TestResults/` (TUnit result and TRX; the Reqnroll HTML report in `TestResults/reqnroll/`). Failed UI scenarios leave a Playwright trace in `TestResults/traces/` (`npx playwright show-trace <file>.zip`). In CI, `aqa-tests.yml` uploads all of it as the `aqa-test-results` artifact.

### Behaviour the homework leaves open, and what we test for

| Case | Chosen behaviour (the backend's) |
|---|---|
| Group nobody has joined | The owner is always listed first, with their own pre-order. With no guests, only the owner is listed, with nothing ordered and a total of 0. This differs from the homework's `participants: []`, because from L2 on the owner is a participant too. |
| Someone other than the owner (another customer, the waiter) views, submits or removes | Refused as "not found" (404), exactly like a group or reservation that doesn't exist, so nobody can find out which groups exist |
| Not signed in, or a guest's invite token used on the owner's routes | Asked to sign in (401) |
| Submitting a group that was already submitted | 400 "Group reservation was submitted." |
| Nobody submitted a cart / the submitted carts hold no dishes | 400 "There is nothing to submit: neither you nor your guests have added any dishes." A submitted cart can't actually be empty: a guest can't submit one ("The cart is empty"), and a cart the owner empties goes back to "not submitted". |
| The owner never started a pre-order of their own | 400 "Active pre-order for reservation '…' not found." |
| Removing the last guest | Allowed: the owner is left alone and the seat is free again |
| Removing the owner's own place | 400 "The owner's session cannot be removed from the group." |
| Removing a guest after submitting | 400 "Group reservation was submitted." |
| A guest of another group / a guest who doesn't exist | 400 "Guest session does not belong to this group reservation." / 404 "Guest session with id … not found." |
| A removed guest's invite | Stops working (401 "Invalid guest session token") |
| Grand total | The service's total counts every cart. The page's Grand Total counts the submitted carts, which are what the restaurant will receive. |
| A guest changing a cart they already submitted | Refused ("Guest session is already submitted."). So "the total follows a guest's changes" is checked on a cart that isn't submitted yet. |

### Defects these tests found

Scenarios that fail because the application is wrong stay red, tagged `@known-defect`, with a comment stating the expected behaviour.

| # | Defect | Where it shows |
|---|---|---|
| 1 | The participants list returned after a removal can still contain the guest who was just removed: it is read from an eventually consistent index. The page shows that list, so the guest stays visible until the next 15-second refresh. | 3 `@known-defect` scenarios in RemoveParticipant.feature (intermittent) |
| 2 | Submitting right after a removal can still merge the removed guest's cart into the restaurant's order (for the same reason). | `@known-defect` in GroupPreOrderJourney.feature (intermittent) |
| 3 | Creating or deleting a dish rewrites the location's whole dish list without a version check, so two dishes added at the same time lose one from the menu. | Worked around in the test data: dishes are created once per run, one at a time (`Api/Seeding/RunScopedDishes.cs`) |
| 4 | The waiter's reservation list fails as a whole (500) if any reservation's table was deleted, and deleting a table with cancelled reservations is allowed. | Found when an early version of the tests deleted their tables; the tests no longer do |
| 5 | The waiter dashboard makes three more requests per reservation, so it takes 20–40 s once a location has a few dozen bookings. | The waiter's UI steps wait up to 60 s for it |

### Test data and cleanup

Every scenario creates what it checks and removes it afterwards:
- **Customers:** each scenario registers new ones (owner, another customer). They're named `AQA-Sample-…` and can't be deleted, because there is no endpoint for it.
- **Bookings:** made at the dedicated test location's own tables, at a random free hour 3–60 days ahead, and cancelled by the waiter after the scenario.
- **Dishes:** created once per run with fixed prices, so totals are exact, and removed by the run-level sweep.
- **Tables:** never created or deleted, because a cancelled reservation still points at its table.

The backend has no way to delete a cancelled reservation, so they accumulate at the test location. When its dashboard gets slow, purge them (cancelled bookings of `AQA-Sample-` customers only; it's a dry run unless `--delete` is given):

```bash
python3 tools/purge_test_bookings.py --region <aws-region> --user-pool-id <cognito-pool-id> --waiter-id <test waiter sub> --delete
```

## Run the tests

```bash
cp .env.example .env        # then fill in your own values (see "Configuration")
dotnet build
dotnet run --no-build -- --report-trx                          # everything
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=smoke]"   # by tag
```

Everything a run produces lands in `TestResults/`: the TUnit report and TRX, the Reqnroll report in `reqnroll/`, and Playwright traces in `traces/` (open one with `npx playwright show-trace <file>.zip`).

Some scenarios are expected to fail (`@known-defect`, bugs in the application) or are skipped (`@wip`, no admin account configured). That is intentional.

## Quality gates

| Where | What runs |
|---|---|
| Git hooks (local) | `pre-commit`: `dotnet format` check and Gherkin lint on staged files · `commit-msg`: Conventional Commits · `pre-push`: build with analyzers |
| `aqa-quality.yml` | on every push and pull request: no generated files tracked, format, build (warnings are errors), Gherkin lint, test discovery |
| `aqa-tests.yml` | the scenarios themselves, nightly and on demand (Actions → AQA Tests → Run workflow) |

Enable the hooks once per clone, from the repository root:

```bash
dotnet tool restore --tool-manifest aqa/restaurant-aqa-framework/.config/dotnet-tools.json
git config core.hooksPath aqa/restaurant-aqa-framework/.husky
```

The hooks only act on commits and pushes that touch `aqa/restaurant-aqa-framework`. Gherkin lint needs Node.js.

## Configuration

There is no shared environment and no committed `.env`: copy `.env.example` to `.env` and fill it in with the data of **your own** deployment (see "Testing your own deployment"). `.env` is git-ignored; never commit it.

Settings come from `.env` or real environment variables (variables win):

| Setting | Meaning |
|---|---|
| `UI_URL` | where the frontend is served |
| `API_URL` | base URL of the backend API, including the stage (for example `…/develop`) |
| `WAITER_USERNAME`, `WAITER_PASSWORD` | a waiter account that already exists in the environment under test |
| `TESTTABLE_ID`, `TESTTABLE_WAITER_*` | a table and the waiter assigned to it (only for scenarios that need them) |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD` | an admin account; without it the admin scenarios are skipped |

The full list, with defaults, is in `.env.example`.

## Testing your own deployment

### If you deployed the backend / frontend / DevOps homework

1. `cp .env.example .env` and set `UI_URL` and `API_URL` to your deployment (API URL includes the stage, for example `…/develop`).
2. Waiters cannot be created by signing up, so create a waiter (and, for the admin scenarios, an admin) in *your* environment and put its credentials in `WAITER_*` / `ADMIN_*`. Optionally add a table and its assigned waiter (`TESTTABLE_*`).
3. Run `dotnet build` and a small slice first (`--treenode-filter "/*/*/*/*[Category=smoke]"`).

### If you have not deployed your own homework

If you deployed the original application, test that: set `UI_URL` / `API_URL` as above. Otherwise point the framework at a comparable public site (Booking, Airbnb, OpenTable, …) and write all the layers for it yourself: Gherkin features, step definitions, Screenplay tasks and questions, page objects with locators, and API clients where the site has a usable API. Use only flows you are allowed to automate (no purchases, no real bookings, respect the site's terms), and set `UI_URL` to that site.

1. **Generate a skeleton with Playwright codegen** (see "Presetup instructions"): record one flow, for example search → open a result → check details. Save the generated code to a scratch file, it is only raw material.
2. **Split it across the layers**: locators and small actions go to `Ui/` page objects, the flow becomes a task in `Screenplay/Tasks/`, the read-backs become questions in `Screenplay/Questions/`, the business wording goes to `Features/` and thin glue to `StepDefinitions/`.
3. **Replace weak locators.** Codegen often emits positional or text-fragile selectors. Prefer role + accessible name, label, test id, then CSS (see [CLAUDE.md](CLAUDE.md)); check them in the Playwright inspector.
4. Tag scenarios `@ui` / `@api`, keep them independent, and run `npx gherkin-lint -c .gherkin-lintrc "Features/*.feature"`.

### CI on your own public repository

Publish your work in your own public Git repository (the link is the deliverable). Workflows are disabled in a fresh fork until you enable them. Add the same values under Settings → Secrets and variables → Actions:

- **Variables:** `AQA_UI_URL`, `AQA_API_URL`, `AQA_TESTTABLE_ID`, and optionally `AQA_MAX_PARALLEL_TESTS` (the workflow defaults to 2)
- **Secrets:** `AQA_WAITER_USERNAME`, `AQA_WAITER_PASSWORD`, `AQA_ADMIN_USERNAME`, `AQA_ADMIN_PASSWORD`, `AQA_TESTTABLE_WAITER_USERNAME`, `AQA_TESTTABLE_WAITER_PASSWORD`

`aqa-quality.yml` runs on every push and pull request; `aqa-tests.yml` (nightly and on demand) tests whatever those variables point at. Never put credentials in the repository, in `.env.example` or in test code.

## Presetup instructions

This framework contains PlaywriteInstaller script so you don't need to setup it manually.
For full BDD highlighting in your IDE you may need to install Reqnroll extension https://docs.reqnroll.net/latest/ide-integrations/index.html
To start the Playwright code generator open Powershell in the framework directory and enter the line:
```bash
bin/Debug/net10.0/playwright.ps1 codegen "<your UI_URL or the public site you test>"
```
Build first so `playwright.ps1` exists. The URL is optional.
