Epic: Table Reservation

US name: Owner Views Reservations List

Role: Customer (Owner)

Description: As a group owner, I want to see all my reservations in one list split into upcoming and past ones, so that I can quickly find a reservation and manage its group pre-order.

Pre-conditions:
- The Customer is logged in.
- The Customer has created at least one reservation via Book a Table flow.
- Group pre-order controls are defined in US1 from lesson; the "Group Pre-Order" screen is defined in US21–US23.

Acceptance Criteria (AC):
1. The header tab "Reservations" must be shown only to a logged-in Customer and must open the "Reservations" page; clicking [ View Reservation ] in the "Reservation Confirmed" modal or [ View Reservations ] in the "Pre-Order Submitted" modal must open the same page on the "Upcoming" tab. The page must show "Hello, {FirstName} {LastName}" and the info box "For any assistance with your table reservation or pre-order, please call us at this number: +123 445 678 8990."
2. The page must show only reservations created by the logged-in Customer, in two tabs: "Upcoming" (default; status "Reserved" or "In Progress") and "Past & Cancelled" (status "Completed" or "Cancelled"). A reservation is "In Progress" from its start time until its end time, and "Completed" after its end time.
3. "Upcoming" must be sorted by date and start time, nearest first; "Past & Cancelled" must be sorted by date and start time, most recent first.
4. Each reservation card must show the status at the top of the card ("Reserved", "In Progress", "Completed" or "Cancelled"), Location, Date, Time ("{start} - {end}"), Guests ("{N} Guests", or "1 Guest") and "Table {N}" (e.g. "Reserved", "48 Rustaveli Avenue", "Oct 17, 2026", "12:00 - 14:00", "4 Guests", "Table 1").
5. A "Reserved" card must show one pre-order action, for any guests count: [ Add Pre-Order ] until the Owner has sent the pre-order to the restaurant for the first time (opens the menu with the reservation strip; the Owner's cart already holds the Guests' submitted pre-orders), and [ Group Pre-Order ] after that (opens the "Group Pre-Order" screen, US21). After the pre-order cut-off (US23 AC7), [ Add Pre-Order ] is hidden and [ Group Pre-Order ] opens the screen in view mode.
6. A "Reserved" card with guests count ≥ 2 must show the hint "Share link with your guests so they can place their pre-orders" with [ Invite Guests ], and the counter "Pre-orders: {X}/{Y}" with the same X and Y as the progress line on the "Group Pre-Order" screen (US23 AC3) (e.g. 4 Guests, the Owner + 2 Guests submitted → "Pre-orders: 3/4"; 1 Guest submitted and the Owner hasn't → "Pre-orders: 1/4"). [ Invite Guests ] opens the "Guest Invite link" modal with "Share this link with your guests so they can place their pre-orders", the "Invite Link" with the copy icon and "Copy this link, then paste it into a message to your guests."; copying behaves as in US1 from lesson ("Link copied! Share it with your friends."). If copying fails (e.g. the browser blocks clipboard access), the modal must show "Couldn't copy the link. Copy it manually:" with the full invite link as selectable text. The hint and [ Invite Guests ] are hidden when every seat has a participant (US23 AC1) and after the cut-off (US23 AC7). A card with guests count = 1 shows neither the hint, the button nor the counter.
7. "In Progress", "Completed" and "Cancelled" cards must show no pre-order actions ([ Add Pre-Order ], [ Group Pre-Order ], [ Invite Guests ]): once the visit starts, the pre-order is handled by the waiter, and the invite link is active only while the reservation is "Reserved" (US1 from lesson). A card with guests count ≥ 2 keeps showing the last "Pre-orders: {X}/{Y}" value.
8. If the Customer has no reservations at all, the page must show "No Reservations", "Looks like you haven't made any reservations yet." and [ Book a Table ] instead of the tabs. If only one tab has no reservations, the system must show "You have no upcoming reservations." with [ Book a Table ] on "Upcoming", and "No past or cancelled reservations." on "Past & Cancelled".
9. The list must reflect the data at the moment the page is opened or refreshed; no real-time push is required (same rule as US11 from lesson).

Assumptions / Open questions:
- Note: supporting story for US21–US23. It is the entry point to the "Group Pre-Order" screen and the "Reservations" page that US1 from lesson refers to.
- Note: the button is labelled [ Invite Guests ], as in US1 from lesson.
- Conflict with US1 from lesson: US1 knows only "Reserved", "Cancelled" and "Completed". This story adds "In Progress"; US1 must say that the invite link also expires when the reservation becomes "In Progress".
- Assumption: "In Progress" and "Completed" are set automatically from the reservation start and end time.
- The orange change indicator on the card and the cut-off notification bar on this page are defined in US23.
- Out of scope for now (will be covered in a later release; full list in US21): the Owner's pre-order flow behind [ Add Pre-Order ]; cancelling or editing a reservation from the list (cancellation is allowed up to 30 min before the start); feedback after the visit.
- Open question: once an "Owner changes reservation location" story exists (see US21), should the card show a note such as "Location changed" and a new invite link?
- Open question: should "Past & Cancelled" show all history or only a limited period (e.g. last 12 months)?
