@group-preorder @L3 @e2e
Feature: Group pre-order from invitation to the waiter

  The whole journey: guests order through the invite link, the owner reviews the
  group, removes a guest, submits, and the waiter receives the pre-order without
  anything the removed guest had ordered.

  Background:
    Given the restaurant serves:
      | dish  | price |
      | Soup  | 10.00 |
      | Steak | 25.00 |
      | Salad | 7.50  |
    And a customer has booked a table for 4

  # Known defect (expected to fail until fixed, intermittently): submitting reads the guests' carts
  # from an eventually consistent index, so a guest removed just before the owner submits can still be
  # merged into the order (seen live: the removed guest's Steak reached the restaurant). Expected: a
  # removed guest's dishes are never sent.
  @ui @smoke @known-defect
  Scenario: The waiter receives the pre-order without the guest the owner removed
    Given the owner has pre-ordered:
      | dish | quantity |
      | Soup | 1        |
    And Anna has joined the group and submitted:
      | dish | quantity |
      | Soup | 2        |
    And Ben has joined the group and submitted:
      | dish  | quantity |
      | Steak | 1        |
    And Cara has joined the group and submitted:
      | dish  | quantity |
      | Salad | 2        |
    When the owner opens the group pre-order
    Then the participants are:
      | participant | dishes    | total |
      | owner       | Soup × 1  | 10.00 |
      | Anna        | Soup × 2  | 20.00 |
      | Ben         | Steak × 1 | 25.00 |
      | Cara        | Salad × 2 | 15.00 |
    When the owner removes Ben from the group
    And the owner submits the group pre-order
    Then the owner sees that the pre-order was sent to the restaurant with:
      | dish  | quantity |
      | Soup  | 3        |
      | Salad | 2        |
    When the waiter opens the reservation's pre-order
    Then the waiter sees the pre-order:
      | dish  | quantity |
      | Soup  | 3        |
      | Salad | 2        |
