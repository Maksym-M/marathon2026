Epic: Order Management
US name: Owner Views Group Dish Summary
Role: Customer (Owner)
Description: As a group owner, I want the consolidated group pre-order to show each dish the group ordered with its total quantity, so that I can check at a glance what the table will receive without adding up individual orders.

Pre-conditions:
- The Owner has opened the "Group Pre-Order" screen for their reservation (US21).
- At least one participant has a "Submitted" pre-order.

Acceptance Criteria (AC):
1. The "Group Pre-Order" screen must show a "Dish Summary" section below the "Grand Total", listing every dish (menu item) that appears in at least one "Submitted" pre-order of the session ("Submitted" as defined in US21 AC5, including Guest pre-orders the Owner hasn't sent to the restaurant yet); dishes that exist only in draft (unsubmitted) carts or only in "Rejected" lines (US23 AC5) must not appear.
2. Each line must show the dish image (a circle with the first letter of the dish name if the dish has no image or was removed from the menu, same as US21 AC5), the dish name, "× {total qty}" (the sum of that dish's counted quantities across all participants) and the line total in $ with 2 decimals (= Σ UnitPrice × qty across participants).
3. Each dish must appear only once in the "Dish Summary", even if several participants ordered it (e.g. Guest A × 1 + Guest B × 2 → "× 3").
4. Lines must be sorted by total quantity, highest first; dishes with equal quantity are sorted by dish name A–Z, case-insensitive.
5. The sum of all "Dish Summary" line totals must equal the "Grand Total" (US21). "Rejected" lines are excluded from both (US23 AC5), e.g. Guest A × 1 confirmed + Guest B × 1 rejected → "× 1".
6. The "Dish Summary" must use the names and UnitPrices saved at submission (US21 AC6): a dish that is now "On Stop" stays listed with the label "On Stop" and is still counted; a dish removed from the menu is listed with its saved name and the letter placeholder; a dish submitted at two UnitPrices is one line with the summed total (e.g. $8.00 + $9.00 → "× 2", "$17.00").
7. The "Dish Summary" must be shown whenever the "Group Pre-Order" screen shows participant blocks (US21 AC10a) and at least one counted line exists, including view mode after the cut-off (US23 AC7) and reservations with 1 guest. It must not be shown in the "not sent yet" state (US21 AC10b, US23 AC4), when the reservation is "In Progress", "Completed" or "Cancelled" (US21 AC10c, AC10d), or when every line is "Rejected".

Assumptions / Open questions:
- None. Shared assumptions are listed in US21.
