@group-preorder @L3
Feature: Removing a participant from the group pre-order

  The owner can remove a guest from the group. The guest's cart goes with them: the
  list and the total are recalculated, and the guest's invite session stops working.
  Only a guest who exists and belongs to this group can be removed, only by the
  owner, and only while the group is still collecting pre-orders.

  Background:
    Given the restaurant serves:
      | dish  | price |
      | Soup  | 10.00 |
      | Steak | 25.00 |
      | Salad | 7.50  |
    And a customer has booked a table for 4
    And Anna has joined the group and submitted:
      | dish | quantity |
      | Soup | 2        |
    And Ben has joined the group and submitted:
      | dish  | quantity |
      | Steak | 1        |
    And Cara has joined the group and added:
      | dish  | quantity |
      | Salad | 1        |

  Rule: The owner can remove a guest, and the total no longer counts them

    # Known defect (expected to fail until fixed, intermittently): the participants list returned
    # after a removal can still show the guest who was just removed (see "Removing the last guest").
    @api @smoke @known-defect
    Scenario: Removing one guest keeps the others and recalculates the total
      When the owner removes Ben from the group
      Then the participants are:
        | participant | dishes    | total | status        |
        | owner       | none      | 0     | submitted     |
        | Anna        | Soup × 2  | 20.00 | submitted     |
        | Cara        | Salad × 1 | 7.50  | not submitted |
      And the total of all carts is 27.50

    # Known defect (expected to fail until fixed): the page shows the participants list the removal
    # answered with, which can still contain the guest who was just removed (see the API scenario
    # below); the guest only disappears at the page's next 15-second refresh.
    @ui @smoke @known-defect
    Scenario: The owner removes a guest who already submitted, on the page
      When the owner opens the group pre-order
      And the owner removes Ben from the group
      Then the participants are:
        | participant | dishes   | total |
        | owner       | none     | -     |
        | Anna        | Soup × 2 | 20.00 |
        | Cara        | none     | -     |
      And the grand total shown is 20.00

    # Known defect (expected to fail until fixed, intermittently): the participants list returned
    # after a removal is read from an eventually consistent index, so it can still show the guest who
    # was just removed (seen live: Cara still listed). Expected: the removed guest is never listed.
    @api @known-defect
    Scenario: Removing the last guest leaves only the owner
      Given the owner has removed Anna from the group
      And the owner has removed Ben from the group
      When the owner removes Cara from the group
      Then only the owner is listed, with nothing ordered

    @api @negative
    Scenario: A removed guest can no longer use their invite
      Given the owner has removed Ben from the group
      When Ben goes back to the group with their invite
      Then Ben is turned away

  Rule: Only a guest of this group who exists can be removed

    @api @negative
    Scenario: A guest of another group cannot be removed
      Given the owner has another booking, for 3
      And Zoe has joined the other booking's group
      When the owner removes Zoe from the group
      Then the owner is told "Guest session does not belong to this group reservation."

    @api @negative
    Scenario: A guest who never joined cannot be removed
      When the owner removes a guest who never joined
      Then the owner is told the guest could not be found

    @api @negative
    Scenario: The owner cannot remove their own place in the group
      Given the owner has opened the group pre-order
      When the owner tries to remove their own place in the group
      Then the owner is told "The owner's session cannot be removed from the group."

  Rule: Only the owner can remove a guest, and only before submitting

    @api @negative
    Scenario Outline: Someone other than the owner cannot remove a guest
      When <someone> removes Ben from the group
      Then <outcome>
      And Ben is still in the group

      Examples:
        | someone                        | outcome                        |
        | another customer               | the group appears not to exist |
        | the waiter                     | the group appears not to exist |
        | a guest of the group           | they are asked to sign in      |
        | a visitor who is not signed in | they are asked to sign in      |

    @api @negative
    Scenario: A guest cannot be removed once the group is submitted
      Given the owner has pre-ordered:
        | dish | quantity |
        | Soup | 1        |
      And the owner has submitted the group pre-order
      When the owner removes Anna from the group
      Then the owner is told "Group reservation was submitted."
